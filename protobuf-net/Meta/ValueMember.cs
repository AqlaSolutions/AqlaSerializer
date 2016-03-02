// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq;
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

        ValueSerializationSettings _vs;

        MethodInfo getSpecified, setSpecified;
        RuntimeTypeModel _model;

        private volatile IProtoSerializerWithWireType _serializer;
        internal IProtoSerializerWithWireType Serializer => _serializer ?? (_serializer = BuildSerializer());

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
        public object DefaultValue { get { return _vs.DefaultValue; } set { ThrowIfFrozen(); _vs.DefaultValue = value; } }

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
            MemberMainSettingsValue memberSettings, ValueSerializationSettings valueSerializationSettings, MemberInfo member, Type parentType, RuntimeTypeModel model)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (parentType == null) throw new ArgumentNullException(nameof(parentType));
            if (model == null) throw new ArgumentNullException(nameof(model));
            MemberType = valueSerializationSettings.GetSettingsCopy(0).Basic.EffectiveType ?? Helpers.GetMemberType(member);
            Member = member;
            ParentType = parentType;
            _model = model;
            _main = memberSettings;
            _vs = valueSerializationSettings.Clone();
            // this doesn't depend on anything so can check right now 
            if (MemberType.IsArray && MemberType.GetArrayRank() != 1)
                throw new NotSupportedException("Multi-dimension arrays are not supported");
        }

        IProtoSerializerWithWireType BuildSerializer()
        {
            int opaqueToken = 0;
            try
            {
                _model.TakeLock(ref opaqueToken); // check nobody is still adding this type

                if (FieldNumber < 1 && !Helpers.IsEnum(ParentType)) throw new ProtoException("FieldNumber < 1 for member " + Member);

                if (Helpers.IsNullOrEmpty(_main.Name)) _main.Name = Member.Name;

                var level0 = _vs.GetSettingsCopy(0);
                level0.Basic.EffectiveType = MemberType;
                level0.IsNotAssignable = !Helpers.CanWrite(_model, Member);
                _vs.SetSettings(level0, 0);
                
                if (_vs.DefaultValue != null && getSpecified != null)
                    throw new ProtoException("Can't use default value \"" + _vs.DefaultValue + "\" on member with *Specified or ShouldSerialize check " + Member);

                if (_vs.DefaultValue != null && IsRequired)
                    throw new ProtoException("Can't use default value \"" + _vs.DefaultValue + "\" on Required member " + Member);

                if (getSpecified == null && !IsRequired && _vs.DefaultValue == null)
                {
                    if (_model.UseImplicitZeroDefaults)
                    {
                        switch (Helpers.GetTypeCode(MemberType))
                        {
                            case ProtoTypeCode.Boolean:
                                _vs.DefaultValue = false;
                                break;
                            case ProtoTypeCode.Decimal:
                                _vs.DefaultValue = (decimal)0;
                                break;
                            case ProtoTypeCode.Single:
                                _vs.DefaultValue = (float)0;
                                break;
                            case ProtoTypeCode.Double:
                                _vs.DefaultValue = (double)0;
                                break;
                            case ProtoTypeCode.Byte:
                                _vs.DefaultValue = (byte)0;
                                break;
                            case ProtoTypeCode.Char:
                                _vs.DefaultValue = (char)0;
                                break;
                            case ProtoTypeCode.Int16:
                                _vs.DefaultValue = (short)0;
                                break;
                            case ProtoTypeCode.Int32:
                                _vs.DefaultValue = (int)0;
                                break;
                            case ProtoTypeCode.Int64:
                                _vs.DefaultValue = (long)0;
                                break;
                            case ProtoTypeCode.SByte:
                                _vs.DefaultValue = (sbyte)0;
                                break;
                            case ProtoTypeCode.UInt16:
                                _vs.DefaultValue = (ushort)0;
                                break;
                            case ProtoTypeCode.UInt32:
                                _vs.DefaultValue = (uint)0;
                                break;
                            case ProtoTypeCode.UInt64:
                                _vs.DefaultValue = (ulong)0;
                                break;
                            case ProtoTypeCode.TimeSpan:
                                _vs.DefaultValue = TimeSpan.Zero;
                                break;
                            case ProtoTypeCode.Guid:
                                _vs.DefaultValue = Guid.Empty;
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
                        _vs,
                        true,
                        out wt);

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
                    if (getSpecified != null || setSpecified != null)
                        ser = new MemberSpecifiedDecorator(getSpecified, setSpecified, ser);

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
                    throw new ArgumentException(GetRethrowExceptionText(ex), ex.ParamName, ex);
                }
                catch (MissingMethodException ex)
                {
                    throw new MissingMethodException(GetRethrowExceptionText(ex), ex);
                }
                catch (MissingFieldException ex)
                {
                    throw new MissingFieldException(GetRethrowExceptionText(ex), ex);
                }
                catch (MissingMemberException ex)
                {
                    throw new MissingMemberException(GetRethrowExceptionText(ex), ex);
                }
                catch (FieldAccessException ex)
                {
                    throw new FieldAccessException(GetRethrowExceptionText(ex), ex);
                }
                catch (MethodAccessException ex)
                {
                    throw new MethodAccessException(GetRethrowExceptionText(ex), ex);
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
        public MemberLevelSettingsValue GetSettingsCopy(int level = 0)
        {
            return _vs.GetSettingsCopy(level).Basic;
        }
        
        public void SetSettings(MemberLevelSettingsValue value, int level = 0)
        {
            SetSettings(value, level, false);
        }

        void SetSettings(MemberLevelSettingsValue value, int level, bool bypassFrozenCheck)
        {
            if (!bypassFrozenCheck) ThrowIfFrozen();
            _vs.SetSettings(value, level);
        }

        /// <summary>
        /// Allows to set EnhancedFormat and EnhancedWriteMode for all levels
        /// </summary>
        [Obsolete("Use EnhancedFormat and EnhancedWriteMode")]
        public bool AsReference
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            if (value)
                            {
                                x.EnhancedFormat = true;
                                if (x.EnhancedWriteMode != EnhancedMode.LateReference)
                                    x.EnhancedWriteMode = EnhancedMode.Reference;
                            }
                            else
                            {
                                x.EnhancedWriteMode = EnhancedMode.Minimal;
                            }
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set as ContentBinaryFormatHint for all levels
        /// </summary>
        [Obsolete("Use ContentBinaryFormatHint")]
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
        /// Allows to set as ContentBinaryFormatHint for all levels
        /// </summary>
        public BinaryDataFormat ContentBinaryFormatHint
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
        /// Allows to set Collection.Append for all levels
        /// </summary>
        [Obsolete("May be not supported later")]
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
        /// Allows to set CollectionFormat for all levels
        /// </summary>
        [Obsolete("Use CollectionFormat")]
        public bool IsPacked
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.Format = value ? CollectionFormat.Google : CollectionFormat.GoogleNotPacked;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set CollectionFormat for all levels
        /// </summary>
        public CollectionFormat CollectionFormat
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.Format = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set Collection.ItemType for all levels
        /// </summary>
        public Type ItemType
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.ItemType = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set CollectionConcreteType for all levels
        /// </summary>
        public Type CollectionConcreteType
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.Collection.ConcreteType = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set enchanced format for all levels
        /// </summary>
        public bool EnhancedFormat
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.EnhancedFormat = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set enchanced write mode for all levels
        /// </summary>
        public EnhancedMode EnhancedWriteMode
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            x.EnhancedWriteMode = value;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set enchanced mode for all levels
        /// </summary>
        [Obsolete("Use EnhancedFormat and EnhancedWriteMode")]
        public bool SupportNull
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            if (value)
                                x.EnhancedFormat = true;
                            else if (x.EnhancedWriteMode == EnhancedMode.NotSpecified || x.EnhancedWriteMode != EnhancedMode.LateReference)
                                x.EnhancedFormat = false;
                            return x;
                        });
            }
        }

        /// <summary>
        /// Allows to set dynamic type and enhanced format for all levels
        /// </summary>
        public bool DynamicType
        {
            set
            {
                SetForAllLevels(
                    x =>
                        {
                            if (value)
                            {
                                x.EnhancedFormat = true;
                                x.WriteAsDynamicType = true;
                                if (x.EnhancedWriteMode == EnhancedMode.LateReference)
                                    x.EnhancedWriteMode = EnhancedMode.Reference;
                            }
                            else
                            {
                                x.WriteAsDynamicType = false;
                            }
                            return x;
                        });
            }
        }

        void SetForAllLevels(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter, bool bypassFrozenCheck = false)
        {
            if (!bypassFrozenCheck) ThrowIfFrozen();
            _vs.SetForAllLevels(setter);
        }

#endregion

        internal object GetRawEnumValue()
        {
#if WINRT || PORTABLE || CF || FX11
            object value = ((FieldInfo)member).GetValue(null);
            switch(Helpers.GetTypeCode(Enum.GetUnderlyingType(((FieldInfo)member).FieldType)))
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
            if (getSpecified != null)
            {
                if (getSpecified.ReturnType != _model.MapType(typeof(bool))
                    || getSpecified.IsStatic
                    || getSpecified.GetParameters().Length != 0)
                {
                    throw new ArgumentException("Invalid pattern for checking member-specified", "getSpecified");
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
                    throw new ArgumentException("Invalid pattern for setting member-specified", "setSpecified");
                }
            }
            ThrowIfFrozen();
            this.getSpecified = getSpecified;
            this.setSpecified = setSpecified;

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
            Type effectiveType = GetSettingsCopy(0).Collection.ItemType;
            if (effectiveType == null) effectiveType = MemberType;
            return _model.GetSchemaTypeName(
                effectiveType,
                GetSettingsCopy(0).ContentBinaryFormatHint.GetValueOrDefault(),
                applyNetObjectProxy && GetSettingsCopy().EnhancedFormat.GetValueOrDefault(),
                applyNetObjectProxy && GetSettingsCopy().EnhancedFormat.GetValueOrDefault() && GetSettingsCopy().WriteAsDynamicType.GetValueOrDefault(),
                ref requiresBclImport);
        }

        internal ValueMember CloneAsUnfrozen(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var vm = (ValueMember)MemberwiseClone();
            vm._model = model;
            vm._serializer = null;
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