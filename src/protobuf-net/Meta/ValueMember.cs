// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq; using System.Linq;
using System.Threading;
using AqlaSerializer.Internal;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Represents a member (property/field) that is mapped to a protobuf field
    /// </summary>
    public class ValueMember
    {
        MemberMainSettingsValue _main;
        readonly bool _isAccessHandledOutside;
        readonly bool _canHaveDefaultValue;

        ValueSerializationSettings _vsByClient;
        ValueSerializationSettings _vsFinal;

        MethodInfo _getSpecified, _setSpecified;
        RuntimeTypeModel _model;

        volatile IProtoSerializerWithWireType _serializer;
        internal IProtoSerializerWithWireType Serializer => BuildSerializer();

        private MemberInfo backingMember;

        /// <summary>
        /// The number that identifies this member in a protobuf stream
        /// </summary>
        public int FieldNumber => _main.Tag;

        /// <summary>
        /// Gets the logical name for this member in the schema (this is not critical for binary serialization, but may be used
        /// when inferring a schema).
        /// </summary>
        public string Name => !string.IsNullOrEmpty(_main.Name) ? _main.Name : Member.Name;
        
        // TODO used only to disable WRITING default value but can just not specify default value at all!, remove this
        /// <summary>
        /// Indicates whether this field is mandatory.
        /// </summary>
        public bool IsRequired { get { return _main.IsRequiredInSchema; } set { ThrowIfFrozen(); _main.IsRequiredInSchema = value; } }

        /// <summary>
        /// Gets the backing member (field/property) which this member relates to
        /// </summary>
        public MemberInfo BackingMember
        {
            get { return backingMember; }
            set
            {
                if (backingMember != value)
                {
                    ThrowIfFrozen();
                    backingMember = value;
                }
            }
        }

        /// <summary>
        /// The default value of the item (members with this value will not be serialized)
        /// </summary>
        public object DefaultValue
        {
            get { return _vsByClient.DefaultValue; }
            set
            {
                ThrowIfFrozen();
                if (!_canHaveDefaultValue) throw new ArgumentException("Member " + Name + " can't have a default value");
                _vsByClient.DefaultValue = value;
            }
        }

        /// <summary>
        /// Gets the member (field/property) which this member relates to.
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// The underlying type of the member
        /// </summary>
        public Type MemberType { get; }

        /// <summary>
        /// The type the defines the member
        /// </summary>
        public Type ParentType { get; }
        
        /// <summary>
        /// Creates a new ValueMember instance
        /// </summary>
        public ValueMember(
            MemberMainSettingsValue memberSettings, ValueSerializationSettings valueSerializationSettings, MemberInfo member, Type parentType, RuntimeTypeModel model, bool isAccessHandledOutside = false, bool canHaveDefaultValue = true)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (parentType == null) throw new ArgumentNullException(nameof(parentType));
            if (model == null) throw new ArgumentNullException(nameof(model));
            MemberType = valueSerializationSettings.GetSettingsCopy(0).Basic.EffectiveType ?? Helpers.GetMemberType(member);
            Member = member;
            ParentType = parentType;
            _model = model;
            _canHaveDefaultValue = canHaveDefaultValue;
            _main = memberSettings;
            _isAccessHandledOutside = isAccessHandledOutside;
            _vsByClient = valueSerializationSettings.Clone();
            if (!canHaveDefaultValue && _vsByClient.DefaultValue != null)
                throw new ArgumentException("Default value was already set but the member " + Name + " can't have a default value", nameof(valueSerializationSettings));
        }

        internal class FinalizingSettingsArgs : EventArgs
        {
            public ValueMember Member { get; }

            public FinalizingSettingsArgs(ValueMember member)
            {
                Member = member;
            }
        }

        internal event EventHandler<FinalizingSettingsArgs> FinalizingSettings;

        IProtoSerializerWithWireType BuildSerializer()
        {
            if (_serializer != null) return _serializer;

            int opaqueToken = 0;
            try
            {
                // this lock is for whole model, not just this value member
                // so it should be taken anyway, right?
                _model.TakeLock(ref opaqueToken); // check nobody is still adding this type

                if (_serializer != null) return _serializer; // double check

                FinalizingSettings?.Invoke(this, new FinalizingSettingsArgs(this));

                if (FieldNumber < 1 && !Helpers.IsEnum(ParentType) && !_isAccessHandledOutside) throw new ProtoException("FieldNumber < 1 for member " + Member);

                if (Helpers.IsNullOrEmpty(_main.Name)) _main.Name = Member.Name;

                _vsFinal = _vsByClient.Clone();
                
                var level0 = _vsFinal.GetSettingsCopy(0);
                if (level0.Basic.EffectiveType == null)
                    level0.Basic.EffectiveType = MemberType;
                if (!level0.IsNotAssignable)
                    level0.IsNotAssignable = !Helpers.CanWrite(_model, Member);
                _vsFinal.SetSettings(level0, 0);
                
                if (_vsFinal.DefaultValue != null && _getSpecified != null)
                    throw new ProtoException("Can't use default value \"" + _vsFinal.DefaultValue + "\" on member with *Specified or ShouldSerialize check " + Member);

                if (_vsFinal.DefaultValue != null && IsRequired)
                    throw new ProtoException("Can't use default value \"" + _vsFinal.DefaultValue + "\" on Required member " + Member);

                if (_getSpecified == null && !IsRequired && _vsFinal.DefaultValue == null)
                {
                    if (_model.UseImplicitZeroDefaults)
                    {
                        switch (Helpers.GetTypeCode(MemberType))
                        {
                            case ProtoTypeCode.Boolean:
                                _vsFinal.DefaultValue = false;
                                break;
                            case ProtoTypeCode.Decimal:
                                _vsFinal.DefaultValue = (decimal)0;
                                break;
                            case ProtoTypeCode.Single:
                                _vsFinal.DefaultValue = (float)0;
                                break;
                            case ProtoTypeCode.Double:
                                _vsFinal.DefaultValue = (double)0;
                                break;
                            case ProtoTypeCode.Byte:
                                _vsFinal.DefaultValue = (byte)0;
                                break;
                            case ProtoTypeCode.Char:
                                _vsFinal.DefaultValue = (char)0;
                                break;
                            case ProtoTypeCode.Int16:
                                _vsFinal.DefaultValue = (short)0;
                                break;
                            case ProtoTypeCode.Int32:
                                _vsFinal.DefaultValue = (int)0;
                                break;
                            case ProtoTypeCode.Int64:
                                _vsFinal.DefaultValue = (long)0;
                                break;
                            case ProtoTypeCode.SByte:
                                _vsFinal.DefaultValue = (sbyte)0;
                                break;
                            case ProtoTypeCode.UInt16:
                                _vsFinal.DefaultValue = (ushort)0;
                                break;
                            case ProtoTypeCode.UInt32:
                                _vsFinal.DefaultValue = (uint)0;
                                break;
                            case ProtoTypeCode.UInt64:
                                _vsFinal.DefaultValue = (ulong)0;
                                break;
                            case ProtoTypeCode.TimeSpan:
                                _vsFinal.DefaultValue = TimeSpan.Zero;
                                break;
                            case ProtoTypeCode.Guid:
                                _vsFinal.DefaultValue = Guid.Empty;
                                break;
                        }
                    }
                }

                return Helpers.WrapExceptions(
                    () =>
                        {
                            WireType wt;
                            var ser = _model.ValueSerializerBuilder.BuildValueFinalSerializer(
                                _vsFinal,
                                true,
                                out wt);

                            if (!_isAccessHandledOutside)
                            {

                                PropertyInfo prop = Member as PropertyInfo;
                                if (prop != null)
                                    ser = new PropertyDecorator(_model, ParentType, (PropertyInfo)Member, ser);
                                else
                                {
                                    FieldInfo fld = Member as FieldInfo;
                                    if (fld != null)
                                        ser = new FieldDecorator(ParentType, (FieldInfo)Member, ser);
                                    else
                                        throw new InvalidOperationException();
                                }

                                if (_getSpecified != null || _setSpecified != null)
                                    ser = new MemberSpecifiedDecorator(_getSpecified, _setSpecified, ser);
                            }
                            _serializer = ser;

                            return ser;
                        }, e => Member + ": " + (string.IsNullOrEmpty(e.Message) ? "serializer build problem" : e.Message));
            }
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
        }

        #region Setting accessors

        public object GetFinalDefaultValue()
        {
            BuildSerializer();
            return _vsFinal.DefaultValue;
        }

        public MemberLevelSettingsValue[] GetFinalSettingsListCopy()
        {
            BuildSerializer();
            return _vsFinal.ExistingLevels.Select(x => x.Basic).ToArray();
        }

        public MemberLevelSettingsValue GetSettingsCopy(int level = 0)
        {
            return _vsByClient.GetSettingsCopy(level).Basic;
        }
        
        public MemberLevelSettingsValue GetFinalSettingsCopy(int level = 0)
        {
            BuildSerializer();
            return _vsFinal.GetSettingsCopy(level).Basic;
        }
        
        public void SetSettings(MemberLevelSettingsChangeDelegate func, int level = 0)
        {
            var v = new MemberLevelSettingsRef(GetSettingsCopy(level));
            func(v);
            SetSettings(v.V, level);
        }

        public void SetSettings(MemberLevelSettingsValue value, int level = 0)
        {
            ThrowIfFrozen();
            Helpers.MemoryBarrier();
            _vsByClient.SetSettings(value, level);
        }

        /// <summary>
        /// Allows to set Format for all levels
        /// </summary>
        [Obsolete("Use GetSettingsCopy/SetSettings and Format")]
        public bool AsReference
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            if (value)
                            {
                                if (x.Format != ValueFormat.LateReference)
                                    x.Format = ValueFormat.Reference;
                            }
                            else
                            {
                                x.Format = ValueFormat.Compact;
                            }
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set as ContentBinaryFormatHint for all levels; to set for specific level use <see cref="SetSettings"/>.
        /// </summary>
        [Obsolete("Use GetSettingsCopy/SetSettings and ContentBinaryFormatHint")]
        public BinaryDataFormat DataFormat
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.ContentBinaryFormatHint = value;
                            return x;
                        });
            }
        }
        
        /// <summary>
        /// Allows to set Collection.Append for all levels; to set for specific level use <see cref="SetSettings"/>.
        /// </summary>
        [Obsolete("May be not supported later; also use GetSettingsCopy/SetSettings and Collection.Append")]
        public bool AppendCollection
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.Append = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set CollectionFormat for all levels; to set for specific level use <see cref="SetSettings"/>.
        /// </summary>
        [Obsolete("Use GetSettingsCopy/SetSettings and Collection.Format")]
        public bool IsPacked
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.Format = value ? CollectionFormat.Protobuf : CollectionFormat.ProtobufNotPacked;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set Format for all levels; to set for specific level use <see cref="SetSettings"/>.
        /// </summary>
        [Obsolete("Use GetSettingsCopy/SetSettings and Format")]
        public bool SupportNull
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            if (value)
                            {
                                if (x.Format == ValueFormat.Compact || x.Format == ValueFormat.NotSpecified)
                                    x.Format = ValueFormat.MinimalEnhancement;
                            }
                            else if (x.Format == ValueFormat.MinimalEnhancement || x.Format == ValueFormat.NotSpecified)
                                x.Format = ValueFormat.Compact;
                            return x;
                        });
            }
        }
        
        void SetForAllLevels(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter)
        {
            _vsByClient.SetForAllLevels(setter, _serializer != null);
        }

#endregion

        internal object GetRawEnumValue()
        {
#if WINRT || PORTABLE || CF || FX11
            object value = ((FieldInfo)Member).GetValue(null);
            switch(Helpers.GetTypeCode(Enum.GetUnderlyingType(((FieldInfo)Member).FieldType)))
            {
                case ProtoTypeCode.SByte: return (sbyte)value;
                case ProtoTypeCode.Byte: return (byte)value;
                case ProtoTypeCode.Int16: return (short)value;
                case ProtoTypeCode.UInt16: return (ushort)value;
                case ProtoTypeCode.Int32: return (int)value;
                case ProtoTypeCode.UInt32: return (uint)value;
                case ProtoTypeCode.Int64: return (long)value;
                case ProtoTypeCode.UInt64: return (ulong)value;
                default:
                    throw new InvalidOperationException();
            }
#else
            return ((FieldInfo)Member).GetRawConstantValue();
#endif
        }
        /// <summary>
        /// Specifies the data-format that should be used for the value, when IsMap is enabled
        /// </summary>
        public DataFormat MapValueFormat
        {
            get { return mapValueFormat; }
            set
            {
                if (mapValueFormat != value)
                {
                    ThrowIfFrozen();
                    mapValueFormat = value;
                }
            }
        }
        /// <summary>
        /// Specifies the data-format that should be used for the key, when IsMap is enabled
        /// </summary>
        public DataFormat MapKeyFormat
        {
            get { return mapKeyFormat; }
            set
            {
                if (mapKeyFormat != value)
                {
                    ThrowIfFrozen();
                    mapKeyFormat = value;
                }
            }
        }

        private static bool IsValidMapKeyType(Type type)
        {
            if (type == null || Helpers.IsEnum(type)) return false;
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.Boolean:
                case ProtoTypeCode.Byte:
                case ProtoTypeCode.Char:
                case ProtoTypeCode.Int16:
                case ProtoTypeCode.Int32:
                case ProtoTypeCode.Int64:
                case ProtoTypeCode.String:

                case ProtoTypeCode.SByte:
                case ProtoTypeCode.UInt16:
                case ProtoTypeCode.UInt32:
                case ProtoTypeCode.UInt64:
                    return true;
            }
            return false;
        }

        internal bool ResolveMapTypes(out Type dictionaryType, out Type keyType, out Type valueType)
        {
            dictionaryType = keyType = valueType = null;
            try
            {
                var info = MemberType;
                if (ImmutableCollectionDecorator.IdentifyImmutable(MemberType, out _, out _, out _, out _, out _, out _))
                {
                    return false;
                }
                if (info.IsInterface && info.IsGenericType && info.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var typeArgs = MemberType.GetGenericArguments();
                    if (IsValidMapKeyType(typeArgs[0]))
                    {
                        keyType = typeArgs[0];
                        valueType = typeArgs[1];
                        dictionaryType = MemberType;
                    }
                    return false;
                }
                foreach (var iType in MemberType.GetInterfaces())
                {
                    info = iType;

                    if (info.IsGenericType && info.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    {
                        if (dictionaryType != null) throw new InvalidOperationException("Multiple dictionary interfaces implemented by type: " + MemberType.FullName);
                        var typeArgs = iType.GetGenericArguments();

                        if (IsValidMapKeyType(typeArgs[0]))
                        {
                            keyType = typeArgs[0];
                            valueType = typeArgs[1];
                            dictionaryType = MemberType;
                        }
                    }
                }
                if (dictionaryType == null) return false;

                // (note we checked the key type already)
                // not a map if value is repeated
                Type itemType = null, defaultType = null;
                model.ResolveListTypes(valueType, ref itemType, ref defaultType);
                if (itemType != null) return false;

                return dictionaryType != null;
            }
            catch
            {
                // if it isn't a good fit; don't use "map"
                return false;
            }
        }

        private DataFormat mapKeyFormat, mapValueFormat;

        /// <summary>
        /// Indicates that the member should be treated as a protobuf Map
        /// </summary>
        public bool IsMap
        {
            get { return HasFlag(OPTIONS_IsMap); }
            set { SetFlag(OPTIONS_IsMap, value, true); }
        }

        /// <summary>
        /// Specifies methods for working with optional data members.
        /// </summary>
        /// <param name="getSpecified">Provides a method (null for none) to query whether this member should
        /// be serialized; it must be of the form "bool {Method}()". The member is only serialized if the
        /// method returns true.</param>
        /// <param name="setSpecified">Provides a method (null for none) to indicate that a member was
        /// deserialized; it must be of the form "void {Method}(bool)", and will be called with "true"
        /// when data is found.</param>
        public void SetSpecified(MethodInfo getSpecified, MethodInfo setSpecified)
        {
            if (_isAccessHandledOutside) throw new InvalidOperationException("Member access is handled from outside");
            if (getSpecified != null)
            {
                if (getSpecified.ReturnType != _model.MapType(typeof(bool))
                    || getSpecified.IsStatic
                    || getSpecified.GetParameters().Length != 0)
                {
                    throw new ArgumentException("Invalid pattern for checking member-specified", nameof(getSpecified));
                }
            }
            if (setSpecified != null)
            {
                ParameterInfo[] args;
                if (setSpecified.ReturnType != _model.MapType(typeof(void))
                    || setSpecified.IsStatic
                    || (args = setSpecified.GetParameters()).Length != 1
                    || args[0].ParameterType != _model.MapType(typeof(bool)))
                {
                    throw new ArgumentException("Invalid pattern for setting member-specified", nameof(setSpecified));
                }
            }
            ThrowIfFrozen();
            this._getSpecified = getSpecified;
            this._setSpecified = setSpecified;

        }

        void ThrowIfFrozen()
        {
            if (_serializer != null) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
        }
        
        public override string ToString()
        {
            return Member.ToString();
        }

        internal string GetSchemaTypeName(bool applyNetObjectProxy, ref bool requiresBclImport)
        {
            var s = Serializer;
            s.GetHashCode();
            Type effectiveType = GetFinalSettingsCopy().Collection.ItemType ?? MemberType;
            return _model.GetSchemaTypeName(
                effectiveType,
                GetFinalSettingsCopy().ContentBinaryFormatHint.GetValueOrDefault(),
                applyNetObjectProxy && GetFinalSettingsCopy().Format == ValueFormat.Reference,
                applyNetObjectProxy && GetFinalSettingsCopy().WriteAsDynamicType.GetValueOrDefault(),
                ref requiresBclImport);
        }

        internal ValueMember CloneAsUnfrozen(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var vm = (ValueMember)MemberwiseClone();
            vm._model = model;
            vm._serializer = null;
            vm._vsFinal = null;
            vm.FinalizingSettings = null;
            vm._vsByClient = vm._vsByClient.Clone();
            return vm;
        }

        internal sealed class Comparer : System.Collections.IComparer, IComparer<ValueMember>
        {
            public static readonly ValueMember.Comparer Default = new Comparer();
            public int Compare(object x, object y)
            {
                return Compare(x as ValueMember, y as ValueMember);
            }
            public int Compare(ValueMember x, ValueMember y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return x.FieldNumber.CompareTo(y.FieldNumber);
            }
        }
    }
}
#endif