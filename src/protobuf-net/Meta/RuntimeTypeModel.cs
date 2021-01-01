using ProtoBuf.Compiler;
using ProtoBuf.Internal;
using ProtoBuf.Internal.Serializers;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
#if !PORTABLE
using System.Runtime.Serialization;
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
using ProtoBuf.WellKnownTypes;
#endif
using AqlaSerializer;
using AqlaSerializer.Serializers;
using System.IO;
using AltLinq; using System.Linq;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Diagnostics;
using System.Reflection.Emit;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#else
using System.Reflection;
#endif
using System.Text;
using System.Threading;

#pragma warning disable IDE0079 // sorry IDE, you're wrong

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
    /// <summary>
    /// Ensures that RuntimeTypeModel has been initialized, in advance of using methods on <see cref="Serializer"/>.
    /// </summary>
    public static void Initialize() => _ = Default;
        public ProtoCompatibilitySettingsValue ProtoCompatibility { get; private set; }
        
        internal bool IsFrozen => GetOption(OPTIONS_Frozen);
        internal IValueSerializerBuilder ValueSerializerBuilder { get; private set; }
        
        public static bool CheckTypeCanBeAdded(RuntimeTypeModel model, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (Helpers.IsGenericTypeDefinition(type)) return false;
            if (type.IsArray)
            {
                // byte arrays are handled internally
                if (Helpers.GetTypeCode(type.GetElementType()) == ProtoTypeCode.Byte) return false;
                return true;
            }

            var underlying = Helpers.GetNullableUnderlyingType(type);
            if (underlying != null)
                return CheckTypeCanBeAdded(model, underlying);

            return type != model.MapType(typeof(Enum))
                   && type != model.MapType(typeof(object))
                   && type != model.MapType(typeof(ValueType))
                   && (type.IsEnum || Helpers.GetTypeCode(type) == ProtoTypeCode.Unknown)
                   && !model.IsInbuiltType(type);
            //&& !MetaType.IsDictionaryOrListInterface(model, type);
        }
        
        /// <summary>
        /// Some types which can't be handled by Auxiliary should be always registered even without a contract
        /// </summary>
        internal static bool CheckTypeDoesntRequireContract(RuntimeTypeModel model, Type type)
        {
            if (!CheckTypeCanBeAdded(model, type)) return false;
            if (type.IsArray && type.GetArrayRank() != 1) return true;

            Type defaultType = null;
            Type itemType = null;
            model.ResolveListTypes(type, ref itemType, ref defaultType);
            // ArrayList can't be added without contract
            // because it's item type can't be serialized normally
            // should be dynamic?
            if (itemType == null || itemType == model.MapType(typeof(object))) return false;
            if (model.AlwaysUseTypeRegistrationForCollections)
                return true;

            // in legacy mode list and array types are added but ONLY NESTED
            return CheckTypeIsCollection(model, itemType);
        }

        internal static bool CheckTypeIsCollection(RuntimeTypeModel model, Type type)
        {
            Type defaultType = null;
            Type itemType = null;
            model.ResolveListTypes(type, ref itemType, ref defaultType);
            return itemType != null;
        }

        internal static bool CheckTypeIsNestedCollection(RuntimeTypeModel model, Type type)
        {
            Type defaultType = null;
            Type itemType = null;
            model.ResolveListTypes(type, ref itemType, ref defaultType);
            if (itemType == null) return false;
            return CheckTypeIsCollection(model, type);
        }


        private short _options;
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

    private enum RuntimeTypeModelOptions
    {
        None = 0,
        InternStrings = TypeModelOptions.InternStrings,
        IncludeDateTimeKind = TypeModelOptions.IncludeDateTimeKind,
        SkipZeroLengthPackedArrays = TypeModelOptions.SkipZeroLengthPackedArrays,
        AllowPackedEncodingAtRoot = TypeModelOptions.AllowPackedEncodingAtRoot,

        TypeModelMask = InternStrings | IncludeDateTimeKind | SkipZeroLengthPackedArrays | AllowPackedEncodingAtRoot,

        // stuff specific to RuntimeTypeModel
        InferTagFromNameDefault = 1 << 10,
        IsDefaultModel = 1 << 11,
        Frozen = 1 << 12,
        AutoAddMissingTypes = 1 << 13,
        AutoCompile = 1 << 14,
        UseImplicitZeroDefaults = 1 << 15,
        AllowParseableTypes = 1 << 16,
        AutoAddProtoContractTypesOnly = 1 << 17,
    }

    /// <summary>
    /// Specifies optional behaviors associated with this model
    /// </summary>
    public override TypeModelOptions Options => (TypeModelOptions)(_options & RuntimeTypeModelOptions.TypeModelMask);
           OPTIONS_InternStrings = 512;

    internal CompilerContextScope Scope { get; } = CompilerContextScope.CreateInProcess();

        private bool GetOption(short option)
        {
            return (_options & option) == option;
        }
        private void SetOption(short option, bool value)
        {
            if (value) _options |= option;
            else _options &= (short)~option;
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
            get { return GetOption(RuntimeTypeModelOptions.InferTagFromNameDefault); }
            set { SetOption(RuntimeTypeModelOptions.InferTagFromNameDefault, value); }
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
            get { return GetOption(RuntimeTypeModelOptions.UseImplicitZeroDefaults); }
            set
            {
                if (!value && GetOption(RuntimeTypeModelOptions.IsDefaultModel))
                    ThrowDefaultUseImplicitZeroDefaults();
                SetOption(RuntimeTypeModelOptions.UseImplicitZeroDefaults, value);
            }
        }

        /// <summary>
        /// Global switch that determines whether types with a <c>.ToString()</c> and a <c>Parse(string)</c>
        /// should be serialized as strings.
        /// </summary>
        public bool AllowParseableTypes
        {
            get { return GetOption(RuntimeTypeModelOptions.AllowParseableTypes); }
            set { SetOption(RuntimeTypeModelOptions.AllowParseableTypes, value); }
        }

        /// <summary>
        /// See <see cref="SerializableMemberAttributeBase.ModelId"/>
        /// </summary>
        public object ModelId { get; set; }

        public void SetEnumFlagModelId<T>(T value) where T : struct
        {
            ModelId = new EnumFlagModelId<T>(value);
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
                    throw new ArgumentNullException(nameof(value));
                _autoAddStrategy = value;
            }
        }

        /// <summary>
        /// Global switch that determines whether DateTime serialization should include the <c>Kind</c> of the date/time.
        /// </summary>
        public bool IncludeDateTimeKind
        {
            get { return GetOption(RuntimeTypeModelOptions.IncludeDateTimeKind); }
            set { SetOption(RuntimeTypeModelOptions.IncludeDateTimeKind, value); }
        }

    /// <summary>
    /// Global switch that determines whether a single instance of the same string should be used during deserialization.
    /// </summary>
    /// <remarks>Note this does not use the global .NET string interner</remarks>
    public bool InternStrings
    {
        get { return GetOption(RuntimeTypeModelOptions.InternStrings); }
        set { SetOption(RuntimeTypeModelOptions.InternStrings, value); }
    }
        

        private sealed class Singleton
        {
            private Singleton() { }
            internal static readonly RuntimeTypeModel Value = new RuntimeTypeModel(true, ProtoCompatibilitySettingsValue.Default);
        }
        /// <summary>
        /// The default model, used to support AqlaSerializer.Serializer
        /// </summary>
        public static RuntimeTypeModel Default
            => (DefaultModel as RuntimeTypeModel) ?? CreateDefaultModelInstance();

        private sealed class SingletonCompatible
        {
            private SingletonCompatible() { }
            internal static readonly RuntimeTypeModel Value = new RuntimeTypeModel(true, ProtoCompatibilitySettingsValue.FullCompatibility);
        }
        /// <summary>
        /// The default model, used to support AqlaSerializer.Serializer
        /// </summary>
        public static RuntimeTypeModel ProtoCompatible => SingletonCompatible.Value;

        public static AutoAddStrategy DefaultAutoAddStrategy => (AutoAddStrategy) Default.AutoAddStrategy;

    /// <summary>
    /// Should zero-length packed arrays be serialized? (this is the v2 behavior, but skipping them is more efficient)
    /// </summary>
    public bool SkipZeroLengthPackedArrays
    {
        get { return GetOption(RuntimeTypeModelOptions.SkipZeroLengthPackedArrays); }
        set { SetOption(RuntimeTypeModelOptions.SkipZeroLengthPackedArrays, value); }
    }

        /// <summary>
        /// Returns list of the MetaType instances that can be
        /// processed by this model.
        /// </summary>
        public MetaType[] MetaTypes
        {
            get
            {
                MetaType[] r = new MetaType[_types.Count - _serviceTypesCount];
                _types.CopyTo(r, _serviceTypesCount, 0, _types.Count - _serviceTypesCount);
                return r;
            }
        }

    /// <summary>
    /// Should root-values allow "packed" encoding? (v2 does not support this)
    /// </summary>
    public bool AllowPackedEncodingAtRoot
    {
        get { return GetOption(RuntimeTypeModelOptions.AllowPackedEncodingAtRoot); }
        set { SetOption(RuntimeTypeModelOptions.AllowPackedEncodingAtRoot, value); }
    }

    /// <summary>
    /// Gets or sets the default <see cref="CompatibilityLevel"/> for this model.
    /// </summary>
    public CompatibilityLevel DefaultCompatibilityLevel
    {
        get => _defaultCompatibilityLevel;
        set
        {
            if (value != _defaultCompatibilityLevel)
            {
                CompatibilityLevelAttribute.AssertValid(value);
                ThrowIfFrozen();
                if (GetOption(RuntimeTypeModelOptions.IsDefaultModel)) ThrowHelper.ThrowInvalidOperationException("The default compatibility level of the default model cannot be changed");
                if (types.Any()) ThrowHelper.ThrowInvalidOperationException("The default compatibility level of cannot be changed once types have been added");
                _defaultCompatibilityLevel = value;
            }
        }
    }

    private CompatibilityLevel _defaultCompatibilityLevel = CompatibilityLevel.Level200;

        /// <summary>
        /// Returns list of the Type instances that can be
        /// processed by this model.
        /// </summary>
        public Type[] Types
        {
            get
            {
                Type[] r = new Type[_types.Count - _serviceTypesCount];
                for (int i = 0, j = _serviceTypesCount; i < r.Length; i++, j++)
                {
                    r[i] = ((MetaType)_types[j]).Type;
                }
                return r;
            }
        }
        
        internal RuntimeTypeModel(bool isDefault, ProtoCompatibilitySettingsValue protoCompatibility)
        {
            ProtoCompatibility = protoCompatibility.Clone();
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
            _autoAddStrategy = new AutoAddStrategy(this);
            ValueSerializerBuilder = new ValueSerializerBuilder(this);
#if !FEAT_IKVM
            Add(MapType(typeof(ModelTypeRelationsData)), true);
#endif
            _serviceTypesCount = _types.Count;
            IsInitialized = true;
        }

        internal bool IsInitialized { get; }

    private void CascadeRepeated(List<MetaType> list, RepeatedSerializerStub provider, CompatibilityLevel ambient, DataFormat keyFormat, HashSet<string> imports, string origin)
    {
        if (provider.IsMap)
        {
            provider.ResolveMapTypes(out var key, out var value);
            TryGetCoreSerializer(list, key, ambient, imports, origin);
            TryGetCoreSerializer(list, value, ambient, imports, origin);

            if (!provider.IsValidProtobufMap(this, ambient, keyFormat)) // add the KVP
                TryGetCoreSerializer(list, provider.ItemType, ambient, imports, origin);
        }
        else
        {
            TryGetCoreSerializer(list, provider.ItemType, ambient, imports, origin);
        }
    }
	    [Flags]
	    internal enum CommonImports
	    {
	        None = 0,
	        Bcl = 1,
	        Timestamp = 2,
	        Duration = 4,
	        Protogen = 8
	    }
        readonly int _serviceTypesCount;


        /// <summary>
        /// Obtains the MetaType associated with a given Type for the current model,
        /// allowing additional configuration.
        /// </summary>
        public MetaType this[Type type] => (MetaType)_types[FindOrAddAuto(type, true, false, false)];

	    private void TryGetCoreSerializer(BasicList list, Type itemType)
	    {
	        var coreSerializer = ValueMember.TryGetCoreSerializer(this, BinaryDataFormat.Default, itemType, out _, false, false, false, false);
	        if (coreSerializer != null)
	        {
	            return;
	        }
	        int index = FindOrAddAuto(itemType, false, false, false);
	        if (index < 0)
	        {
	            return;
	        }
	        var temp = ((MetaType)types[index]).GetSurrogateOrBaseOrSelf(false);
	        if (list.Contains(temp))
	        {
	            return;
	        }
	        // could perhaps also implement as a queue, but this should work OK for sane models
	        list.Add(temp);
	        CascadeDependents(list, temp);
	    }

        internal MetaType this[int key] => (MetaType)_types[key];

        internal MetaType FindWithoutAdd(Type type)
        {
            // this list is thread-safe for reading
            foreach (MetaType metaType in _types)
            {
                if (metaType.Type == type)
                {
                    if (metaType.IsPending) WaitOnLock(metaType);
                    return metaType;
                }
            }
            // if that failed, check for a proxy
            Type underlyingType = ResolveProxies(type);
            return underlyingType == null ? null : FindWithoutAdd(underlyingType);
        }

        static readonly BasicList.MatchPredicate
            MetaTypeFinder = MetaTypeFinderImpl,
            BasicTypeFinder = BasicTypeFinderImpl;

        static bool MetaTypeFinderImpl(object value, object ctx)
        {
            return ((MetaType)value).Type == (Type)ctx;
        }

        static bool BasicTypeFinderImpl(object value, object ctx)
        {
            return ((BasicType)value).Type == (Type)ctx;
        }

        private void WaitOnLock()
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
        BasicList _basicTypes = new BasicList();

        sealed class BasicType
        {
            public Type Type { get; }

            public IRuntimeProtoSerializerNode Serializer { get; }

            public BasicType(Type type, IRuntimeProtoSerializerNode serializer)
            {
                this.Type = type;
                this.Serializer = serializer;
            }
        }
        internal IProtoSerializer TryGetBasicTypeSerializer(Type type)
        {
            if (type.IsArray) return null;
            int idx = _basicTypes.IndexOf(BasicTypeFinder, type);

            if (idx >= 0) return ((BasicType)_basicTypes[idx]).Serializer;

            lock (_basicTypes)
            { // don't need a full model lock for this

                // double-checked
                idx = _basicTypes.IndexOf(BasicTypeFinder, type);
                if (idx >= 0) return ((BasicType)_basicTypes[idx]).Serializer;

                WireType defaultWireType;
                MetaType.AttributeFamily family = _autoAddStrategy.GetContractFamily(type);
                IProtoSerializer ser = family == MetaType.AttributeFamily.None
                    ? this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(BinaryDataFormat.Default, type, out defaultWireType)
                    : null;

                if (ser != null) _basicTypes.Add(new BasicType(type, ser));
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
            var info = GetTypeInfoFromDictionary(type);
            int key = -1;

            // the fast happy path: meta-types we've already seen
            if (info != null)
            {
                key = info.Value.Key;
                metaType = info.Value.MetaType;
                if (metaType.IsPending)
                {
                    WaitOnLock();
                }
                return key;
            }

            // the fast fail path: types that will never have a meta-type
            bool shouldAdd = AutoAddMissingTypes || addEvenIfAutoDisabled;

            if (IsInbuiltType(type))
            {
                if (shouldAdd && !addWithContractOnly) throw MetaType.InbuiltType(type);
                return -1; // this will never be a meta-type
            }

            // otherwise: we don't yet know

            // check for proxy types
            Type underlyingType = ResolveProxies(type);
            if (underlyingType != null && underlyingType != type)
            {
                key = _types.IndexOf(MetaTypeFinder, underlyingType);
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
                Type origType = type;
                bool weAdded = false;
                try
                {
                    TakeLock(ref opaqueToken);
                    // try to recognise a few familiar patterns...
                    if ((metaType = RecogniseCommonTypes(type)) == null)
                    { // otherwise, check if it is a contract
                        MetaType.AttributeFamily family = _autoAddStrategy.GetContractFamily(type);

                        if (family == MetaType.AttributeFamily.AutoTuple && Helpers.IsGenericType(type))
                        {
                            // always add safe tuples
                            Type def = type.GetGenericTypeDefinition();
                            if (def == MapType(typeof(System.Collections.Generic.KeyValuePair<,>))
                                || def.FullName.StartsWith("System.Tuple`")
                                || def.FullName.StartsWith("System.ValueTuple`"))
                            {
                                shouldAdd = true;
                            }
                        }

                        if (!shouldAdd || (
                            !type.IsEnum && addWithContractOnly && family == MetaType.AttributeFamily.None && !CheckTypeDoesntRequireContract(this, type)))
                        {
                            if (demand) ThrowUnexpectedType(type);
                            return key;
                        }

                        metaType = Create(type);
                    }

                    metaType.IsPending = true;

                    // double-checked
                    int winner = _types.IndexOf(MetaTypeFinder, type);
                    if (winner < 0)
                    {
                        ThrowIfFrozen();
                        key = Add(metaType);
                        weAdded = true;
                    }
                    else
                    {
                        key = winner;
                    }
                    if (weAdded)
                    {
                        metaType.ApplyDefaultBehaviour();
                        metaType.IsPending = false;
                    }
                    else
                    {
                        metaType = (MetaType)_types[winner];
                    }
                }
                finally
                {
                    ReleaseLock(opaqueToken);
                    if (weAdded)
                    {
                        ResetKeyCache();
                    }
                    AddDependencies(metaType);
                }
            }
            return key;
        }

        bool IsInbuiltType(Type type)
        {
            return !type.IsEnum && TryGetBasicTypeSerializer(type) != null;
        }

        void AddDependencies(MetaType type)
        {
            //// e.g. IDictionary<MyKey, MyValue> - specific generic type
            //Type defaultType;
            //if (MetaType.IsDictionaryOrListInterface(this, type.Type, out defaultType))
            //{
            //    //baseType.AddSubType()
            //}
        }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool EnableAutoCompile()
    {
        try
        {
            var dm = new DynamicMethod("CheckCompilerAvailable", typeof(bool), new Type[] { typeof(int) });
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, 42);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Ret);
            var func = (Predicate<int>)dm.CreateDelegate(typeof(Predicate<int>));
            return func(42);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

        OverridingManager _overridingManager = new OverridingManager();

        /// <summary>
        /// Type overrides are run before preparing type serializer
        /// </summary>
        public void AddTypeOverride(TypeSettingsOverride @override)
        {
            if (@override == null) throw new ArgumentNullException(nameof(@override));
            int opaqueToken = 0;
            try
            {
                TakeLock(ref opaqueToken);
                ThrowIfFrozen();
                _overridingManager.Add(@override);
            }
            finally
            {
                ReleaseLock(opaqueToken);
            }
        }

    internal MetaType FindWithAmbientCompatibility(Type type, CompatibilityLevel ambient)
    {
        var found = (MetaType)types[FindOrAddAuto(type, true, false, false, ambient)];
        if (found is object && found.IsAutoTuple && found.CompatibilityLevel != ambient)
        {
            throw new InvalidOperationException($"The tuple-like type {type.NormalizeName()} must use a single compatiblity level, but '{ambient}' and '{found.CompatibilityLevel}' are both observed; this usually means it is being used in different contexts in the same model.");
        }
        return found;
    }

        /// <summary>
        /// Member overrides are run before preparing member serializer
        /// </summary>
        public void AddFieldOverride(FieldSettingsOverride @override)
        {
            if (@override == null) throw new ArgumentNullException(nameof(@override));
            int opaqueToken = 0;
            try
            {
                TakeLock(ref opaqueToken);
                ThrowIfFrozen();
                _overridingManager.Add(@override);
            }
            finally
            {
                ReleaseLock(opaqueToken);
            }
        }

    private static readonly BasicList.MatchPredicate MetaTypeFinder = (value, ctx)
        => ((MetaType)value).Type == (Type)ctx;
        
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
            return new MetaType(this, type, _defaultFactory);
        }
        
        /// <summary>
        /// If enabled all Arrays and Lists will be handled in extended mode to always support reference tracking and null (which will break without runtime/pre-compiled dll) but may have a size overhead (going to fix that in later releases)
        /// </summary>
        public bool AlwaysUseTypeRegistrationForCollections { get; set; }
        
        /// <summary>
        /// Adds all types inside the specified assembly. Surrogates are not detected automatically, set surrogates before calling this!
        /// </summary>
        public void Add(Assembly assembly, bool nonPublic, bool applyDefaultBehavior, MetaType.AttributeFamily? rootFilter = MetaType.AttributeFamily.Aqla | MetaType.AttributeFamily.ProtoBuf | MetaType.AttributeFamily.DataContractSerialier | MetaType.AttributeFamily.XmlSerializer)
        {
            Add(assembly, nonPublic, applyDefaultBehavior, t => rootFilter == null || (_autoAddStrategy.GetContractFamily(t) & rootFilter.Value) != 0);
        }
        
        /// <summary>
        /// Adds all types inside the specified assembly. Surrogates are not detected automatically, set surrogates before calling this!
        /// </summary>
        public void Add(Assembly assembly, bool nonPublic, bool applyDefaultBehavior, AqlaPredicate<Type> rootFilter)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (rootFilter == null) throw new ArgumentNullException(nameof(rootFilter));
            Type[] list = nonPublic ? Helpers.GetTypes(assembly) : Helpers.GetExportedTypes(assembly);
            
            // types order actually does not matter
            // but subtypes does
            Array.Sort(list, new TypeNamesSortComparer());


            foreach (Type t in list)
            {
                if (!Helpers.IsGenericTypeDefinition(t) && rootFilter(t))
                {
                    Add(t, applyDefaultBehavior);
                }
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
        /// Use this when you need to recreate RuntimeTypeModel with the same subtype keys on another side (if you use runtime (non-precompiled) models they should be initialized in this way), see also <see cref="ExportTypeRelations"/>. It doesn't synchronize member field numbers. Use <paramref name="forceDefaultBehavior"></paramref> to auto-register fields or add them with proper numbers manually.
        /// </summary>
        /// <remarks>
        /// On 1st side (possible network server):
        /// Serialize(stream, model.ExportTypeRelations());
        /// On 2nd side (possible network client):
        /// model.ImportTypeRelations((ModelTypeRelationsData)Deserialize(...))
        /// </remarks>
        /// <param name="data">The data exported with <see cref="ExportTypeRelations"/></param>
        /// <param name="forceDefaultBehavior">
        /// Allows to add members with their field numbers the same way as <see cref="MetaType.ApplyDefaultBehaviour"/> does.
        /// If you don't need to register any members or want to add them manually pass false to this parameter.
        /// </param>
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
                        ImportTypeRelationsElement(t);
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

        public MetaType ImportTypeRelationsElement(TypeData data)
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
                        t.AddSubType(subType.FieldNumber, subType.Type);
                    }
                return t;
            }
            finally
            {
                ReleaseLock(lockToken);
            }
        }

        /// <summary>
        /// Exports all types list with registered subtypes, can be used to recreate the same model (normal field numbers are not stored, only subtype numbers).
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
            if (type == null) throw new ArgumentNullException(nameof(type));
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
                            nameof(applyDefaultBehaviourIfNew));
                    }
                    // we should assume that type is fully configured, though; no need to re-run:
                    applyDefaultBehaviourIfNew = false;
                }
                if (newType == null) newType = Create(type);
                newType.IsPending = true;
                TakeLock(ref opaqueToken);
                // double checked
                if (FindWithoutAdd(type) != null) throw new ArgumentException("Duplicate type", nameof(type));
                ThrowIfFrozen();
                Add(newType);
                if (applyDefaultBehaviourIfNew) { newType.ApplyDefaultBehaviour(); }
                newType.IsPending = false;
            }
            finally
            {
                ReleaseLock(opaqueToken);
            }
            AddDependencies(newType);
            return newType;
        }

#if FEAT_COMPILER && !FX11
        /// <summary>
        /// Should serializers be compiled on demand? It may be useful
        /// to disable this for debugging purposes.
        /// </summary>
        public bool AutoCompile { get { return GetOption(OPTIONS_AutoCompile); } set { SetOption(OPTIONS_AutoCompile, value); } }
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

        RuntimeTypeModel _compiledVersionCache;

        /// <summary>
        /// Verifies that the model is still open to changes; if not, an exception is thrown
        /// </summary>
        private void ThrowIfFrozen()
        {
            if (GetOption(OPTIONS_Frozen)) throw new InvalidOperationException("The model cannot be changed once frozen");
            _compiledVersionCache = null;
        }

        /// <summary>
        /// Prevents further changes to this model
        /// </summary>
        public void Freeze()
        {
            if (GetOption(RuntimeTypeModelOptions.IsDefaultModel)) ThrowDefaultFrozen();
            SetOption(RuntimeTypeModelOptions.Frozen, true);
        }

    /// <summary>
    /// Like the non-generic Add(Type); for convenience
    /// </summary>
    public MetaType Add<T>(bool applyDefaultBehaviour = true, CompatibilityLevel compatibilityLevel = default)
        => Add(typeof(T), applyDefaultBehaviour, compatibilityLevel);

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
    /// <param name="applyDefaultBehaviour">Whether to apply the inbuilt configuration patterns (via attributes etc), or
    /// just add the type with no additional configuration (the type must then be manually configured).</param>
    /// <returns>The MetaType representing this type, allowing
    /// further configuration.</returns>
    public MetaType Add(Type type, bool applyDefaultBehaviour)
        => Add(type, applyDefaultBehaviour, default);

        internal int GetKey(Type type, bool demand, bool getBaseKey)
        {
            Helpers.DebugAssert(type != null);
            return Helpers.WrapExceptions(
                () =>
                    {
                        int typeIndex = FindOrAddAuto(type, demand, true, false);
                        if (typeIndex >= 0)
                        {
                            MetaType mt = (MetaType)_types[typeIndex];
                            if (getBaseKey)
                            {
                                mt = MetaType.GetTypeForRootSerialization(mt);
                                typeIndex = FindOrAddAuto(mt.Type, true, true, false);
                            }
                        }
                        return typeIndex;
                    },
                ex => Helpers.TryGetWrappedExceptionMessage(ex, type));
        }

        internal static event Action<string> ValidateDll;

    /// <summary>
    /// Raised before a type is auto-configured; this allows the auto-configuration to be electively suppressed
    /// </summary>
    /// <remarks>This callback should be fast and not involve complex external calls, as it may block the model</remarks>
    public event EventHandler<TypeAddedEventArgs> BeforeApplyDefaultBehaviour;

    /// <summary>
    /// Raised after a type is auto-configured; this allows additional external customizations
    /// </summary>
    /// <remarks>This callback should be fast and not involve complex external calls, as it may block the model</remarks>
    public event EventHandler<TypeAddedEventArgs> AfterApplyDefaultBehaviour;

    internal static void OnAfterApplyDefaultBehaviour(MetaType metaType, ref TypeAddedEventArgs args)
        => OnApplyDefaultBehaviour(metaType?.Model?.AfterApplyDefaultBehaviour, metaType, ref args);

    private static void OnApplyDefaultBehaviour(
        EventHandler<TypeAddedEventArgs> handler, MetaType metaType, ref TypeAddedEventArgs args)
    {
        if (handler is object)
        {
            if (args is null) args = new TypeAddedEventArgs(metaType);
            handler(metaType.Model, args);
        }
    }

    /// <summary>
    /// Should serializers be compiled on demand? It may be useful
    /// to disable this for debugging purposes.
    /// </summary>
    public bool AutoCompile
    {
        get { return GetOption(RuntimeTypeModelOptions.AutoCompile); }
        set { SetOption(RuntimeTypeModelOptions.AutoCompile, value); }
    }

    internal static void OnBeforeApplyDefaultBehaviour(MetaType metaType, ref TypeAddedEventArgs args)
        => OnApplyDefaultBehaviour(metaType?.Model?.BeforeApplyDefaultBehaviour, metaType, ref args);

        /// <summary>
        /// Turn off this when each (de)serialization has side effects like changing static field
        /// </summary>
        internal bool SkipCompiledVsNotCheck { get; set; }

        internal bool SkipForcedLateReference { get; set; }

        internal bool SkipForcedAdvancedVersioning { get; set; }

        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream.
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination stream to write to.</param>
        /// <param name="isRoot"></param>
        protected internal override void Serialize(int key, object value, ProtoWriter dest, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            var metaType = ((MetaType)_types[key]);
            var ser = isRoot ? metaType.RootSerializer : metaType.Serializer;

            ser.Write(value, dest);

#if CHECK_COMPILED_VS_NOT
            if (ProtoWriter.GetLongPosition(dest) < int.MaxValue / 10 && !SkipCompiledVsNotCheck && (value.GetType().IsPublic || value.GetType().IsNestedPublic) && isRoot)
            {
                RuntimeTypeModel rtm = GetInvertedVersionForCheckCompiledVsNot(key, metaType);

                byte[] original;
                // can't use cause netCache may contain items from ather array items from aux so different output

                using (var ms = new MemoryStream())
                {
                    using (var wr = new ProtoWriter(ms, this, null))
                        ser.Write(value, wr);
                    original = ms.ToArray();
                }

                using (var ms = new MemoryStream())
                {
                    using (var wr = new ProtoWriter(ms, this, dest.Context))
                    {
                        var invType = rtm[key];
                        var invSer = invType.RootSerializer;
                        invSer.Write(value, wr);
                    }
                    if (!original.SequenceEqual(ms.ToArray()))
                        throw new InvalidOperationException("CHECK_COMPILED_VS_NOT failed, lengths " + ms.Length + ", " + original.Length);
                    if (1.Equals(2))
                    {
                        using (var ms2 = new MemoryStream())
                        {
                            using (var wr = new ProtoWriter(ms2, this, null))
                            {
                                ser.Write(value, wr);
                            }
                        }
                    }
                }
            }
#endif
#endif
        }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object GetServices<T>(CompatibilityLevel ambient)
        => (_serviceCache[typeof(T)] ?? GetServicesSlow(typeof(T), ambient));

    /// <summary>Indicates whether a type is known to the model</summary>
    internal override bool IsKnownType<T>(CompatibilityLevel ambient) // the point of this override is to avoid loops
                                                                      // when trying to *build* a model; we don't actually need the service (which may not exist yet);
                                                                      // we just need to know whether we should *expect one*
        => _serviceCache[typeof(T)] is object || FindOrAddAuto(typeof(T), false, true, false, ambient) >= 0;
    internal void ResetServiceCache(Type type)
    {
        if (type is object)
        {
            lock (_serviceCache)
            {
                _serviceCache.Remove(type);
            }
        }
    }

    private object GetServicesSlow(Type type, CompatibilityLevel ambient)
    {
        if (type is null) return null; // GIGO
        object service;
        lock (_serviceCache)
        {   // once more, with feeling
            service = _serviceCache[type];
            if (service is object) return service;
        }
        service = GetServicesImpl(this, type, ambient);
        if (service is object)
        {
            try
            {
                _ = this[type]; // if possible, make sure that we've registered it, so we export a proxy if needed
            }
            catch { }
            lock (_serviceCache)
            {
                _serviceCache[type] = service;
            }
        }
        return service;

        static object GetServicesImpl(RuntimeTypeModel model, Type type, CompatibilityLevel ambient)
        {
            if (type.IsEnum) return EnumSerializers.GetSerializer(type);

            var nt = Nullable.GetUnderlyingType(type);
            if (nt is object)
            {
                // rely on the fact that we always do double-duty with nullables
                return model.GetServicesSlow(nt, ambient);
            }

            // rule out repeated (this has an internal cache etc)
            var repeated = model.TryGetRepeatedProvider(type); // this handles ignores, etc
            if (repeated is object) return repeated.Serializer;

            int typeIndex = model.FindOrAddAuto(type, false, true, false, ambient);
            if (typeIndex >= 0)
            {
                var mt = (MetaType)model.types[typeIndex];
                var serializer = mt.Serializer;
                if (serializer is IExternalSerializer external)
                {
                    return external.Service;
                }
                return serializer;
            }

            return null;
        }

    }

    /// <summary>
    /// See Object.ToString
    /// </summary>
    public override string ToString() => _name ?? base.ToString();

    // this is used by some unit-tests; do not remove
    internal Compiler.ProtoSerializer<TActual> GetSerializer<TActual>(IRuntimeProtoSerializerNode serializer, bool compiled)
    {
        if (serializer is null) throw new ArgumentNullException(nameof(serializer));

        if (compiled) return Compiler.CompilerContext.BuildSerializer<TActual>(Scope, serializer, this);

        return new Compiler.ProtoSerializer<TActual>(
            (ref ProtoWriter.State state, TActual val) => serializer.Write(ref state, val));
    }

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

    private void BuildAllSerializers()
    {
        // note that types.Count may increase during this operation, as some serializers
        // bring other types into play
        for (int i = 0; i < types.Count; i++)
        {
            // the primary purpose of this is to force the creation of the Serializer
            MetaType mt = (MetaType)types[i];

            if (GetServicesSlow(mt.Type, mt.CompatibilityLevel) is null) // respects enums, repeated, etc
                throw new InvalidOperationException("No serializer available for " + mt.Type.NormalizeName());
        }
    }

    internal sealed class SerializerPair : IComparable
    {
        int IComparable.CompareTo(object obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));
            SerializerPair other = (SerializerPair)obj;

            // we want to bunch all the items with the same base-type together, but we need the items with a
            // different base **first**.
            if (this.BaseKey == this.MetaKey)
            {
                if (other.BaseKey == other.MetaKey)
                { // neither is a subclass
                    return this.MetaKey.CompareTo(other.MetaKey);
                }
                else
                { // "other" (only) is involved in inheritance; "other" should be first
                    return 1;
                }
            }
            else
            {
                if (other.BaseKey == other.MetaKey)
                { // "this" (only) is involved in inheritance; "this" should be first
                    return -1;
                }
                else
                { // both are involved in inheritance
                    int result = this.BaseKey.CompareTo(other.BaseKey);
                    if (result == 0) result = this.MetaKey.CompareTo(other.MetaKey);
                    return result;
                }
            }
        }
        public readonly int MetaKey, BaseKey;
        public readonly MetaType Type;
        public readonly MethodBuilder Serialize, Deserialize;
        public readonly ILGenerator SerializeBody, DeserializeBody;
        public SerializerPair(int metaKey, int baseKey, MetaType type, MethodBuilder serialize, MethodBuilder deserialize,
            ILGenerator serializeBody, ILGenerator deserializeBody)
        {
            this.MetaKey = metaKey;
            this.BaseKey = baseKey;
            this.Serialize = serialize;
            this.Deserialize = deserialize;
            this.SerializeBody = serializeBody;
            this.DeserializeBody = deserializeBody;
            this.Type = type;
        }
    }

    internal static ILGenerator Override(TypeBuilder type, string name)
        => Override(type, name, out _);
    internal static ILGenerator Override(TypeBuilder type, string name, out Type[] genericArgs)
    {
        MethodInfo baseMethod;
        try
        {
            baseMethod = type.BaseType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (baseMethod is null)
                throw new ArgumentException($"Unable to resolve '{name}'");
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Unable to resolve '{name}': {ex.Message}", nameof(name), ex);
        }

        var parameters = baseMethod.GetParameters();
        var paramTypes = new Type[parameters.Length];
        for (int i = 0; i < paramTypes.Length; i++)
        {
            paramTypes[i] = parameters[i].ParameterType;
        }
        MethodBuilder newMethod = type.DefineMethod(baseMethod.Name,
            (baseMethod.Attributes & ~MethodAttributes.Abstract) | MethodAttributes.Final, baseMethod.CallingConvention, baseMethod.ReturnType, paramTypes);
        if (baseMethod.IsGenericMethodDefinition)
        {
            genericArgs = baseMethod.GetGenericArguments();
            string[] names = Array.ConvertAll(genericArgs, x => x.Name);
            newMethod.DefineGenericParameters(names);
        }
        else
            genericArgs = Type.EmptyTypes;
        for (int i = 0; i < parameters.Length; i++)
        {
            newMethod.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);
        }
        ILGenerator il = newMethod.GetILGenerator();
        type.DefineMethodOverride(newMethod, baseMethod);
        return il;
    }

    /// <summary>
    /// Represents configuration options for compiling a model to 
    /// a standalone assembly.
    /// </summary>
    public sealed class CompilerOptions
    {
        /// <summary>
        /// Import framework options from an existing type
        /// </summary>
        public void SetFrameworkOptions(MetaType from)
        {
            if (from is null) throw new ArgumentNullException(nameof(from));
            AttributeMap[] attribs = AttributeMap.Create(from.Type.Assembly);
            foreach (AttributeMap attrib in attribs)
            {
                if (attrib.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
                {
                    if (attrib.TryGet("FrameworkName", out var tmp)) TargetFrameworkName = (string)tmp;
                    if (attrib.TryGet("FrameworkDisplayName", out tmp)) TargetFrameworkDisplayName = (string)tmp;
                    break;
                }
            }
        }

        /// <summary>
        /// The TargetFrameworkAttribute FrameworkName value to burn into the generated assembly
        /// </summary>
        public string TargetFrameworkName { get; set; }

        /// <summary>
        /// The TargetFrameworkAttribute FrameworkDisplayName value to burn into the generated assembly
        /// </summary>
        public string TargetFrameworkDisplayName { get; set; }
        /// <summary>
        /// The name of the TypeModel class to create
        /// </summary>
        public string TypeName { get; set; }

#if PLAT_NO_EMITDLL
        internal const string NoPersistence = "Assembly persistence not supported on this runtime";
#endif
        /// <summary>
        /// The path for the new dll
        /// </summary>
#if PLAT_NO_EMITDLL
        [Obsolete(NoPersistence)]
#endif
        public string OutputPath { get; set; }
        /// <summary>
        /// The runtime version for the generated assembly
        /// </summary>
        public string ImageRuntimeVersion { get; set; }
        /// <summary>
        /// The runtime version for the generated assembly
        /// </summary>
        public int MetaDataVersion { get; set; }

        /// <summary>
        /// The acecssibility of the generated serializer
        /// </summary>
        public Accessibility Accessibility { get; set; }

        /// <summary>
        /// Implements a filter for use when generating models from assemblies
        /// </summary>
        public event Func<Type, bool> IncludeType;

        internal bool OnIncludeType(Type type)
        {
            var evt = IncludeType;
            return evt is null || evt(type);
        }
    }

    /// <summary>
    /// Type accessibility
    /// </summary>
    public enum Accessibility
    {
        /// <summary>
        /// Available to all callers
        /// </summary>
        Public,
        /// <summary>
        /// Available to all callers in the same assembly, or assemblies specified via [InternalsVisibleTo(...)]
        /// </summary>
        Internal
    }

#if !PLAT_NO_EMITDLL
    /// <summary>
    /// Fully compiles the current model into a static-compiled serialization dll
    /// (the serialization dll still requires protobuf-net for support services).
    /// </summary>
    /// <remarks>A full compilation is restricted to accessing public types / members</remarks>
    /// <param name="name">The name of the TypeModel class to create</param>
    /// <param name="path">The path for the new dll</param>
    /// <returns>An instance of the newly created compiled type-model</returns>
    public TypeModel Compile(string name, string path)
    {
        var options = new CompilerOptions()
        {
            TypeName = name,
#pragma warning disable CS0618
            OutputPath = path,
#pragma warning restore CS0618
        };
        return Compile(options);
    }
#endif
    /// <summary>
    /// Fully compiles the current model into a static-compiled serialization dll
    /// (the serialization dll still requires protobuf-net for support services).
    /// </summary>
    /// <remarks>A full compilation is restricted to accessing public types / members</remarks>
    /// <returns>An instance of the newly created compiled type-model</returns>
    public TypeModel Compile(CompilerOptions options = null)
    {
        options ??= new CompilerOptions();
        string typeName = options.TypeName;
#pragma warning disable 0618
        string path = options.OutputPath;
#pragma warning restore 0618
        BuildAllSerializers();
        Freeze();
        bool save = !string.IsNullOrEmpty(path);
        if (string.IsNullOrEmpty(typeName))
        {
#pragma warning disable CA2208 // param name - for clarity
            if (save) throw new ArgumentNullException("typeName");
#pragma warning restore CA2208 // param name - for clarity
            typeName = "CompiledModel_" + Guid.NewGuid().ToString();
        }

        string assemblyName, moduleName;
        if (path is null)
        {
            assemblyName = typeName;
            moduleName = assemblyName + ".dll";
        }
        else
        {
            assemblyName = new System.IO.FileInfo(System.IO.Path.GetFileNameWithoutExtension(path)).Name;
            moduleName = assemblyName + System.IO.Path.GetExtension(path);
        }

#if PLAT_NO_EMITDLL
        AssemblyName an = new AssemblyName { Name = assemblyName };
        AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(an,
            AssemblyBuilderAccess.Run);
        ModuleBuilder module = asm.DefineDynamicModule(moduleName);
#else
        AssemblyName an = new AssemblyName { Name = assemblyName };
        AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(an,
            save ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run);
        ModuleBuilder module = save ? asm.DefineDynamicModule(moduleName, path)
                                    : asm.DefineDynamicModule(moduleName);
#endif
        var scope = CompilerContextScope.CreateForModule(this, module, true, assemblyName);
        WriteAssemblyAttributes(options, assemblyName, asm);


        var serviceType = WriteBasicTypeModel("<Services>" + typeName, module, typeof(object), true);
        // note: the service could benefit from [DynamicallyAccessedMembers(DynamicAccess.Serializer)], but: that only exists
        // (on the public API) in net5+, and those platforms don't allow full dll emit (which is when the linker matters)
        WriteSerializers(scope, serviceType);
        WriteEnumsAndProxies(serviceType);

#if PLAT_NO_EMITDLL
        var finalServiceType = serviceType.CreateTypeInfo().AsType();
#else
        var finalServiceType = serviceType.CreateType();
#endif

        var modelType = WriteBasicTypeModel(typeName, module, typeof(TypeModel),
            options.Accessibility == Accessibility.Internal);

        WriteConstructorsAndOverrides(modelType, finalServiceType);

#if PLAT_NO_EMITDLL
        Type finalType = modelType.CreateTypeInfo().AsType();
#else
        Type finalType = modelType.CreateType();
#endif
        if (!string.IsNullOrEmpty(path))
        {
#if PLAT_NO_EMITDLL
            throw new NotSupportedException(CompilerOptions.NoPersistence);
#else
            try
            {
                asm.Save(path);
            }
            catch (IOException ex)
            {
                // advertise the file info
                throw new IOException(path + ", " + ex.Message, ex);
            }
            Debug.WriteLine("Wrote dll:" + path);
#endif
        }
        return (TypeModel)Activator.CreateInstance(finalType, nonPublic: true);
    }

    private void WriteConstructorsAndOverrides(TypeBuilder type, Type serviceType)
    {
        ILGenerator il;
        var options = Options;
        if (options != TypeModel.DefaultOptions)
        {
            il = Override(type, "get_" + nameof(TypeModel.Options));
            CompilerContext.LoadValue(il, (int)options);
            il.Emit(OpCodes.Ret);
        }

        il = Override(type, nameof(TypeModel.GetSerializer), out var genericArgs);
        var genericT = genericArgs.Single();
        var method = typeof(SerializerCache).GetMethod(nameof(SerializerCache.Get)).MakeGenericMethod(serviceType, genericT);
        il.EmitCall(OpCodes.Call, method, null);
        il.Emit(OpCodes.Ret);

        type.DefineDefaultConstructor(MethodAttributes.Public);
    }

    private void WriteEnumsAndProxies(TypeBuilder type)
    {
        for (int index = 0; index < types.Count; index++)
        {
            var metaType = (MetaType)types[index];
            var runtimeType = metaType.Type;
            RepeatedSerializerStub repeated;
            if (runtimeType.IsEnum)
            {
                var member = EnumSerializers.GetProvider(runtimeType);
                AddProxy(type, runtimeType, member, true);
            }
            else if (ShouldEmitCustomSerializerProxy(metaType.SerializerType))
            {
                AddProxy(type, runtimeType, metaType.SerializerType, false);
            }
            else if ((repeated = TryGetRepeatedProvider(runtimeType)) is object)
            {
                AddProxy(type, runtimeType, repeated.Provider, false);
            }
        }
        static bool ShouldEmitCustomSerializerProxy(Type serializerType)
        {
            if (serializerType is null) return false; // nothing to do
            if (IsFullyPublic(serializerType)) return true; // fine, just do it

            // so: non-public; don't emit for anything inbuilt
            return serializerType.Assembly != typeof(PrimaryTypeProvider).Assembly;
        }
    }

    internal static MemberInfo GetUnderlyingProvider(MemberInfo provider, Type forType)
    {
        switch (provider)
        {   // properties are really a special-case of methods, via the getter
            case PropertyInfo property:
                provider = property.GetGetMethod(true);
                break;
            // types are really a short-hand for the singleton API
            case Type type when type.IsClass && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is object:
                provider = typeof(SerializerCache).GetMethod(nameof(SerializerCache.Get), BindingFlags.Public | BindingFlags.Static)
                    .MakeGenericMethod(type, forType);
                break;
        }
        return provider;
    }

    internal static void EmitProvider(MemberInfo provider, ILGenerator il)
    {
        // after GetUnderlyingProvider, all we *actually* need to implement is fields and methods
        switch (provider)
        {
            case FieldInfo field when field.IsStatic:
                il.Emit(OpCodes.Ldsfld, field);
                break;
            case MethodInfo method when method.IsStatic:
                il.EmitCall(OpCodes.Call, method, null);
                break;
            default:
                ThrowHelper.ThrowInvalidOperationException($"Invalid provider: {provider}");
                break;
        }
    }

    internal RepeatedSerializerStub TryGetRepeatedProvider(Type type, CompatibilityLevel ambient = default)
    {
        if (type is null) return null;
        var repeated = RepeatedSerializers.TryGetRepeatedProvider(type);
        // but take it back if it is explicitly excluded
        if (repeated is object)
        { // looks like a list, but double check for IgnoreListHandling
            int idx = this.FindOrAddAuto(type, false, true, false, ambient);
            if (idx >= 0 && ((MetaType)types[idx]).IgnoreListHandling)
            {
                return null;
            }
        }
        return repeated;
    }

    private static void AddProxy(TypeBuilder building, Type proxying, MemberInfo provider, bool includeNullable)
    {
        provider = GetUnderlyingProvider(provider, proxying);
        if (provider is object)
        {
            var iType = typeof(ISerializerProxy<>).MakeGenericType(proxying);
            building.AddInterfaceImplementation(iType);
            var il = CompilerContextScope.Implement(building, iType, "get_" + nameof(ISerializerProxy<string>.Serializer));
            EmitProvider(provider, il);
            il.Emit(OpCodes.Ret);

            if (includeNullable)
            {
                iType = typeof(ISerializerProxy<>).MakeGenericType(typeof(Nullable<>).MakeGenericType(proxying));
                building.AddInterfaceImplementation(iType);
                il = CompilerContextScope.Implement(building, iType, "get_" + nameof(ISerializerProxy<string>.Serializer));
                EmitProvider(provider, il);
                il.Emit(OpCodes.Ret);
            }
        }
    }

    private void WriteSerializers(CompilerContextScope scope, TypeBuilder type)
    {
        // there are only a few permutations of "features" you want; share them between like-minded types
        var featuresLookup = new Dictionary<SerializerFeatures, MethodInfo>();

        MethodInfo GetFeaturesMethod(SerializerFeatures features)
        {
            if (!featuresLookup.TryGetValue(features, out var method))
            {
                var name = nameof(ISerializer<int>.Features) + "_" + ((int)features).ToString(CultureInfo.InvariantCulture);
                var newMethod = type.DefineMethod(name, MethodAttributes.Private | MethodAttributes.Virtual,
                    typeof(SerializerFeatures), Type.EmptyTypes);
                ILGenerator il = newMethod.GetILGenerator();
                CompilerContext.LoadValue(il, (int)features);
                il.Emit(OpCodes.Ret);
                method = featuresLookup[features] = newMethod;
            }
            return method;
        }

        for (int index = 0; index < types.Count; index++)
        {
            var metaType = (MetaType)types[index];
            var serializer = metaType.Serializer;
            var runtimeType = metaType.Type;

            metaType.Validate();
            if (runtimeType.IsEnum || metaType.SerializerType is object || TryGetRepeatedProvider(metaType.Type) is object)
            {   // we don't implement these
                continue;
            }
            if (!IsFullyPublic(runtimeType, out var problem))
            {
                ThrowHelper.ThrowInvalidOperationException("Non-public type cannot be used with full dll compilation: " + problem.NormalizeName());
            }

            Type inheritanceRoot = metaType.GetInheritanceRoot();

            // we always emit the serializer API
            var serType = typeof(ISerializer<>).MakeGenericType(runtimeType);
            type.AddInterfaceImplementation(serType);

            var il = CompilerContextScope.Implement(type, serType, nameof(ISerializer<string>.Read));
            using (var ctx = new CompilerContext(scope, il, false, CompilerContext.SignatureType.ReaderScope_Input, this, runtimeType, nameof(ISerializer<string>.Read)))
            {
                if (serializer.HasInheritance)
                {
                    serializer.EmitReadRoot(ctx, ctx.InputValue);
                }
                else
                {
                    serializer.EmitRead(ctx, ctx.InputValue);
                    ctx.LoadValue(ctx.InputValue);
                }
                ctx.Return();
            }

            il = CompilerContextScope.Implement(type, serType, nameof(ISerializer<string>.Write));
            using (var ctx = new CompilerContext(scope, il, false, CompilerContext.SignatureType.WriterScope_Input, this, runtimeType, nameof(ISerializer<string>.Write)))
            {
                if (serializer.HasInheritance) serializer.EmitWriteRoot(ctx, ctx.InputValue);
                else serializer.EmitWrite(ctx, ctx.InputValue);
                ctx.Return();
            }

            var featuresGetter = serType.GetProperty(nameof(ISerializer<string>.Features)).GetGetMethod();
            type.DefineMethodOverride(GetFeaturesMethod(serializer.Features), featuresGetter);

            // and we emit the sub-type serializer whenever inheritance is involved
            if (serializer.HasInheritance)
            {
                serType = typeof(ISubTypeSerializer<>).MakeGenericType(runtimeType);
                type.AddInterfaceImplementation(serType);

                il = CompilerContextScope.Implement(type, serType, nameof(ISubTypeSerializer<string>.WriteSubType));
                using (var ctx = new CompilerContext(scope, il, false, CompilerContext.SignatureType.WriterScope_Input, this,
                     runtimeType, nameof(ISubTypeSerializer<string>.WriteSubType)))
                {
                    serializer.EmitWrite(ctx, ctx.InputValue);
                    ctx.Return();
                }

                il = CompilerContextScope.Implement(type, serType, nameof(ISubTypeSerializer<string>.ReadSubType));
                using (var ctx = new CompilerContext(scope, il, false, CompilerContext.SignatureType.ReaderScope_Input, this,
                    typeof(SubTypeState<>).MakeGenericType(runtimeType),
                    nameof(ISubTypeSerializer<string>.ReadSubType)))
                {
                    serializer.EmitRead(ctx, ctx.InputValue);
                    // note that EmitRead will unwrap the T for us on the stack
                    ctx.Return();
                }
            }

            // if we're constructor skipping, provide a factory for that
            if (serializer.ShouldEmitCreateInstance)
            {
                serType = typeof(IFactory<>).MakeGenericType(runtimeType);
                type.AddInterfaceImplementation(serType);

                il = CompilerContextScope.Implement(type, serType, nameof(IFactory<string>.Create));
                using var ctx = new CompilerContext(scope, il, false, CompilerContext.SignatureType.Context, this,
                     typeof(ISerializationContext), nameof(IFactory<string>.Create));
                serializer.EmitCreateInstance(ctx, false);
                ctx.Return();
            }
        }
    }

    private static TypeBuilder WriteBasicTypeModel(string typeName, ModuleBuilder module,
        Type baseType, bool @internal)
    {
        TypeAttributes typeAttributes = (baseType.Attributes & ~(TypeAttributes.Abstract | TypeAttributes.Serializable)) | TypeAttributes.Sealed;
        if (@internal) typeAttributes &= ~TypeAttributes.Public;

        return module.DefineType(typeName, typeAttributes, baseType);
    }

    private void WriteAssemblyAttributes(CompilerOptions options, string assemblyName, AssemblyBuilder asm)
    {
        if (!string.IsNullOrEmpty(options.TargetFrameworkName))
        {
            // get [TargetFramework] from mscorlib/equivalent and burn into the new assembly
            Type versionAttribType = null;
            try
            { // this is best-endeavours only
                versionAttribType = TypeModel.ResolveKnownType("System.Runtime.Versioning.TargetFrameworkAttribute", typeof(string).Assembly);
            }
            catch { /* don't stress */ }
            if (versionAttribType is object)
            {
                PropertyInfo[] props;
                object[] propValues;
                if (string.IsNullOrEmpty(options.TargetFrameworkDisplayName))
                {
                    props = Array.Empty<PropertyInfo>();
                    propValues = Array.Empty<object>();
                }
                else
                {
                    props = new PropertyInfo[1] { versionAttribType.GetProperty("FrameworkDisplayName") };
                    propValues = new object[1] { options.TargetFrameworkDisplayName };
                }
                CustomAttributeBuilder builder = new CustomAttributeBuilder(
                    versionAttribType.GetConstructor(new Type[] { typeof(string) }),
                    new object[] { options.TargetFrameworkName },
                    props,
                    propValues);
                asm.SetCustomAttribute(builder);
            }
        }

        // copy assembly:InternalsVisibleTo
        Type internalsVisibleToAttribType = null;

        try
        {
            internalsVisibleToAttribType = typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute);
        }
        catch { /* best endeavors only */ }

        if (internalsVisibleToAttribType is object)
        {
            List<string> internalAssemblies = new List<string>();
            List<Assembly> consideredAssemblies = new List<Assembly>();
            foreach (MetaType metaType in types)
            {
                Assembly assembly = metaType.Type.Assembly;
                if (consideredAssemblies.IndexOf(assembly) >= 0) continue;
                consideredAssemblies.Add(assembly);

                AttributeMap[] assemblyAttribsMap = AttributeMap.Create(assembly);
                for (int i = 0; i < assemblyAttribsMap.Length; i++)
                {
                    if (assemblyAttribsMap[i].AttributeType != internalsVisibleToAttribType) continue;

                    assemblyAttribsMap[i].TryGet("AssemblyName", out var privelegedAssemblyObj);
                    string privelegedAssemblyName = privelegedAssemblyObj as string;
                    if (privelegedAssemblyName == assemblyName || string.IsNullOrEmpty(privelegedAssemblyName)) continue; // ignore

                    if (internalAssemblies.IndexOf(privelegedAssemblyName) >= 0) continue; // seen it before
                    internalAssemblies.Add(privelegedAssemblyName);

                    CustomAttributeBuilder builder = new CustomAttributeBuilder(
                        internalsVisibleToAttribType.GetConstructor(new Type[] { typeof(string) }),
                        new object[] { privelegedAssemblyName });
                    asm.SetCustomAttribute(builder);
                }
            }
        }
    }


    private readonly Hashtable _serviceCache = new Hashtable();

    internal override ISerializer<T> GetSerializerCore<T>(CompatibilityLevel ambient)
        => GetServices<T>(ambient) as ISerializer<T>;

    /// <summary>Resolve a service relative to T</summary>
    protected override ISerializer<T> GetSerializer<T>()
        => GetServices<T>(default) as ISerializer<T>;

#if CHECK_COMPILED_VS_NOT
        RuntimeTypeModel GetInvertedVersionForCheckCompiledVsNot(int key, MetaType metaType)
        {

            bool compiled = IsFrozen || metaType.IsCompiledInPlace;
            RuntimeTypeModel rtm;
            bool canCompileDll = _types.Cast<MetaType>().All(t => t.Type.IsPublic || t.Type.IsNestedPublic);
            if (!compiled)
            {
                if (_compiledVersionCache != null)
                    rtm = _compiledVersionCache;
                else
                {
                    rtm = CloneAsUnfrozen();
                    if (canCompileDll)
                        CompileForCheckAndValidate(rtm);
                    else
                        rtm[key].CompileInPlace();
                    _compiledVersionCache = rtm;
                }
            }
            else if (canCompileDll && _compiledVersionCache == null)
            {
                // we only check compilation here and throw away
                rtm = CloneAsUnfrozen();
                rtm.AutoCompile = false;
                // still need to validate
                if (string.IsNullOrEmpty(_compiledToPath))
                    CompileForCheckAndValidate(rtm.CloneAsUnfrozen());
                else
                    RaiseValidateDll(_compiledToPath);
                _compiledVersionCache = rtm;
            }
            else rtm = CloneAsUnfrozen();
            rtm.AutoCompile = false;
            return rtm;
        }
#endif

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="isRoot"></param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        protected internal override object DeserializeCore(int key, object value, ProtoReader source, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            var metaType = ((MetaType)_types[key]);

            var ser = isRoot ? metaType.RootSerializer : metaType.Serializer;
#if CHECK_COMPILED_VS_NOT
            long initialSourcePosition = source.LongPosition;
            long initialStreamPosition = initialSourcePosition + source.InitialUnderlyingStreamPosition;
            var refState = (!SkipCompiledVsNotCheck && isRoot) ? source.StoreReferenceState() : null;
            var initialWireType = source.WireType;
#endif
            object result;
            if (value == null && ser.ExpectedType.IsValueType)
            {
                if (ser.RequiresOldValue) value = CreateInstance(ser, source);
                result = ser.Read(value, source);
            }
            else
                result = ser.Read(value, source);

#if CHECK_COMPILED_VS_NOT
            if (!SkipCompiledVsNotCheck && (result == null || result.GetType().IsPublic || result.GetType().IsNestedPublic) && isRoot)
            {
                var rtm = GetInvertedVersionForCheckCompiledVsNot(key, metaType);
                
                var stream = source.UnderlyingStream;
                if (stream.CanSeek)
                {
                    long positionAfterRead = stream.Position;
                    stream.Position = source.InitialUnderlyingStreamPosition;
                    using (var pr = ProtoReader.Create(stream, this, source.Context, source.FixedLength >= 0 ? source.FixedLength : ProtoReader.TO_EOF))
                    {
                        pr.BlockEndPosition = source.BlockEndPosition;
                        if (initialWireType != WireType.None)
                        {
                            if (!ProtoReader.HasSubValue(initialWireType, pr)) throw new Exception();
                        }
                        pr.LoadReferenceState(refState);
                        pr.SkipBytes(initialSourcePosition);
                        var invType = rtm[key];
                        var invSer = invType.RootSerializer;
                        
                        var copy = invSer.Read(null, pr);

                        if (copy == null || result == null)
                        {
                            if ((copy == null) != (result == null))
                                throw new InvalidOperationException("CHECK_COMPILED_VS_NOT failed, copy is null");
                        }
                        else if (value == null && copy.GetType() != result.GetType())
                            throw new InvalidOperationException("CHECK_COMPILED_VS_NOT failed, types " + copy.GetType() + ", " + result.GetType());
                        else if (copy.GetType().IsPrimitive && !result.Equals(copy))
                            throw new InvalidOperationException("CHECK_COMPILED_VS_NOT failed, values " + copy + ", " + result);
                        else if (source.LongPosition != pr.LongPosition)
                            throw new InvalidOperationException("CHECK_COMPILED_VS_NOT failed, wrong position after read");
                    }
                    if (1.Equals(2)) // for debug
                    {
                        stream.Position = initialStreamPosition;
                        using (var pr = ProtoReader.Create(stream, this, source.Context, source.FixedLength >= 0 ? source.FixedLength : ProtoReader.TO_EOF))
                        {
                            if (initialWireType != WireType.None)
                            {
                                if (!ProtoReader.HasSubValue(initialWireType, pr)) throw new Exception();
                            }
                            pr.LoadReferenceState(refState);
                            var copy2 = ser.Read(null, pr);
                            copy2?.GetHashCode();
                        }

                    }
                    stream.Position = positionAfterRead;
                }
            }
#endif
            return result;
#endif
        }

#if CHECK_COMPILED_VS_NOT
        private static int _autoTestCounter;
        static void CompileForCheckAndValidate(RuntimeTypeModel rtm)
        {

            string name = "AutoTest" + Interlocked.Increment(ref _autoTestCounter);
            rtm.Compile(name, name + ".dll");
            RaiseValidateDll(name + ".dll");
        }
#endif

        static void RaiseValidateDll(string name)
        {
            ValidateDll?.Invoke(name);
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
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
#if FEAT_COMPILER && !FX11
            if (compiled) return Compiler.CompilerContext.BuildSerializer(serializer, this);
#endif
            return serializer.Write;
#endif
        }

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
            if (index < 0) return null;
            else
            {
                var metaType = ((MetaType)_types[index]);
                metaType.Serializer.GetHashCode();
                return metaType.GetEnumMap();
            }
        }

        private int _metadataTimeoutMilliseconds = 5000;
#pragma warning restore RCS1159 // Use EventHandler<T>.

    internal string GetSchemaTypeName(HashSet<Type> callstack, Type effectiveType, DataFormat dataFormat, CompatibilityLevel compatibilityLevel, bool asReference, bool dynamicType, HashSet<string> imports)
        => GetSchemaTypeName(callstack, effectiveType, dataFormat, compatibilityLevel, asReference, dynamicType, imports, out _);

    static bool IsWellKnownType(Type type, out string name, HashSet<string> imports)
    {
        if (type == typeof(byte[]))
        {
            name = "bytes";
            return true;
        }
        else if (type == typeof(Timestamp))
        {
            imports.Add(CommonImports.Timestamp);
            name = ".google.protobuf.Timestamp";
            return true;
        }
        else if (type == typeof(Duration))
        {
            imports.Add(CommonImports.Duration);
            name = ".google.protobuf.Duration";
            return true;
        }
        else if (type == typeof(Empty))
        {
            imports.Add(CommonImports.Empty);
            name = ".google.protobuf.Empty";
            return true;
        }
        name = default;
        return false;
    }
        /// <summary>
        /// The amount of time to wait if there are concurrent metadata access operations
        /// </summary>
        public int MetadataTimeoutMilliseconds
        {
            get { return _metadataTimeoutMilliseconds; }
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _metadataTimeoutMilliseconds = value;
            }
        }

#if DEBUG
        /// <summary>
        /// Gets how many times a model lock was taken
        /// </summary>
        public int LockCount { get; set; }
#endif
        internal void TakeLock(ref int opaqueToken)
        {
            const string message = "Timeout while inspecting metadata; this may indicate a deadlock. This can often be avoided by preparing necessary serializers during application initialization, rather than allowing multiple threads to perform the initial metadata inspection; please also see the LockContended event";
            opaqueToken = 0;
#if PORTABLE
            if(!Monitor.TryEnter(_types, _metadataTimeoutMilliseconds)) throw new TimeoutException(message);
            opaqueToken = Interlocked.CompareExchange(ref _contentionCounter, 0, 0); // just fetch current value (starts at 1)
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
            opaqueToken = Interlocked.CompareExchange(ref _contentionCounter, 0, 0); // just fetch current value (starts at 1)
#else
            if (Monitor.TryEnter(_types, _metadataTimeoutMilliseconds))
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
            LockCount++;
#endif
        }

        private int _contentionCounter = 1;
#if PLAT_NO_INTERLOCKED
        private readonly object _contentionLock = new object();
#endif
        private int GetContention()
        {
#if PLAT_NO_INTERLOCKED
            lock(_contentionLock)
            {
                return _contentionCounter;
            }
#else
            return Interlocked.CompareExchange(ref _contentionCounter, 0, 0);
#endif
        }

    /// <summary>
    /// Creates a new runtime model, to which the caller
    /// can add support for a range of types. A model
    /// can be used "as is", or can be compiled for
    /// optimal performance.
    /// </summary>
    /// <param name="name">The logical name of this model</param>
    public static RuntimeTypeModel Create([CallerMemberName] string name = null)
    {
        return new RuntimeTypeModel(false, name);
    }

    private readonly string _name;

    internal static bool IsFullyPublic(Type type) => IsFullyPublic(type, out _);

    internal static bool IsFullyPublic(Type type, out Type cause)
    {
        Type originalType = type;
        while (type is object)
        {
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                foreach (var arg in args)
                {
                    if (!IsFullyPublic(arg))
                    {
                        cause = arg;
                        return false;
                    }
                }
            }
            cause = type;
            if (type.IsNestedPublic)
            {
                type = type.DeclaringType;
            }
            else
            {
                return type.IsPublic;
            }
        }
        cause = originalType;
        return false;
    }

    /// <summary>
    /// Create a model that serializes all types from an
    /// assembly specified by type
    /// </summary>
    public static new TypeModel CreateForAssembly<T>()
        => AutoCompileTypeModel.CreateForAssembly<T>();

    /// <summary>
    /// Create a model that serializes all types from an
    /// assembly specified by type
    /// </summary>
    public static new TypeModel CreateForAssembly(Type type)
        => AutoCompileTypeModel.CreateForAssembly(type);

    /// <summary>
    /// Create a model that serializes all types from an assembly
    /// </summary>
    public static new TypeModel CreateForAssembly(Assembly assembly)
        => AutoCompileTypeModel.CreateForAssembly(assembly);

    /// <summary>
    /// Promotes this model instance to be the default model; the default model is used by <see cref="Serializer"/> and <see cref="Serializer.NonGeneric"/>.
    /// </summary>
    public void MakeDefault()
    {
        lock (s_ModelSyncLock)
        {
            var oldModel = DefaultModel as RuntimeTypeModel;

            if (ReferenceEquals(this, oldModel)) return; // we're already the default

            try
            {
                // pre-emptively set the IsDefaultModel flag on the current model
                SetOption(RuntimeTypeModelOptions.IsDefaultModel, true);

                // check invariants (no race condition here, because of ^^^)
                if (!UseImplicitZeroDefaults) ThrowDefaultUseImplicitZeroDefaults();
                if (!AutoAddMissingTypes) ThrowDefaultAutoAddMissingTypes();
                if (GetOption(RuntimeTypeModelOptions.Frozen)) ThrowDefaultFrozen();

                // actually flip the reference
                SetDefaultModel(this);
            }
            finally
            {
                // clear the IsDefaultModel flag on anything that is not, in fact, the default
                var currentDefault = DefaultModel;
                if (!ReferenceEquals(this, currentDefault))
                    SetOption(RuntimeTypeModelOptions.IsDefaultModel, false);

                if (oldModel is object && !ReferenceEquals(oldModel, currentDefault))
                    oldModel.SetOption(RuntimeTypeModelOptions.IsDefaultModel, false);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDefaultAutoAddMissingTypes()
        => throw new InvalidOperationException("The default model must allow missing types");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDefaultUseImplicitZeroDefaults()
        => throw new InvalidOperationException("UseImplicitZeroDefaults cannot be disabled on the default model");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDefaultFrozen()
        => throw new InvalidOperationException("The default model cannot be frozen");

    private static readonly object s_ModelSyncLock = new object();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static RuntimeTypeModel CreateDefaultModelInstance()
    {
        lock (s_ModelSyncLock)
        {
            if (DefaultModel is not RuntimeTypeModel model)
            {
                model = new RuntimeTypeModel(true, "(default)");
                SetDefaultModel(model);
            }
            return model;
        }
    }

    /// <summary>
    /// Treat all values of <typeparamref name="TUnderlying"/> (non-serializable)
    /// as though they were the surrogate <typeparamref name="TSurrogate"/> (serializable);
    /// if custom conversion operators are provided, they are used in place of implicit
    /// or explicit conversion operators.
    /// </summary>
    /// <typeparam name="TUnderlying">The non-serializable type to provide custom support for</typeparam>
    /// <typeparam name="TSurrogate">The serializable type that should be used instead</typeparam>
    /// <param name="underlyingToSurrogate">Custom conversion operation</param>
    /// <param name="surrogateToUnderlying">Custom conversion operation</param>
    /// <param name="dataFormat">The <see cref="DataFormat"/> to use</param>
    /// <param name="compatibilityLevel">The <see cref="CompatibilityLevel"/> to assume for this type</param>
    /// <returns>The original model (for chaining).</returns>
    public RuntimeTypeModel SetSurrogate<TUnderlying, TSurrogate>(
        Func<TUnderlying, TSurrogate> underlyingToSurrogate = null, Func<TSurrogate, TUnderlying> surrogateToUnderlying = null,
        DataFormat dataFormat = DataFormat.Default, CompatibilityLevel compatibilityLevel = CompatibilityLevel.NotSpecified)
    {
        Add<TUnderlying>(compatibilityLevel: compatibilityLevel).SetSurrogate(typeof(TSurrogate),
            GetMethod(underlyingToSurrogate, nameof(underlyingToSurrogate)),
            GetMethod(surrogateToUnderlying, nameof(surrogateToUnderlying)), dataFormat);
        return this;

        static MethodInfo GetMethod(Delegate value, string paramName)
        {
            if (value is null) return null;
            var handlers = value.GetInvocationList();
            if (handlers.Length != 1) ThrowHelper.ThrowArgumentException("A unicast delegate was expected.", paramName);
            value = handlers[0];
            if (value.Target is object target)
            {
                var msg = "A delegate to a static method was expected.";
                if (target.GetType().IsDefined(typeof(CompilerGeneratedAttribute)))
                {
                    msg += $" The conversion '{target.GetType().NormalizeName()}.{value.Method.Name}' is compiler-generated (possibly a lambda); an explicit static method should be used instead.";
                }
                ThrowHelper.ThrowArgumentException(msg, paramName);
            }
            return value.Method;
        }
    }
        private void AddContention()
        {
#if PLAT_NO_INTERLOCKED
            lock(_contentionLock)
            {
                _contentionCounter++;
            }
#else
            Interlocked.Increment(ref _contentionCounter);
#endif
        }

        internal void ReleaseLock(int opaqueToken)
        {
            if (opaqueToken != 0)
            {
                Monitor.Exit(_types);
                if (opaqueToken != GetContention()) // contention-count changes since we looked!
                {
                    LockContentedEventHandler handler = LockContended;
                    if (handler is object)
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

        #pragma warning disable RCS1159 // Use EventHandler<T>.
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
            _defaultFactory = methodInfo;
        }
        private MethodInfo _defaultFactory;

    private void VerifyNotNested(Type type, Type itemType)
    {
        if (itemType != null)
        {
            Type nestedItemType = null, nestedDefaultType = null;
            ResolveListTypes(itemType, ref nestedItemType, ref nestedDefaultType);
            if (nestedItemType != null)
            {
                throw TypeModel.CreateNestedListsNotSupported(type);
            }
        }
    }

    private static void RetrieveArrayListTypes(Type type, out Type itemType, out Type defaultType)
    {
        if (type.GetArrayRank() != 1)
        {
            throw new NotSupportedException("Multi-dimension arrays are supported");
        }
        itemType = type.GetElementType();
        if (itemType == typeof(byte))
        {
            defaultType = itemType = null;
        }
        else
        {
            defaultType = type;
        }
    }

        internal void VerifyFactory(MethodInfo factory, Type type)
        {
            if (factory != null)
            {
                if (type != null && type.IsValueType) throw new InvalidOperationException();
                if (!factory.IsStatic) throw new ArgumentException("A factory-method must be static", nameof(factory));
                if ((type != null && factory.ReturnType != type) && factory.ReturnType != MapType(typeof(object))) throw new ArgumentException("The factory-method must return object" + (type == null ? "" : (" or " + type.FullName)), nameof(factory));

                if (!CallbackSet.CheckCallbackParameters(this, factory)) throw new ArgumentException("Invalid factory signature in " + factory.DeclaringType.FullName + "." + factory.Name, nameof(factory));
            }
        }

        public void PrepareSerializer<T>()
        {
#if FEAT_COMPILER
            this[MapType(typeof(T))].CompileInPlace();
#endif
        }
        
        /// <summary>
        /// Returns a full deep copy of a model with all settings and added types
        /// </summary>
        public RuntimeTypeModel CloneAsUnfrozen()
        {
            var m = (RuntimeTypeModel)MemberwiseClone();
            m.SetOption(OPTIONS_Frozen, false);
            m.SetOption(OPTIONS_IsDefaultModel, false);
            m._types = new BasicList();
            m.AutoAddStrategy = AutoAddStrategy.Clone(m);
            m._basicTypes = new BasicList();
            m._overridingManager = m._overridingManager.CloneAsUnsubscribed();
            m.ResetTypesDictionary();
            var cache = new MetaTypeCloneCache(m);
            m._types = new BasicList();
            m.ValueSerializerBuilder = new ValueSerializerBuilder(m);
            foreach (MetaType metaType in _types.Cast<MetaType>().Select(cache.CloneMetaTypeWithoutDerived).Select(mt => (object)mt))
                m.Add(metaType);
            for (int i = 0; i < m._types.Count; i++)
            {
                var original = (MetaType)_types[i];
                var cloned = (MetaType)m._types[i];

                foreach (SubType st in original.GetSubtypes())
                    cloned.AddSubType(st.FieldNumber, st.DerivedType.Type);
            }
            return m;
        }

        class MetaTypeCloneCache
        {
            readonly RuntimeTypeModel _clonedModelInstance;

            readonly Dictionary<MetaType, MetaType> _oldToNew = new Dictionary<MetaType, MetaType>();

            public MetaTypeCloneCache(RuntimeTypeModel clonedModelInstance)
            {
                if (clonedModelInstance == null) throw new ArgumentNullException(nameof(clonedModelInstance));
                _clonedModelInstance = clonedModelInstance;
            }

            public MetaType CloneMetaTypeWithoutDerived(MetaType type)
            {
                var model = _clonedModelInstance;
                MetaType cloned;
                if (_oldToNew.TryGetValue(type, out cloned)) return cloned;

                var baseType = type.BaseType;
                if (baseType != null)
                    baseType = CloneMetaTypeWithoutDerived(baseType);

                if (!_oldToNew.TryGetValue(type, out cloned))
                    _oldToNew.Add(type, cloned = type.CloneAsUnfrozenWithoutDerived(model, baseType));
                return cloned;
            }
        }
    }
    /// <summary>
    /// Contains the stack-trace of the owning code when a lock-contention scenario is detected
    /// </summary>
    public sealed class LockContentedEventArgs : EventArgs
    {
        internal LockContentedEventArgs(string ownerStackTrace)
        {
            this.OwnerStackTrace = ownerStackTrace;
        }
        /// <summary>
        /// The stack-trace of the code that owned the lock when a lock-contention scenario occurred
        /// </summary>
        public string OwnerStackTrace { get; }
    }
    /// <summary>
    /// Event-type that is raised when a lock-contention scenario is detected
    /// </summary>
    public delegate void LockContentedEventHandler(object sender, LockContentedEventArgs args);

    public delegate bool AqlaPredicate<in T>(T x);
}