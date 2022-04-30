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
        internal static readonly System.Type ienumerable = typeof(IEnumerable);

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

        /// <summary>
        /// The runtime type that the meta-type represents
        /// </summary>
        public Type Type { get; }

        bool _isFrozen;
#if !PORTABLE
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
#if DEBUG && !PORTABLE
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
#if DEBUG && !PORTABLE
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


    }
}
#endif
