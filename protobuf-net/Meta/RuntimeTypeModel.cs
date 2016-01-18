// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
#if !PORTABLE
using System.Runtime.Serialization;
#endif
using System.Text;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#endif
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
#if !FEAT_IKVM
using AqlaSerializer.Meta.Data;
#endif
using AqlaSerializer;
using AqlaSerializer.Serializers;
using System.Threading;
using System.IO;

namespace AqlaSerializer.Meta
{
#if !NO_GENERiCS
    using TypeSet = Dictionary<Type, object>;
    using TypeList = List<Type>;
#else
    using TypeSet = System.Collections.Hashtable;
    using TypeList = System.Collections.ArrayList;
#endif

    /// <summary>
    /// Provides protobuf serialization support for a number of types that can be defined at runtime
    /// </summary>
    public sealed partial class RuntimeTypeModel : TypeModel
    {
        public static bool CheckTypeCanBeAdded(RuntimeTypeModel model, Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (type.IsArray)
            {
                // byte arrays are handled internally
                if (Helpers.GetTypeCode(type.GetElementType()) == ProtoTypeCode.Byte) return false;
                return type.GetArrayRank() == 1;
            }
            return type != model.MapType(typeof(Enum))
                   && type != model.MapType(typeof(object))
                   && type != model.MapType(typeof(ValueType))
                   && (Helpers.GetNullableUnderlyingType(type) == null && (Helpers.IsEnum(type) || Helpers.GetTypeCode(type) == ProtoTypeCode.Unknown));
            //&& !MetaType.IsDictionaryOrListInterface(model, type);
        }
        
        internal static bool CheckTypeDoesntRequireAttributeFamily(RuntimeTypeModel model, Type type)
        {
            if (!CheckTypeCanBeAdded(model, type)) return false;
            Type defaultType = null;
            Type itemType = null;
            model.ResolveListTypes(type, ref itemType, ref defaultType);
            if (itemType == null) return false;
            if (model.AlwaysUseTypeRegistrationForCollections)
                return true;
            
            // in legacy mode list and array types are added but ONLY NESTED
            type = itemType;
            itemType = null;
            model.ResolveListTypes(type, ref itemType, ref defaultType);
            return itemType != null;
        }


        private short options;
        private const short
            OPTIONS_InferTagFromNameDefault = 1,
            OPTIONS_IsDefaultModel = 2,
            OPTIONS_Frozen = 4,
            OPTIONS_AutoAddMissingTypes = 8,
#if FEAT_COMPILER && !FX11
            OPTIONS_AutoCompile = 16,
#endif
            OPTIONS_UseImplicitZeroDefaults = 32,
            OPTIONS_AllowParseableTypes = 64,
            OPTIONS_IncludeDateTimeKind = 256;

        private bool GetOption(short option)
        {
            return (options & option) == option;
        }
        private void SetOption(short option, bool value)
        {
            if (value) options |= option;
            else options &= (short)~option;
        }
        /// <summary>
        /// Global default that
        /// enables/disables automatic tag generation based on the existing name / order
        /// of the defined members. See <seealso cref="ProtoBuf.ProtoContractAttribute.InferTagFromName"/>
        /// for usage and <b>important warning</b> / explanation.
        /// You must set the global default before attempting to serialize/deserialize any
        /// impacted type.
        /// </summary>
        public bool InferTagFromNameDefault
        {
            get { return GetOption(OPTIONS_InferTagFromNameDefault); }
            set { SetOption(OPTIONS_InferTagFromNameDefault, value); }
        }

        /// <summary>
        /// Global switch that enables or disables the implicit
        /// handling of "zero defaults"; meanning: if no other default is specified,
        /// it assumes bools always default to false, integers to zero, etc.
        /// 
        /// If this is disabled, no such assumptions are made and only *explicit*
        /// default values are processed. This is enabled by default to 
        /// preserve similar logic to v1 but disabled for the Create(newestBehavior: true) mode.
        /// </summary>
        public bool UseImplicitZeroDefaults
        {
            get { return GetOption(OPTIONS_UseImplicitZeroDefaults); }
            set
            {
                if (!value && GetOption(OPTIONS_IsDefaultModel))
                {
                    throw new InvalidOperationException("UseImplicitZeroDefaults cannot be disabled on the default model");
                }
                SetOption(OPTIONS_UseImplicitZeroDefaults, value);
            }
        }

        /// <summary>
        /// Global switch that determines whether types with a <c>.ToString()</c> and a <c>Parse(string)</c>
        /// should be serialized as strings.
        /// </summary>
        public bool AllowParseableTypes
        {
            get { return GetOption(OPTIONS_AllowParseableTypes); }
            set { SetOption(OPTIONS_AllowParseableTypes, value); }
        }

        IAutoAddStrategy _autoAddStrategy;
        public IAutoAddStrategy AutoAddStrategy
        {
            get { return _autoAddStrategy; }
            set
            {
                if (this == Default)
                    throw new InvalidOperationException("Not allowed on default " + this.GetType().Name + ", use TypeModel.Create()");
                if (value == null)
                    throw new ArgumentNullException("value");
                _autoAddStrategy = value;
            }
        }

        /// <summary>
        /// Global switch that determines whether DateTime serialization should include the <c>Kind</c> of the date/time.
        /// </summary>
        public bool IncludeDateTimeKind
        {
            get { return GetOption(OPTIONS_IncludeDateTimeKind); }
            set { SetOption(OPTIONS_IncludeDateTimeKind, value); }
        }

        /// <summary>
        /// Should the <c>Kind</c> be included on date/time values?
        /// </summary>
        protected internal override bool SerializeDateTimeKind()
        {
            return GetOption(OPTIONS_IncludeDateTimeKind);
        }
        

        private sealed class Singleton
        {
            private Singleton() { }
            internal static readonly RuntimeTypeModel Value = new RuntimeTypeModel(true);
        }
        /// <summary>
        /// The default model, used to support AqlaSerializer.Serializer
        /// </summary>
        public static RuntimeTypeModel Default
        {
            get { return Singleton.Value; }
        }

        public static DefaultAutoAddStrategy DefaultAutoAddStrategy { get { return (DefaultAutoAddStrategy) Default.AutoAddStrategy; } }

        /// <summary>
        /// Returns list of the MetaType instances that can be
        /// processed by this model.
        /// </summary>
        public MetaType[] MetaTypes
        {
            get
            {
                MetaType[] r = new MetaType[types.Count - _serviceTypesCount];
                types.CopyTo(r, _serviceTypesCount, 0, types.Count - _serviceTypesCount);
                return r;
            }
        }

        /// <summary>
        /// Returns list of the Type instances that can be
        /// processed by this model.
        /// </summary>
        public Type[] Types
        {
            get
            {
                Type[] r = new Type[types.Count - _serviceTypesCount];
                for (int i = 0, j = _serviceTypesCount; i < r.Length; i++, j++)
                {
                    r[i] = ((MetaType)types[j]).Type;
                }
                return r;
            }
        }

#if FEAT_COMPILER
#endif

        internal RuntimeTypeModel(bool isDefault)
        {
#if FEAT_COMPILER
#if FEAT_IKVM
            universe = new IKVM.Reflection.Universe();
            universe.EnableMissingMemberResolution(); // needed to avoid TypedReference issue on WinRT
            RunSharpTypeMapper = new TypeMapper(universe);
#else
            RunSharpTypeMapper = new TypeMapper();
#endif
#endif
            AutoAddMissingTypes = true;
            UseImplicitZeroDefaults = true;
            SetOption(OPTIONS_IsDefaultModel, isDefault);
#if FEAT_COMPILER && !FX11 && !DEBUG
            AutoCompile = true;
#endif
            _autoAddStrategy = new DefaultAutoAddStrategy(this);
#if !FEAT_IKVM
            Add(MapType(typeof(ModelTypeRelationsData)), true);
#endif
            _serviceTypesCount = types.Count;
        }

        readonly int _serviceTypesCount;


        /// <summary>
        /// Obtains the MetaType associated with a given Type for the current model,
        /// allowing additional configuration.
        /// </summary>
        public MetaType this[Type type] { get { return (MetaType)types[FindOrAddAuto(type, true, false, false)]; } }

        internal MetaType FindWithoutAdd(Type type)
        {
            // this list is thread-safe for reading
            foreach (MetaType metaType in types)
            {
                if (metaType.Type == type)
                {
                    if (metaType.Pending) WaitOnLock(metaType);
                    return metaType;
                }
            }
            // if that failed, check for a proxy
            Type underlyingType = ResolveProxies(type);
            return underlyingType == null ? null : FindWithoutAdd(underlyingType);
        }

        static readonly BasicList.MatchPredicate
            MetaTypeFinder = new BasicList.MatchPredicate(MetaTypeFinderImpl),
            BasicTypeFinder = new BasicList.MatchPredicate(BasicTypeFinderImpl);
        static bool MetaTypeFinderImpl(object value, object ctx)
        {
            return ((MetaType)value).Type == (Type)ctx;
        }
        static bool BasicTypeFinderImpl(object value, object ctx)
        {
            return ((BasicType)value).Type == (Type)ctx;
        }

        private void WaitOnLock(MetaType type)
        {
            int opaqueToken = 0;
            try
            {
                TakeLock(ref opaqueToken);
            }
            finally
            {
                ReleaseLock(opaqueToken);
            }
        }
        BasicList basicTypes = new BasicList();

        sealed class BasicType
        {
            private readonly Type type;
            public Type Type { get { return type; } }
            private readonly IProtoSerializer serializer;
            public IProtoSerializer Serializer { get { return serializer; } }
            public BasicType(Type type, IProtoSerializer serializer)
            {
                this.type = type;
                this.serializer = serializer;
            }
        }
        internal IProtoSerializer TryGetBasicTypeSerializer(Type type)
        {
            if (type.IsArray) return null;
            int idx = basicTypes.IndexOf(BasicTypeFinder, type);

            if (idx >= 0) return ((BasicType)basicTypes[idx]).Serializer;

            lock (basicTypes)
            { // don't need a full model lock for this

                // double-checked
                idx = basicTypes.IndexOf(BasicTypeFinder, type);
                if (idx >= 0) return ((BasicType)basicTypes[idx]).Serializer;

                WireType defaultWireType;
                MetaType.AttributeFamily family = _autoAddStrategy.GetContractFamily(type);
                IProtoSerializer ser = family == MetaType.AttributeFamily.None
                    ? ValueMember.TryGetCoreSerializer(this, BinaryDataFormat.Default, type, out defaultWireType, false, false, false, false)
                    : null;

                if (ser != null) basicTypes.Add(new BasicType(type, ser));
                return ser;
            }

        }

        internal int FindOrAddAuto(Type type, bool demand, bool addWithContractOnly, bool addEvenIfAutoDisabled)
        {
            MetaType metaType;
            return FindOrAddAuto(type, demand, addWithContractOnly, addEvenIfAutoDisabled, out metaType);
        }

        internal int FindOrAddAuto(Type type, bool demand, bool addWithContractOnly, bool addEvenIfAutoDisabled, out MetaType metaType)
        {
            metaType = null;
            int key = types.IndexOf(MetaTypeFinder, type);

            // the fast happy path: meta-types we've already seen
            if (key >= 0)
            {
                metaType = (MetaType)types[key];
                if (metaType.Pending)
                {
                    WaitOnLock(metaType);
                }
                return key;
            }

            // the fast fail path: types that will never have a meta-type
            bool shouldAdd = AutoAddMissingTypes || addEvenIfAutoDisabled;

            if (!Helpers.IsEnum(type) && TryGetBasicTypeSerializer(type) != null)
            {
                if (shouldAdd && !addWithContractOnly) throw MetaType.InbuiltType(type);
                return -1; // this will never be a meta-type
            }

            // otherwise: we don't yet know

            // check for proxy types
            Type underlyingType = ResolveProxies(type);
            if (underlyingType != null)
            {
                key = types.IndexOf(MetaTypeFinder, underlyingType);
                type = underlyingType; // if new added, make it reflect the underlying type
            }

            if (!CheckTypeCanBeAdded(this, type))
            {
                if (demand)
                    throw new InvalidOperationException("Type " + type.Name + " can't be added to model (has inbuilt behavior or not supported)");
                return -1;
            }

            if (key < 0)
            {
                int opaqueToken = 0;
                try
                {
                    TakeLock(ref opaqueToken);
                    // try to recognise a few familiar patterns...
                    if ((metaType = RecogniseCommonTypes(type)) == null)
                    { // otherwise, check if it is a contract
                        MetaType.AttributeFamily family = _autoAddStrategy.GetContractFamily(type);
                        if (family == MetaType.AttributeFamily.AutoTuple)
                        {
                            shouldAdd = addEvenIfAutoDisabled = true; // always add basic tuples, such as KeyValuePair
                        }
                        if (!shouldAdd || (
                            !Helpers.IsEnum(type) && addWithContractOnly && family == MetaType.AttributeFamily.None && !CheckTypeDoesntRequireAttributeFamily(this,type)))
                        {
                            if (demand) ThrowUnexpectedType(type);
                            return key;
                        }
                        metaType = Create(type);
                    }
                    metaType.Pending = true;
                    bool weAdded = false;

                    // double-checked
                    int winner = types.IndexOf(MetaTypeFinder, type);
                    if (winner < 0)
                    {
                        ThrowIfFrozen();
                        key = types.Add(metaType);
                        weAdded = true;
                    }
                    else
                    {
                        key = winner;
                    }
                    if (weAdded)
                    {
                        metaType.ApplyDefaultBehaviour();
                        metaType.Pending = false;
                    }
                    else
                    {
                        metaType = (MetaType)types[winner];
                    }
                }
                finally
                {
                    ReleaseLock(opaqueToken);
                }
                AddDependentSubTypes(metaType);
            }
            return key;
        }

        void AddDependentSubTypes(MetaType baseType)
        {
            //// e.g. IDictionary<MyKey, MyValue> - specific generic type
            //Type defaultType;
            //if (MetaType.IsDictionaryOrListInterface(this, baseType.Type, out defaultType))
            //{
            //    //baseType.AddSubType()
            //}
        }

        private MetaType RecogniseCommonTypes(Type type)
        {
            //#if !NO_GENERICS
            //            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.KeyValuePair<,>))
            //            {
            //                MetaType mt = new MetaType(this, type);

            //                Type surrogate = typeof (KeyValuePairSurrogate<,>).MakeGenericType(type.GetGenericArguments());

            //                mt.SetSurrogate(surrogate);
            //                mt.IncludeSerializerMethod = false;
            //                mt.Freeze();

            //                MetaType surrogateMeta = (MetaType)types[FindOrAddAuto(surrogate, true, true, true)]; // this forcibly adds it if needed
            //                if(surrogateMeta.IncludeSerializerMethod)
            //                { // don't blindly set - it might be frozen
            //                    surrogateMeta.IncludeSerializerMethod = false;
            //                }
            //                surrogateMeta.Freeze();
            //                return mt;
            //            }
            //#endif
            return null;
        }
        private MetaType Create(Type type)
        {
            ThrowIfFrozen();
            return new MetaType(this, type, defaultFactory);
        }

        /// <summary>
        /// See <see cref="MetaType.AsReferenceDefault"/>
        /// </summary>
        public bool AddNotAsReferenceDefault { get; set; }

        /// <summary>
        /// If enabled all Arrays and Lists will be handled in extended mode to always support reference tracking and null (which will break without runtime/pre-compiled dll) but may have a size overhead (going to fix that in later releases)
        /// </summary>
        public bool AlwaysUseTypeRegistrationForCollections { get; set; }

        /// <summary>
        /// Adds all types inside the specified assembly
        /// </summary>
        public void Add(Assembly assembly, bool recognizableOnly, bool nonPublic, bool applyDefaultBehavior)
        {
            Type[] list = nonPublic ? Helpers.GetTypes(assembly) : Helpers.GetExportedTypes(assembly);
            
            // types order actually does not matter
            // but subtypes does
            Array.Sort(list, new TypeNamesSortComparer());


            foreach (Type t in list)
            {
                if (!Helpers.IsGenericTypeDefinition(t)
                    && (!recognizableOnly || _autoAddStrategy.GetContractFamily(t) != MetaType.AttributeFamily.None))
                    Add(t, applyDefaultBehavior);
            }
        }

        class TypeNamesSortComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                var tX = ((Type)x);
                var tY = ((Type)y);
                return StringComparer.Ordinal.Compare(ExtractName(tX), ExtractName(tY));
            }

            private static string ExtractName(Type tX)
            {
                return tX.FullName + ", from " + Helpers.GetAssembly(tX).FullName;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <param name="applyDefaultBehaviorIfNew"></param>
        public void Add(Type[] list, bool applyDefaultBehaviorIfNew)
        {
            foreach (Type t in list)
            {
                if (Helpers.IsGenericTypeDefinition(t))
                    throw new ArgumentException("Should not be GenericTypeDefinition");
            }

            foreach (Type t in list)
            {
                Add(t, applyDefaultBehaviorIfNew);
            }
        }

#if !FEAT_IKVM
        /// <summary>
        /// Use this when you need to recreate RuntimeTypeModel with the same subtype keys on another side (if you don't use precompilation then your models should be initialized in this way), see also <see cref="ExportTypeRelations"/>. Fields are not imported!
        /// </summary>
        /// <remarks>
        /// On 1st side (possible network server):
        /// Serialize(stream, model.ExportTypeRelations());
        /// On 2nd side (possible network client):
        /// model.ImportTypeRelations((ModelTypeRelationsData)Deserialize(...))
        /// </remarks>
        /// <param name="data"></param>
        /// <param name="forceDefaultBehavior"></param>
        public void ImportTypeRelations(ModelTypeRelationsData data, bool forceDefaultBehavior)
        {
            int lockToken = 0;
            try
            {
                TakeLock(ref lockToken);
                if (data.Types != null)
                {
                    foreach (var t in data.Types)
                    {
                        Add(t.Type, false);
                    }
                    
                    foreach (var t in data.Types)
                    {
                        ImportTypeRelations(t);
                    }
                }

                if (forceDefaultBehavior)
                {
                    foreach (var t in data.Types)
                    {
                        FindWithoutAdd(t.Type).ApplyDefaultBehaviour();
                    }
                }
            }
            finally
            {
                ReleaseLock(lockToken);
            }
        }

        public MetaType ImportTypeRelations(TypeData data)
        {
            int lockToken = 0;
            try
            {
                TakeLock(ref lockToken);
                var t = Add(data.Type, false);
                if (data.Subtypes != null)
                    foreach (var subType in data.Subtypes)
                    {
                        Add(subType.Type, false);
                        t.AddSubType(subType.FieldNumber, subType.Type, subType.DataFormat);
                    }
                return t;
            }
            finally
            {
                ReleaseLock(lockToken);
            }
        }

        /// <summary>
        /// Exports all types list with registered subtypes, can be used to recreate the same model but without fields mapping
        /// </summary>
        public ModelTypeRelationsData ExportTypeRelations()
        {
            int lockToken = 0;
            try
            {
                TakeLock(ref lockToken);
                var m = new ModelTypeRelationsData();
                var metaTypes = MetaTypes;
                m.Types = new TypeData[metaTypes.Length];
                for (int i = 0; i < metaTypes.Length; i++)
                {
                    MetaType metaType = metaTypes[i];
                    var data = new TypeData() { Type = metaType.Type };

                    SubType[] metaSubTypes = metaType.GetSubtypes();
                    data.Subtypes = new SubtypeData[metaSubTypes.Length];

                    for (int j = 0; j < metaSubTypes.Length; j++)
                    {
                        var metaSubType = metaSubTypes[j];

                        data.Subtypes[j] = new SubtypeData()
                        {
                            DataFormat = metaSubType.DataFormat,
                            FieldNumber = metaSubType.FieldNumber,
                            Type = metaSubType.DerivedType.Type
                        };
                    }
                    m.Types[i] = data;
                }
                return m;
            }
            finally
            {
                ReleaseLock(lockToken);
            }
        }
#endif

        /// <summary>
        /// Adds support for an additional type in this model, optionally
        /// applying inbuilt patterns. If the type is already known to the
        /// model, the existing type is returned **without** applying
        /// any additional behaviour.
        /// </summary>
        /// <remarks>Inbuilt patterns include:
        /// [ProtoContract]/[ProtoMember(n)]
        /// [DataContract]/[DataMember(Order=n)]
        /// [XmlType]/[XmlElement(Order=n)]
        /// [On{Des|S}erializ{ing|ed}]
        /// ShouldSerialize*/*Specified
        /// </remarks>
        /// <param name="type">The type to be supported</param>
        /// <param name="applyDefaultBehaviourIfNew">Whether to apply the inbuilt configuration patterns (via attributes etc), or
        /// just add the type with no additional configuration (the type must then be manually configured).</param>
        /// <returns>The MetaType representing this type, allowing
        /// further configuration.</returns>
        public MetaType Add(Type type, bool applyDefaultBehaviourIfNew)
        {
            if (type == null) throw new ArgumentNullException("type");
            MetaType newType = FindWithoutAdd(type);
            if (newType != null) return newType; // return existing
            int opaqueToken = 0;

#if WINRT
            System.Reflection.TypeInfo typeInfo = System.Reflection.IntrospectionExtensions.GetTypeInfo(type);
            if (typeInfo.IsInterface && MetaType.ienumerable.IsAssignableFrom(typeInfo)
#else
            if (type.IsInterface && MapType(MetaType.ienumerable).IsAssignableFrom(type)
#endif
 && GetListItemType(this, type) == null)
            {
                throw new ArgumentException("IEnumerable[<T>] data cannot be used as a meta-type unless an Add method can be resolved");
            }
            try
            {
                newType = RecogniseCommonTypes(type);
                if (newType != null)
                {
                    if (!applyDefaultBehaviourIfNew)
                    {
                        throw new ArgumentException(
                            "Default behaviour must be observed for certain types with special handling; " + type.FullName,
                            "applyDefaultBehaviourIfNew");
                    }
                    // we should assume that type is fully configured, though; no need to re-run:
                    applyDefaultBehaviourIfNew = false;
                }
                if (newType == null) newType = Create(type);
                newType.Pending = true;
                TakeLock(ref opaqueToken);
                // double checked
                if (FindWithoutAdd(type) != null) throw new ArgumentException("Duplicate type", "type");
                ThrowIfFrozen();
                types.Add(newType);
                if (applyDefaultBehaviourIfNew) { newType.ApplyDefaultBehaviour(); }
                newType.Pending = false;
            }
            finally
            {
                ReleaseLock(opaqueToken);
            }
            AddDependentSubTypes(newType);
            return newType;
        }

#if FEAT_COMPILER && !FX11
        /// <summary>
        /// Should serializers be compiled on demand? It may be useful
        /// to disable this for debugging purposes.
        /// </summary>
        public bool AutoCompile
        {
            get { return GetOption(OPTIONS_AutoCompile); }
            set { SetOption(OPTIONS_AutoCompile, value); }
        }
#endif
        /// <summary>
        /// Should support for unexpected types be added automatically?
        /// If false, an exception is thrown when unexpected types
        /// are encountered.
        /// </summary>
        public bool AutoAddMissingTypes
        {
            get { return GetOption(OPTIONS_AutoAddMissingTypes); }
            set
            {
                if (!value && GetOption(OPTIONS_IsDefaultModel))
                {
                    throw new InvalidOperationException("The default model must allow missing types");
                }
                ThrowIfFrozen();
                SetOption(OPTIONS_AutoAddMissingTypes, value);
            }
        }
        /// <summary>
        /// Verifies that the model is still open to changes; if not, an exception is thrown
        /// </summary>
        private void ThrowIfFrozen()
        {
            if (GetOption(OPTIONS_Frozen)) throw new InvalidOperationException("The model cannot be changed once frozen");
        }
        /// <summary>
        /// Prevents further changes to this model
        /// </summary>
        public void Freeze()
        {
            if (GetOption(OPTIONS_IsDefaultModel)) throw new InvalidOperationException("The default model cannot be frozen");
            SetOption(OPTIONS_Frozen, true);
        }

        private readonly BasicList types = new BasicList();

        /// <summary>
        /// Provides the key that represents a given type in the current model.
        /// </summary>
        protected override int GetKeyImpl(Type type)
        {
            return GetKey(type, false, true);
        }
        internal int GetKey(Type type, bool demand, bool getBaseKey)
        {
            Helpers.DebugAssert(type != null);
            try
            {
                int typeIndex = FindOrAddAuto(type, demand, true, false);
                if (typeIndex >= 0)
                {
                    MetaType mt = (MetaType)types[typeIndex];
                    if (getBaseKey)
                    {
                        mt = MetaType.GetRootType(mt);
                        typeIndex = FindOrAddAuto(mt.Type, true, true, false);
                    }
                }
                return typeIndex;
            }
            catch (NotSupportedException)
            {
                throw; // re-surface "as-is"
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf(type.FullName, System.StringComparison.Ordinal) >= 0) throw;  // already enough info
                throw new ProtoException(ex.Message + " (" + type.FullName + ")", ex);
            }
        }
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream.
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination stream to write to.</param>
        protected internal override void Serialize(int key, object value, ProtoWriter dest, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            //Helpers.DebugWriteLine("Serialize", value);
            var metaType = ((MetaType)types[key]);
            var ser = isRoot ? metaType.RootSerializer : metaType.Serializer;
            ser.Write(value, dest);
#endif
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        protected internal override object Deserialize(int key, object value, ProtoReader source, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            //Helpers.DebugWriteLine("Deserialize", value);
            var metaType = ((MetaType)types[key]);
            var ser = isRoot ? metaType.RootSerializer : metaType.Serializer;

            if (value == null && Helpers.IsValueType(ser.ExpectedType))
            {
                if (ser.RequiresOldValue) value = CreateInstance(ser, source);
                return ser.Read(value, source);
            }
            else
            {
                return ser.Read(value, source);
            }
#endif
        }

#if !FEAT_IKVM
        private object CreateInstance(IProtoSerializer ser, ProtoReader source)
        {
            var obj = Activator.CreateInstance(ser.ExpectedType);
            ProtoReader.NoteObject(obj, source);
            return obj;
        }
#endif


#if FEAT_COMPILER
        // this is used by some unit-tests; do not remove
        internal Compiler.ProtoSerializer GetSerializer(IProtoSerializer serializer, bool compiled)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (serializer == null) throw new ArgumentNullException("serializer");
#if FEAT_COMPILER && !FX11
            if (compiled) return Compiler.CompilerContext.BuildSerializer(serializer, this);
#endif
            return new Compiler.ProtoSerializer(serializer.Write);
#endif
        }

#if !FX11
        /// <summary>
        /// Compiles the serializers individually; this is *not* a full
        /// standalone compile, but can significantly boost performance
        /// while allowing additional types to be added.
        /// </summary>
        /// <remarks>An in-place compile can access non-public types / members</remarks>
        public void CompileInPlace()
        {
            foreach (MetaType type in types)
            {
                type.CompileInPlace();
            }
        }
#endif
#endif
        //internal override IProtoSerializer GetTypeSerializer(Type type)
        //{   // this list is thread-safe for reading
        //    .Serializer;
        //}
        //internal override IProtoSerializer GetTypeSerializer(int key)
        //{   // this list is thread-safe for reading
        //    MetaType type = (MetaType)types.TryGet(key);
        //    if (type != null) return type.Serializer;
        //    throw new KeyNotFoundException();

        //}
        
        //internal bool IsDefined(Type type, int fieldNumber)
        //{
        //    return FindWithoutAdd(type).IsDefined(fieldNumber);
        //}

        // note that this is used by some of the unit tests
        internal bool IsPrepared(Type type)
        {
            MetaType meta = FindWithoutAdd(type);
            return meta != null && meta.IsPrepared();
        }

        internal EnumSerializer.EnumPair[] GetEnumMap(Type type)
        {
            int index = FindOrAddAuto(type, false, false, false);
            return index < 0 ? null : ((MetaType)types[index]).GetEnumMap();
        }

        private int metadataTimeoutMilliseconds = 5000;
        /// <summary>
        /// The amount of time to wait if there are concurrent metadata access operations
        /// </summary>
        public int MetadataTimeoutMilliseconds
        {
            get { return metadataTimeoutMilliseconds; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException("MetadataTimeoutMilliseconds");
                metadataTimeoutMilliseconds = value;
            }
        }

#if DEBUG
        int lockCount;
        /// <summary>
        /// Gets how many times a model lock was taken
        /// </summary>
        public int LockCount { get { return lockCount; } }
#endif
        internal void TakeLock(ref int opaqueToken)
        {
            const string message = "Timeout while inspecting metadata; this may indicate a deadlock. This can often be avoided by preparing necessary serializers during application initialization, rather than allowing multiple threads to perform the initial metadata inspection; please also see the LockContended event";
            opaqueToken = 0;
#if PORTABLE
            if(!Monitor.TryEnter(types, metadataTimeoutMilliseconds)) throw new TimeoutException(message);
            opaqueToken = Interlocked.CompareExchange(ref contentionCounter, 0, 0); // just fetch current value (starts at 1)
#elif CF2 || CF35
            int remaining = metadataTimeoutMilliseconds;
            bool lockTaken;
            do {
                lockTaken = Monitor.TryEnter(types);
                if(!lockTaken)
                {
                    if(remaining <= 0) throw new TimeoutException(message);
                    remaining -= 50;
                    Thread.Sleep(50);
                }
            } while(!lockTaken);
            opaqueToken = Interlocked.CompareExchange(ref contentionCounter, 0, 0); // just fetch current value (starts at 1)
#else
            if (Monitor.TryEnter(types, metadataTimeoutMilliseconds))
            {
                opaqueToken = GetContention(); // just fetch current value (starts at 1)
            }
            else
            {
                AddContention();
#if FX11
                throw new InvalidOperationException(message);
#else
                throw new TimeoutException(message);
#endif
            }
#endif

#if DEBUG // note that here, through all code-paths: we have the lock
            lockCount++;
#endif
        }

        private int contentionCounter = 1;
#if PLAT_NO_INTERLOCKED
        private readonly object contentionLock = new object();
#endif
        private int GetContention()
        {
#if PLAT_NO_INTERLOCKED
            lock(contentionLock)
            {
                return contentionCounter;
            }
#else
            return Interlocked.CompareExchange(ref contentionCounter, 0, 0);
#endif
        }
        private void AddContention()
        {
#if PLAT_NO_INTERLOCKED
            lock(contentionLock)
            {
                contentionCounter++;
            }
#else
            Interlocked.Increment(ref contentionCounter);
#endif
        }

        internal void ReleaseLock(int opaqueToken)
        {
            if (opaqueToken != 0)
            {
                Monitor.Exit(types);
                if (opaqueToken != GetContention()) // contention-count changes since we looked!
                {
                    LockContentedEventHandler handler = LockContended;
                    if (handler != null)
                    {
                        // not hugely elegant, but this is such a far-corner-case that it doesn't need to be slick - I'll settle for cross-platform
                        string stackTrace;
                        try
                        {
                            throw new ProtoException();
                        }
                        catch (Exception ex)
                        {
                            stackTrace = ex.StackTrace;
                        }

                        handler(this, new LockContentedEventArgs(stackTrace));
                    }
                }
            }
        }
        /// <summary>
        /// If a lock-contention is detected, this event signals the *owner* of the lock responsible for the blockage, indicating
        /// what caused the problem; this is only raised if the lock-owning code successfully completes.
        /// </summary>
        public event LockContentedEventHandler LockContended;

        internal void ResolveListTypes(Type type, ref Type itemType, ref Type defaultType)
        {
            MetaType.ResolveListTypes(this, type, ref itemType, ref defaultType);
        }


#if FEAT_IKVM
        internal override Type GetType(string fullName, Assembly context)
        {
            if (context != null)
            {
                Type found = universe.GetType(context, fullName, false);
                if (found != null) return found;
            }
            return universe.GetType(fullName, false);
        }
#endif
        
        /// <summary>
        /// Designate a factory-method to use to create instances of any type; note that this only affect types seen by the serializer *after* setting the factory.
        /// </summary>
        public void SetDefaultFactory(MethodInfo methodInfo)
        {
            VerifyFactory(methodInfo, null);
            defaultFactory = methodInfo;
        }
        private MethodInfo defaultFactory;

        internal void VerifyFactory(MethodInfo factory, Type type)
        {
            if (factory != null)
            {
                if (type != null && Helpers.IsValueType(type)) throw new InvalidOperationException();
                if (!factory.IsStatic) throw new ArgumentException("A factory-method must be static", "factory");
                if ((type != null && factory.ReturnType != type) && factory.ReturnType != MapType(typeof(object))) throw new ArgumentException("The factory-method must return object" + (type == null ? "" : (" or " + type.FullName)), "factory");

                if (!CallbackSet.CheckCallbackParameters(this, factory)) throw new ArgumentException("Invalid factory signature in " + factory.DeclaringType.FullName + "." + factory.Name, "factory");
            }
        }

    }
    /// <summary>
    /// Contains the stack-trace of the owning code when a lock-contention scenario is detected
    /// </summary>
    public sealed class LockContentedEventArgs : EventArgs
    {
        private readonly string ownerStackTrace;
        internal LockContentedEventArgs(string ownerStackTrace)
        {
            this.ownerStackTrace = ownerStackTrace;
        }
        /// <summary>
        /// The stack-trace of the code that owned the lock when a lock-contention scenario occurred
        /// </summary>
        public string OwnerStackTrace { get { return ownerStackTrace; } }
    }
    /// <summary>
    /// Event-type that is raised when a lock-contention scenario is detected
    /// </summary>
    public delegate void LockContentedEventHandler(object sender, LockContentedEventArgs args);


}
#endif
