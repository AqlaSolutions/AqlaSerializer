using ProtoBuf.Internal;
using ProtoBuf.Internal.Serializers;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
using System.Text;
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
    /// <summary>
    /// Represents a type at runtime for use with protobuf, allowing the field mappings (etc) to be defined
    /// </summary>
    public partial class MetaType : ISerializerProxy
    {
#if WINRT
        internal static readonly TypeInfo ienumerable = typeof(IEnumerable).GetTypeInfo();
#else
        internal static readonly System.Type ienumerable = typeof(IEnumerable);
#endif

        private RuntimeTypeModel _model;
        internal TypeModel Model => _model;
        
        internal bool IsList
        {
            get
            {
                Type itemType = IgnoreListHandling ? null : TypeModel.GetListItemType(_model, Type);
                return itemType != null;
            }
        }

        bool _isDefaultBehaviourApplied;

        #if WINRT
                private readonly TypeInfo _typeInfo;
        #endif
                /// <summary>
                /// The runtime type that the meta-type represents
                /// </summary>
                public Type Type { get; }

        bool _isFrozen;
#if !WINRT && !PORTABLE && !SILVERLIGHT
        StackTrace _freezeStackTrace;
#endif
        internal bool IsFrozen
        {
            get
            {
                return _isFrozen;
            }
            private set
            {
#if DEBUG && !WINRT && !PORTABLE && !SILVERLIGHT
                if (value && !_isFrozen)
                    _freezeStackTrace = new StackTrace(false);
#endif
                _isFrozen = value;
            }
        }

        bool _isPending;
        
        internal bool IsPending
        {
            get { return _isPending; }
            set
            {
                if (value && !_isPending) ThrowIfFrozen();
                _isPending = value;
            }
        }

        List<ValueMember> _tupleFields = new List<ValueMember>();
        readonly ConstructorInfo _tupleCtor;

        internal MetaType(RuntimeTypeModel model, Type type, MethodInfo factory)
        {
            this._factory = factory;
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (type == null) throw new ArgumentNullException(nameof(type));

            this._model = model;

            IProtoSerializer coreSerializer = model.TryGetBasicTypeSerializer(type);
            if (coreSerializer != null)
            {
                throw InbuiltType(type);
            }

            this.Type = type;

#if WINRT
            this._typeInfo = type.GetTypeInfo();
#endif

            MemberInfo[] members;

            _tupleCtor = ResolveTupleConstructor(Type, out members);
            if (_tupleCtor != null)
            {
                foreach (MemberInfo memberInfo in members)
                {
                    var level = new MemberLevelSettingsValue();
                    var vs = new ValueSerializationSettings();
                    vs.SetSettings(new ValueSerializationSettings.LevelValue(level) { IsNotAssignable = true }, 0);
                    vs.DefaultLevel = new ValueSerializationSettings.LevelValue(level.MakeDefaultNestedLevel());
                    var main = new MemberMainSettingsValue() { Name = memberInfo.Name };
                    var vm = new ValueMember(main, vs, memberInfo, Type, model, canHaveDefaultValue: false, isAccessHandledOutside: true);
                    AddTupleField(vm);
                }
            }
        }

        private CompatibilityLevel _compatibilityLevel;

        void AddTupleField(ValueMember vm)
        {
            vm.FinalizingSettings += (s, a) => FinalizingMemberSettings?.Invoke(this, a);
            _tupleFields.Add(vm);
        }

        /// <summary>
        /// Gets or sets the <see cref="MetaType.CompatibilityLevel"/> for this instance
        /// </summary>
        public CompatibilityLevel CompatibilityLevel
        {
            get => _compatibilityLevel;
            set
            {
                if (value != _compatibilityLevel)
                {
                    if (HasFields) ThrowHelper.ThrowInvalidOperationException($"{CompatibilityLevel} cannot be set once fields have been defined");
                    CompatibilityLevelAttribute.AssertValid(value);
                    _compatibilityLevel = value;
                }
            }
        }

        private List<SubType> _subTypes;

        internal int GetKey(bool demand, bool getBaseKey)
        {
            return _model.GetKey(Type, demand, getBaseKey);
        }

        public void ApplyDefaultBehaviourImpl()
        {
            // AddSubType is not thread safe too, so what?
            if (_isDefaultBehaviourApplied) return;
            _isDefaultBehaviourApplied = true;
            _model.AutoAddStrategy.ApplyDefaultBehaviour(this);
        }


        private MethodInfo _factory;
        
        /// <summary>
        /// Designate a factory-method to use to create instances of this type
        /// </summary>
        public MetaType SetFactory(MethodInfo factory)
        {
            _model.VerifyFactory(factory, Type);
            ThrowIfFrozen();
            this._factory = factory;
            return this;
        }

        private static void ThrowTupleTypeWithInheritance(Type type)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Tuple-based types cannot be used in inheritance hierarchies: {type.NormalizeName()}");
        }
        
        /// <summary>
        /// Returns the public Type name of this Type used in serialization
        /// </summary>
        public string GetSchemaTypeName() => GetSchemaTypeName(null);

        internal string GuessPackage()
        {   // very speculative; turns .Foo.Bar.Blap into Foo.Bar
            var s = Name;
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (s[0] != '.') return null; // not fully qualified
            var idx = s.LastIndexOf('.');
            return s.Substring(0, idx).Trim('.').Trim();
        }

        /// <summary>
        /// Gets or sets the file that defines this type (as used with <c>import</c> in .proto)
        /// </summary>
        public string Origin
        {
            get => origin;
            set
            {
                ThrowIfFrozen();
                origin = value;
            }
        }

        private static void ThrowSubTypeWithSurrogate(Type type)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Types with surrogates cannot be used in inheritance hierarchies: {type.NormalizeName()}");
        }

        /// <summary>
        /// Performs serialization of this type via a surrogate; all
        /// other serialization options are ignored and handled
        /// by the surrogate's configuration.
        /// </summary>
        public void SetSurrogate(Type surrogateType)
            => SetSurrogate(surrogateType, null, null, DataFormat.Default);

        internal static bool IsValidEnum(IList<EnumMember> values)
        {
            if (values is null || values.Count == 0) return false;
            foreach(var val in values)
            {
                if (!val.TryGetInt32().HasValue) return false;
            }
            return true;
        }

        internal bool IsValidEnum() => IsValidEnum(_enums);

        /// <summary>
        /// Add a new defined name/value pair for an enum
        /// </summary>
        public void SetEnumValues(EnumMember[] values)
        {
            if (!Type.IsEnum) ThrowHelper.ThrowInvalidOperationException($"Only enums should use {nameof(SetEnumValues)}");

            if (values is null) ThrowHelper.ThrowArgumentNullException(nameof(values));

            var typedClone = Array.ConvertAll(values, val => val.Normalize(Type));

            foreach (var val in values)
                val.Validate();

            int opaqueToken = 0;
            try
            {
                model.TakeLock(ref opaqueToken);
                ThrowIfFrozen();
                Enums.Clear();
                Enums.AddRange(typedClone);
            }
            finally
            {
                model.ReleaseLock(opaqueToken);
            }
        }

        /// <summary>
        /// Returns the EnumMember instances associated with this type
        /// </summary>
        public EnumMember[] GetEnumValues()
        {
            if (!HasEnums) return Array.Empty<EnumMember>();
            return Enums.ToArray();
        }

        /// <summary>
        /// Compiles the serializer for this type; this is *not* a full
        /// standalone compile, but can significantly boost performance
        /// while allowing additional types to be added.
        /// </summary>
        /// <remarks>An in-place compile can access non-public types / members</remarks>
        public void CompileInPlace()
        {
            var original = Serializer; // might lazily create
            if (original is ICompiledSerializer || original.ExpectedType.IsEnum || model.TryGetRepeatedProvider(Type) is object)
                return; // nothing to do
            
            var wrapped = CompiledSerializer.Wrap(original, model);
            if (!ReferenceEquals(original, wrapped))
            {
                _serializer = (IProtoTypeSerializer) wrapped;
                Model.ResetServiceCache(Type);
            }
            
        }
        internal bool HasEnums => _enums is object && _enums.Count != 0;
        internal List<EnumMember> Enums => _enums ??= new List<EnumMember>();

        private List<EnumMember> _enums = new List<EnumMember>();

        /// <summary>
        /// Gets or sets a value indicating whether unknown sub-types should cause serialization failure
        /// </summary>
        public bool IgnoreUnknownSubTypes
        {
            get => HasFlag(TypeOptions.IgnoreUnknownSubTypes);
            set => SetFlag(TypeOptions.IgnoreUnknownSubTypes, value, true);
        }
        internal bool HasFields => _fields is object && _fields.Count != 0;

        private enum TypeOptions : ushort
        {
            None = 0,
            Pending = 1,
            // EnumPassThru = 2,
            Frozen = 4,
            PrivateOnApi = 8,
            SkipConstructor = 16,
#if FEAT_DYNAMIC_REF
            AsReferenceDefault = 32,
#endif
            AutoTuple = 64,
            IgnoreListHandling = 128,
            IsGroup = 256,
            IgnoreUnknownSubTypes = 512,
        }

        private List<ValueMember> _fields = null;
        private MethodInfo underlyingToSurrogate, surrogateToUnderlying;
        internal DataFormat surrogateDataFormat;

        internal Type surrogateType;

        /// <summary>
        /// Specify a custom serializer for this type
        /// </summary>
        public Type SerializerType
        {
            get => _serializerType;
            set
            {
                if (value != _serializerType)
                {
                    if (!value.IsClass)
                        ThrowHelper.ThrowArgumentException("Custom serializer providers must be classes", nameof(SerializerType));
                    ThrowIfFrozen();
                    _serializerType = value;
                }
            }
        }

        internal void Assert(CompatibilityLevel expected)
        {
            var actual = CompatibilityLevel;
            if (actual == expected) return;

            ThrowHelper.ThrowInvalidOperationException($"The expected ('{expected}') and actual ('{actual}') compatibility level of '{Type.NormalizeName()}' did not match; the same type cannot be used with different compatibility levels in the same model; this is most commonly an issue with tuple-like types in different contexts");
        }

        internal void ApplyDefaultBehaviour(CompatibilityLevel ambient)
        {
            TypeAddedEventArgs args = null; // allows us to share the event-args between events
            RuntimeTypeModel.OnBeforeApplyDefaultBehaviour(this, ref args);
            if (args is null || args.ApplyDefaultBehaviour) ApplyDefaultBehaviourImpl(ambient);
            RuntimeTypeModel.OnAfterApplyDefaultBehaviour(this, ref args);
        }

        private bool HasRealInheritance()
            => (baseType is object && baseType != this) || (_subTypes?.Count ?? 0) > 0;

        private SerializerFeatures GetFeatures()
        {
            if (Type.IsEnum) return SerializerFeatures.WireTypeVarint | SerializerFeatures.CategoryScalar;

            if (!Type.IsValueType)
            {
                var bt = GetRootType(this);
                if (!ReferenceEquals(bt, this)) return bt.GetFeatures();
            }
            var features = SerializerFeatures.CategoryMessage;
            features |= IsGroup ? SerializerFeatures.WireTypeStartGroup : SerializerFeatures.WireTypeString;
            return features;
        }

        internal Type GetInheritanceRoot()
        {
            if (Type.IsValueType) return null;

            var root = GetRootType(this);
            if (!ReferenceEquals(root, this)) return root.Type;
            if (_subTypes is object && _subTypes.Count != 0) return root.Type;

            return null;
        }
         

        /// <summary>
        /// Designate a factory-method to use to create instances of this type
        /// </summary>
        public MetaType SetFactory(string factory)
        {
            return SetFactory(ResolveMethod(factory, false));
        }

        /// <summary>
        /// Derived types are not cloned!
        /// </summary>
        internal MetaType CloneAsUnfrozenWithoutDerived(RuntimeTypeModel model, MetaType baseType)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var mt = (MetaType)MemberwiseClone();
            mt._serializer = null;
            mt._rootSerializer = null;
            mt._model = model;
            mt._settingsValueFinalSet = false;
            mt._subTypes = new BasicList();
            mt._subTypesSimple = new BasicList();
            mt.FinalizingOwnSettings = null;
            mt.FinalizingMemberSettings = null;
            mt.IsFrozen = false;
            mt.IsPending = false;
            mt.BaseType = baseType;
            mt._fields = new BasicList();
            mt._rootNestedVs = _rootNestedVs.Clone();
            mt._settingsValueFinal = new TypeSettingsValue();
            foreach (ValueMember field in _fields.Cast<ValueMember>().Select(f => (object)f.CloneAsUnfrozen(model)))
                mt.Add(field);
            mt._tupleFields = new List<ValueMember>();
            foreach (ValueMember vm in _tupleFields.Select(f => f.CloneAsUnfrozen(model)))
                mt.AddTupleField(vm);
            return mt;
        }

		/// <summary>
		/// Throws an exception if the type has been made immutable
		/// </summary>
		protected internal void ThrowIfFrozen()
		{
		    if (IsFrozen) ThrowFrozen();
		}
		
        void ThrowFrozen()
        {
#if DEBUG && !WINRT && !PORTABLE && !SILVERLIGHT
            Debug.WriteLine("Frozen at " + _freezeStackTrace);
#endif
            throw new InvalidOperationException("The type cannot be changed once a serializer has been generated for " + Type.FullName + " or once its settings were used for generating member serializer");
        }


        /// <summary>
        /// Get the name of the type being represented
        /// </summary>
        public override string ToString()
        {
            return Type.ToString();
        }

        /// <summary>
        /// Apply a shift to all fields (and sub-types) on this type
        /// </summary>
        /// <param name="offset">The change in field number to apply</param>
        /// <remarks>The resultant field numbers must still all be considered valid</remarks>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
        public void ApplyFieldOffset(int offset)
        {
            if (Type.IsEnum) throw new InvalidOperationException("Cannot apply field-offset to an enum");
            if (offset == 0) return; // nothing to do
            int opaqueToken = 0;
            try
            {
                model.TakeLock(ref opaqueToken);
                ThrowIfFrozen();

                var fields = _fields;
                var subTypes = _subTypes;
                if (fields is object)
                {
                    foreach (ValueMember field in fields)
                        AssertValidFieldNumber(field.FieldNumber + offset);
                }
                if (subTypes is object)
                {
                    foreach (SubType subType in subTypes)
                        AssertValidFieldNumber(subType.FieldNumber + offset);
                }

                // we've checked the ranges are all OK; since we're moving everything, we can't overlap ourselves
                // so: we can just move
                if (fields is object)
                {
                    foreach (ValueMember field in fields)
                        field.FieldNumber += offset;
                }
                if (subTypes is object)
                {
                    foreach (SubType subType in subTypes)
                        subType.FieldNumber += offset;
                }
            }
            finally
            {
                model.ReleaseLock(opaqueToken);
            }
        }

        internal static void AssertValidFieldNumber(int fieldNumber)
        {
            if (fieldNumber < 1) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
        }

        /// <summary>
        /// Adds a single number field reservation
        /// </summary>
        public MetaType AddReservation(int field, string comment = null)
            => AddReservation(new ProtoReservedAttribute(field, comment));
        /// <summary>
        /// Adds range number field reservation
        /// </summary>
        public MetaType AddReservation(int from, int to, string comment = null)
            => AddReservation(new ProtoReservedAttribute(from, to, comment));
        /// <summary>
        /// Adds a named field reservation
        /// </summary>
        public MetaType AddReservation(string field, string comment = null)
            => AddReservation(new ProtoReservedAttribute(field, comment));
        private MetaType AddReservation(ProtoReservedAttribute reservation)
        {
            reservation.Verify();
            int opaqueToken = default;
            try
            {
                model.TakeLock(ref opaqueToken);
                ThrowIfFrozen();
                _reservations ??= new List<ProtoReservedAttribute>();
                _reservations.Add(reservation);
            }
            finally
            {
                model.ReleaseLock(opaqueToken);
            }
            return this;
        }

        private List<ProtoReservedAttribute> _reservations;

        internal bool HasReservations => (_reservations?.Count ?? 0) != 0;

        internal void Validate() => ValidateReservations(); // just this for now, but: in case we need more later

        internal void ValidateReservations()
        {
            if (!(HasReservations && (HasFields || HasSubtypes || HasEnums))) return;

            foreach (var reservation in _reservations)
            {
                if (reservation.From != 0)
                {
                    if (_fields is object)
                    {
                        foreach (var field in _fields)
                        {
                            if (field.FieldNumber >= reservation.From && field.FieldNumber <= reservation.To)
                            {
                                throw new InvalidOperationException($"Field {field.FieldNumber} is reserved and cannot be used for data member '{field.Name}'{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                    if (_enums is object)
                    {
                        foreach (var @enum in _enums)
                        {
                            var val = @enum.TryGetInt32();
                            if (val.HasValue && val.Value >= reservation.From && val.Value <= reservation.To)
                            {
                                throw new InvalidOperationException($"Field {val.Value} is reserved and cannot be used for enum value '{@enum.Name}'{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                    if (_subTypes is object)
                    {
                        foreach (var subType in _subTypes)
                        {
                            if (subType.FieldNumber >= reservation.From && subType.FieldNumber <= reservation.To)
                            {
                                throw new InvalidOperationException($"Field {subType.FieldNumber} is reserved and cannot be used for sub-type '{subType.DerivedType.Type.NormalizeName()}'{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                }
                else
                {
                    if (_fields is object)
                    {
                        foreach (var field in _fields)
                        {
                            if (field.Name == reservation.Name)
                            {
                                throw new InvalidOperationException($"Field '{field.Name}' is reserved and cannot be used for data member {field.FieldNumber}{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                    if (_enums is object)
                    {
                        foreach (var @enum in _enums)
                        {
                            if (@enum.Name == reservation.Name)
                            {
                                throw new InvalidOperationException($"Field '{@enum.Name}' is reserved and cannot be used for enum value {@enum.Value}{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                    if (_subTypes is object)
                    {
                        foreach (var subType in _subTypes)
                        {
                            var name = subType.DerivedType.Name;
                            if (string.IsNullOrWhiteSpace(name)) name = subType.DerivedType.Type.Name;
                            if (name == reservation.Name)
                            {
                                throw new InvalidOperationException($"Field '{name}' is reserved and cannot be used for sub-type {subType.FieldNumber}{CommentSuffix(reservation)}.");
                            }
                        }
                    }
                }
            }

            static string CommentSuffix(ProtoReservedAttribute reservation)
            {
                var comment = reservation.Comment;
                if (string.IsNullOrWhiteSpace(comment)) return "";
                return " (" + comment.Trim() + ")";
            }
        }

        [Flags]
        public enum AttributeFamily
        {
            None = 0, ProtoBuf = 1, DataContractSerialier = 2, XmlSerializer = 4, AutoTuple = 8, Aqla = 16, ImplicitFallback = 32, SystemSerializable = 64
        }

        private static IEnumerable<Type> GetAllGenericArguments(Type type)
        {
            var genericArguments = type.GetGenericArguments();
            foreach (var arg in genericArguments)
            {
                yield return arg;
                foreach (var inner in GetAllGenericArguments(arg))
                {
                    yield return inner;
                }
            }
        }


        internal IEnumerable<Type> GetAllGenericArguments()
        {
            return GetAllGenericArguments(Type);
        }

        private static StringBuilder AddOption(StringBuilder builder, ref bool hasOption)
        {
            if (hasOption)
                return builder.Append(", ");
            hasOption = true;
            return builder.Append(" [");
        }

        private static StringBuilder CloseOption(StringBuilder builder, ref bool hasOption)
        {
            if (hasOption)
            {
                hasOption = false;
                return builder.Append(']');
            }
            return builder;
        }

        private static bool IsImplicitDefault(object value)
        {
            try
            {
                if (value is null) return false;
                switch (Helpers.GetTypeCode(value.GetType()))
                {
                    case ProtoTypeCode.Boolean: return !(bool)value;
                    case ProtoTypeCode.Byte: return ((byte)value) == 0;
                    case ProtoTypeCode.Char: return ((char)value) == '\0';
                    case ProtoTypeCode.DateTime: return ((DateTime)value) == default;
                    case ProtoTypeCode.Decimal: return ((decimal)value) == 0M;
                    case ProtoTypeCode.Double: return ((double)value) == 0;
                    case ProtoTypeCode.Int16: return ((short)value) == 0;
                    case ProtoTypeCode.Int32: return ((int)value) == 0;
                    case ProtoTypeCode.Int64: return ((long)value) == 0;
                    case ProtoTypeCode.SByte: return ((sbyte)value) == 0;
                    case ProtoTypeCode.Single: return ((float)value) == 0;
                    case ProtoTypeCode.String: return value is object && ((string)value).Length == 0;
                    case ProtoTypeCode.TimeSpan: return ((TimeSpan)value) == TimeSpan.Zero;
                    case ProtoTypeCode.UInt16: return ((ushort)value) == 0;
                    case ProtoTypeCode.UInt32: return ((uint)value) == 0;
                    case ProtoTypeCode.UInt64: return ((ulong)value) == 0;
                }
            }
            catch { }
            return false;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "Readability")]
        private static bool CanPack(Type type)
        {
            if (type is null) return false;
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.Boolean:
                case ProtoTypeCode.Byte:
                case ProtoTypeCode.Char:
                case ProtoTypeCode.Double:
                case ProtoTypeCode.Int16:
                case ProtoTypeCode.Int32:
                case ProtoTypeCode.Int64:
                case ProtoTypeCode.SByte:
                case ProtoTypeCode.Single:
                case ProtoTypeCode.UInt16:
                case ProtoTypeCode.UInt32:
                case ProtoTypeCode.UInt64:
                    return true;
            }
            return false;
        }

        internal bool HasSurrogate
        {
            get
            {
                return surrogateType is object;
            }
        }


    }
}
