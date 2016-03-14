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

        ValueSerializationSettings _vs;
        ValueSerializationSettings _vsFinal;

        MethodInfo _getSpecified, _setSpecified;
        RuntimeTypeModel _model;

        volatile IProtoSerializerWithWireType _serializer;
        internal IProtoSerializerWithWireType Serializer => BuildSerializer();

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
        /// The default value of the item (members with this value will not be serialized)
        /// </summary>
        public object DefaultValue
        {
            get { return _vs.DefaultValue; }
            set
            {
                ThrowIfFrozen();
                if (!_canHaveDefaultValue) throw new ArgumentException("Member " + Name + " can't have a default value");
                _vs.DefaultValue = value;
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
            _vs = valueSerializationSettings.Clone();
            if (!canHaveDefaultValue && _vs.DefaultValue != null)
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

                _vsFinal = _vs.Clone();
                
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
                
#if !DEBUG
                try
#endif
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
                }
#if !DEBUG
                catch (ProtoException ex)
                {
                    throw new ProtoException(GetRethrowExceptionText(ex), ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(GetRethrowExceptionText(ex), ex);
                }
                catch (NotSupportedException ex)
                {
                    throw new InvalidOperationException(GetRethrowExceptionText(ex), ex);
                }
                catch (NotImplementedException ex)
                {
                    throw new NotImplementedException(GetRethrowExceptionText(ex), ex);
                }
                catch (ArgumentNullException ex)
                {
                    throw new ArgumentNullException(GetRethrowExceptionText(ex), ex);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new ArgumentOutOfRangeException(GetRethrowExceptionText(ex), ex);
                }
                catch (ArgumentException ex)
                {
#if SILVERLIGHT || PORTABLE
                    throw new ArgumentException(GetRethrowExceptionText(ex), ex);
#else
                    throw new ArgumentException(GetRethrowExceptionText(ex), ex.ParamName, ex);
#endif
                }
                catch (System.MissingMemberException ex)
                {
                    throw new System.MissingMemberException(GetRethrowExceptionText(ex), ex);
                }
                catch (MemberAccessException ex)
                {
                    throw new MemberAccessException(GetRethrowExceptionText(ex), ex);
                }
                catch (Exception ex)
                {
                    throw new ProtoException(GetRethrowExceptionText(ex), ex);
                }
#endif
            }
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
        }

        string GetRethrowExceptionText(Exception e)
        {
            return Member + ": " + (string.IsNullOrEmpty(e.Message) ? "serializer build problem" : e.Message);
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
            return _vs.GetSettingsCopy(level).Basic;
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
            _vs.SetSettings(value, level);
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
            ThrowIfFrozen();
            _vs.SetForAllLevels(setter);
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
            vm._vs = vm._vs.Clone();
            return vm;
        }

        internal sealed class Comparer : System.Collections.IComparer
#if !NO_GENERICS
, System.Collections.Generic.IComparer<ValueMember>
#endif
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