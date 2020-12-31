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

        void AddTupleField(ValueMember vm)
        {
            vm.FinalizingSettings += (s, a) => FinalizingMemberSettings?.Invoke(this, a);
            _tupleFields.Add(vm);
        }

        internal int GetKey(bool demand, bool getBaseKey)
        {
            return _model.GetKey(Type, demand, getBaseKey);
        }

        public void ApplyDefaultBehaviour()
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
                return builder.Append("]");
            }
            return builder;
        }

        private static bool IsImplicitDefault(object value)
        {
            try
            {
                if (value == null) return false;
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
                    case ProtoTypeCode.String: return value != null && ((string)value).Length == 0;
                    case ProtoTypeCode.TimeSpan: return ((TimeSpan)value) == TimeSpan.Zero;
                    case ProtoTypeCode.UInt16: return ((ushort)value) == 0;
                    case ProtoTypeCode.UInt32: return ((uint)value) == 0;
                    case ProtoTypeCode.UInt64: return ((ulong)value) == 0;
                }
            }
            catch { }
            return false;
        }

        private static bool CanPack(Type type)
        {
            if (type == null) return false;
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
                return surrogate != null;
            }
        }


    }
}
#endif
