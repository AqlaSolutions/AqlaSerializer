// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
#endif
#endif


namespace AqlaSerializer.Meta
{
    partial class MetaType : ILateReferenceSerializerProxy
    {
        // to be compatible with auxiliary type serializer and don't add overhead we don't decorate enums with netobject
        bool GetRootStartsGroup()
        {
            return GetRootNetObjectMode() || GetRootLateReferenceMode();
        }

        bool GetRootNetObjectMode()
        {
            return !IsSimpleValue && IsNetObjectValueDecoratorNecessary(_model, ValueFormat.Reference) && !ForceCompactFormatForRoot;
        }

        bool GetRootLateReferenceMode()
        {
            return !IsSimpleValue && !_model.ProtoCompatibility.SuppressOwnRootFormat && !ForceCompactFormatForRoot;
        }

        bool IsSimpleValue => Helpers.IsEnum(Type);

        static void AddDependenciesRecursively(MetaType mt, Dictionary<MetaType, bool> set)
        {
            RuntimeTypeModel model = mt._model;
            foreach (ValueMember field in mt.Fields)
            {
                int key = model.GetKey(field.MemberType, false, false);
                if (key >= 0)
                {
                    var otherMt = model[key];
                    if (!set.ContainsKey(otherMt))
                    {
                        set.Add(otherMt, true);
                        AddDependenciesRecursively(otherMt, set);
                    }
                }
            }
        }

        void InitSerializers()
        {
            if (_rootSerializer != null)
                return;

            int opaqueToken = 0;
            try
            {
                _model.TakeLock(ref opaqueToken);

                // double-check, but our main purpse with this lock is to ensure thread-safety with
                // serializers needing to wait until another thread has finished adding the properties
                if (_rootSerializer != null) return;
                {
                    IsFrozen = false;

                    AddDependenciesRecursively(this, new Dictionary<MetaType, bool>());

                    FinalizeSettingsValue();

                    bool mayContainReferencesInside;
                    _serializer = BuildSerializer(out mayContainReferencesInside);
                    var newRootSer = BuildRootSerializer();
                    if (!(newRootSer is ForbiddenRootStub)) newRootSer = WrapRootSerializer(newRootSer, mayContainReferencesInside);
                    if (!IsValueType && !IsAutoTuple && _surrogate == null)
                    {
                        try
                        {
                            _lateReferenceSerializer = new LateReferenceSerializer(Type, GetKey(true, false), GetKey(true, true), _model);
                        }
                        catch (Exception e)
                        {
                            // subtypes can be late references which is hard to predict on this stage
                            // but normally tests don't throw
                            _lateReferenceSerializerBuildException = e;
                        }
                    }
                    _rootSerializer = newRootSer;
                    IsFrozen = true;
                }
            }
            catch (Exception ex)
            {
                var msg = Helpers.TryGetWrappedExceptionMessage(ex, Type);
                if (msg == null) throw;
                Helpers.RethrowSpecific(ex, msg);
            }
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
#if FEAT_COMPILER
            if (_model.AutoCompile) CompileInPlace();
#endif
        }

        IProtoTypeSerializer BuildRootSerializer()
        {
            IProtoTypeSerializer ser = MakeModelTypeRerouteForRoot();
            if (Helpers.IsEnum(Type))
            {
                if (!GetRootStartsGroup())
                    ser = new RootFieldNumberDecorator(ser, ListHelpers.FieldItem);
                return ser;
            }

            Type itemType = _settingsValueFinal.Member.Collection.ItemType;

            if (itemType != null)
            {
                // standard root decorator won't start any field
                // in compatibility mode collections won't start subitems like normally
                // so wrap with field
                if (!GetRootStartsGroup())
                    ser = new RootFieldNumberDecorator(ser, TypeModel.EnumRootTag);
                return ser;
            }


            if (_surrogate != null
                || _settingsValueFinal.IsAutoTuple)
            {
                return ser;
            }

            // root serializer should be always from base type
            Debug.Assert(!OmitTypeSearchForRootSerialization);
            if (BaseType != null)
                return new ForbiddenRootStub(Type);
            return ser;
        }

        IProtoTypeSerializer BuildSerializer(out bool mayContainReferencesInside)
        {
            mayContainReferencesInside = false;
            // reference tracking decorators (RootDecorator, NetObjectDecorator, NetObjectValueDecorator)
            // should always be applied only one time (otherwise will consider new objects as already written):
            // #1 For collection types references are handled either by RootDecorator or
            // by ValueMember which owns the value (so outside of this scope)
            // because the value is treated as single object
            // #2 For members: ordinal ValueMembers are used and they will handle references when appropriate

            if (Helpers.IsEnum(Type))
            {
                Debug.Assert(IsSimpleValue);
                return new WireTypeDecorator(WireType.Variant, new EnumSerializer(Type, GetEnumMap(), true));
            }

            Type itemType = _settingsValueFinal.Member.Collection.ItemType;

            if (itemType != null)
            {
                if (_surrogate != null)
                    throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot use a surrogate");

                IProtoTypeSerializer ser;
                Type defaultType = null;
                ResolveListTypes(_model, Type, ref itemType, ref defaultType);

                if (_fields.Count != 0)
                    throw new ArgumentException("Repeated data (an array, list, etc) has inbuilt behavior and can't have fields");

                // apply default member settings to type settings too
                var s = _settingsValueFinal.Member;
                // but change this:
                s.EffectiveType = Type; // not merged with anything so assign
                s.Collection.ConcreteType = _settingsValueFinal.ConstructType ?? defaultType;
                s.Collection.Append = false; // allowed only on members
                // this should be handled as collection
                if (s.Collection.ItemType == null) s.Collection.ItemType = itemType;

                var vs = _rootNestedVs.Clone();
                vs.SetSettings(s, 0);
                vs.DefaultLevel = new ValueSerializationSettings.LevelValue(s.MakeDefaultNestedLevel());

                WireType wt;
                ser = (IProtoTypeSerializer)_model.ValueSerializerBuilder.BuildValueFinalSerializer(vs, false, out wt);

                mayContainReferencesInside = !Helpers.IsValueType(itemType);

                return ser;
            }

            if (BaseType != null && !BaseType.GetFinalSettingsCopy().IgnoreListHandling && RuntimeTypeModel.CheckTypeIsCollection(_model, BaseType.Type))
                throw new ArgumentException("A subclass of a repeated data (an array, list, etc should be handled too as a collection");

            // #2

            if (_surrogate != null)
            {
                MetaType mt = _model[_surrogate], mtBase;
                while ((mtBase = mt.BaseType) != null) { mt = mtBase; }
                return new SurrogateSerializer(_model, Type, _surrogate, mt.Serializer);
            }
            if (_settingsValueFinal.IsAutoTuple)
            {
                if (_tupleCtor == null) throw new InvalidOperationException("Can't find tuple constructor");
                mayContainReferencesInside = _tupleFields.Any(f => !Helpers.IsValueType(f.MemberType));
                return new TupleSerializer(_model, _tupleCtor, _tupleFields.ToArray(), _settingsValueFinal.PrefixLength.GetValueOrDefault(true));
            }

            var fields = new BasicList(_fields.Cast<object>());
            fields.Trim();

            int fieldCount = fields.Count;
            int subTypeCount = _subTypes?.Count ?? 0;
            int[] fieldNumbers = new int[fieldCount + subTypeCount];
            IProtoSerializerWithWireType[] serializers = new IProtoSerializerWithWireType[fieldCount + subTypeCount];
            int i = 0;
            if (subTypeCount != 0)
            {
                mayContainReferencesInside = true;
                Debug.Assert(_subTypes != null, "_subTypes != null");
                foreach (SubType subType in _subTypes)
                {
                    if (!subType.DerivedType.IgnoreListHandling && _model.MapType(ienumerable).IsAssignableFrom(subType.DerivedType.Type))
                    {
                        throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be used as a subclass");
                    }
                    fieldNumbers[i] = subType.FieldNumber;
                    serializers[i++] = subType.GetSerializer(_model);
                }
            }
            if (fieldCount != 0)
            {
                foreach (ValueMember member in fields)
                {
                    fieldNumbers[i] = member.FieldNumber;
                    serializers[i++] = member.Serializer;
                    if (!Helpers.IsValueType(member.MemberType)) mayContainReferencesInside = true;
                }
            }

            BasicList baseCtorCallbacks = null;
            MetaType tmp = BaseType;

            while (tmp != null)
            {
                MethodInfo method = tmp.HasCallbacks ? tmp.Callbacks.BeforeDeserialize : null;
                if (method != null)
                {
                    if (baseCtorCallbacks == null) baseCtorCallbacks = new BasicList();
                    baseCtorCallbacks.Add(method);
                }
                tmp = tmp.BaseType;
            }
            MethodInfo[] arr = null;
            if (baseCtorCallbacks != null)
            {
                arr = new MethodInfo[baseCtorCallbacks.Count];
                baseCtorCallbacks.CopyTo(arr, 0);
                Array.Reverse(arr);
            }
            return new TypeSerializer(_model, Type, fieldNumbers, serializers, arr, BaseType == null, !_settingsValueFinal.SkipConstructor, _callbacks, _settingsValueFinal.ConstructType, _factory, _settingsValueFinal.PrefixLength.Value);
        }

        ModelTypeSerializer MakeModelTypeRerouteForRoot()
        {

            return new ModelTypeSerializer(Type, GetKey(true, false), this, _model);
        }

        IProtoTypeSerializer WrapRootSerializer(IProtoTypeSerializer s, bool mayContainReferencesInside)
        {
            return new RootDecorator(
                        Type,
                        GetRootNetObjectMode(),
                        mayContainReferencesInside && !_model.ProtoCompatibility.SuppressValueEnhancedFormat,
                        !GetRootLateReferenceMode(),
                        s,
                        _model);

        }

        // special handling may support hierarchy but anyway it should be written with a concrete serializer (not base/root)
        bool OmitTypeSearchForRootSerialization
        {
            get
            {
                if (Helpers.IsEnum(Type)) return true;

                if (_settingsValueFinalSet)
                {
                    Helpers.MemoryBarrier();
                    if (_settingsValueFinal.Member.Collection.ItemType != null) return true;
                }
                else
                {
                    // this may change but we can't finalize in GetKey!
                    if (IsList || (!IgnoreListHandling && Type.IsArray)) return true;
                }
                return false;
            }
        }

#if FEAT_IKVM || !FEAT_COMPILER
        internal bool IsCompiledInPlace => false;
#else
        internal bool IsCompiledInPlace => _serializer is CompiledSerializer;
#endif

        public bool IsSerializerReady
        {
            get
            {
                Helpers.MemoryBarrier();
                return _serializer != null;
            }
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IProtoSerializerWithWireType ISerializerProxy.Serializer => Serializer;

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IProtoSerializerWithWireType ILateReferenceSerializerProxy.LateReferenceSerializer => LateReferenceSerializer;

#if FEAT_COMPILER

        /// <summary>
        /// Compiles the serializer for this type; this is *not* a full
        /// standalone compile, but can significantly boost performance
        /// while allowing additional types to be added.
        /// </summary>
        /// <remarks>An in-place compile can access non-public types / members</remarks>
        public void CompileInPlace()
        {
            var s = Serializer;
            var r = RootSerializer;
#if FAKE_COMPILE
            return;
#endif
#if FEAT_IKVM
            // just no nothing, quietely; don't want to break the API
#else
            if (s is CompiledSerializer) return;
            _serializer = CompiledSerializer.Wrap(s, _model);
            _rootSerializer = CompiledSerializer.Wrap(r, _model);
#endif
        }
#endif


        internal bool IsPrepared()
        {
#if FEAT_COMPILER && !FEAT_IKVM
            return _serializer is CompiledSerializer;
#else
            return false;
#endif
        }


        volatile IProtoTypeSerializer _serializer;
        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal IProtoTypeSerializer Serializer
        {
            get
            {
                InitSerializers();
                return _serializer;
            }
        }

        volatile IProtoSerializerWithWireType _lateReferenceSerializer;
        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal IProtoSerializerWithWireType LateReferenceSerializer
        {
            get
            {
                InitSerializers();
                return _lateReferenceSerializer;
            }
        }


        volatile Exception _lateReferenceSerializerBuildException;

        Exception ILateReferenceSerializerProxy.LateReferenceSerializerBuildException => _lateReferenceSerializerBuildException;


        volatile IProtoTypeSerializer _rootSerializer;

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal IProtoTypeSerializer RootSerializer
        {
            get
            {
                InitSerializers();
                return _rootSerializer;
            }
        }
    }
}
#endif