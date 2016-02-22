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

        List<MemberLevelSettingsValue?> _levels;
        // TODO default level used for not specified levels and overrides level[0] when creating
        MemberLevelSettingsValue _defaultLevel;

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
        public object DefaultValue { get { return _main.DefaultValue; } set { ThrowIfFrozen(); _main.DefaultValue = value; } }

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
            MemberMainSettingsValue mainSettings, IEnumerable<MemberLevelSettingsValue?> levels, MemberLevelSettingsValue defaultLevel, MemberInfo member, Type memberType, Type parentType, RuntimeTypeModel model)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (memberType == null) throw new ArgumentNullException(nameof(memberType));
            if (parentType == null) throw new ArgumentNullException(nameof(parentType));
            if (model == null) throw new ArgumentNullException(nameof(model));
            MemberType = memberType;
            Member = member;
            ParentType = parentType;
            this._model = model;
            _main = mainSettings;
            _levels = new List<MemberLevelSettingsValue?>(levels ?? new MemberLevelSettingsValue?[0]);
            _defaultLevel = defaultLevel;
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

                MemberLevelSettingsValue level0 = GetSettingsCopy(0);

                // TODO merge type settings
                //int memberTypeMetaKey = _model.GetKey(MemberType, false, false);
                //if (memberTypeMetaKey >= 0)
                //{
                //    var typeSettings = _model[memberTypeMetaKey].GetSettings());
                //    level0 = MemberLevelSettingsValue.Merge(typeSettings, level0);
                //}

                if (Helpers.IsNullOrEmpty(_main.Name)) _main.Name = Member.Name;

                #region Default value

                if (_main.DefaultValue != null && getSpecified != null)
                    throw new ProtoException("Can't use default value \"" + _main.DefaultValue + "\" on member with *Specified or ShouldSerialize check " + Member);

                if (_main.DefaultValue != null && IsRequired)
                    throw new ProtoException("Can't use default value \"" + _main.DefaultValue + "\" on Required member " + Member);

                if (getSpecified == null && !IsRequired && _main.DefaultValue == null)
                {
                    if (_model.UseImplicitZeroDefaults)
                    {
                        switch (Helpers.GetTypeCode(MemberType))
                        {
                            case ProtoTypeCode.Boolean:
                                _main.DefaultValue = false;
                                break;
                            case ProtoTypeCode.Decimal:
                                _main.DefaultValue = (decimal)0;
                                break;
                            case ProtoTypeCode.Single:
                                _main.DefaultValue = (float)0;
                                break;
                            case ProtoTypeCode.Double:
                                _main.DefaultValue = (double)0;
                                break;
                            case ProtoTypeCode.Byte:
                                _main.DefaultValue = (byte)0;
                                break;
                            case ProtoTypeCode.Char:
                                _main.DefaultValue = (char)0;
                                break;
                            case ProtoTypeCode.Int16:
                                _main.DefaultValue = (short)0;
                                break;
                            case ProtoTypeCode.Int32:
                                _main.DefaultValue = (int)0;
                                break;
                            case ProtoTypeCode.Int64:
                                _main.DefaultValue = (long)0;
                                break;
                            case ProtoTypeCode.SByte:
                                _main.DefaultValue = (sbyte)0;
                                break;
                            case ProtoTypeCode.UInt16:
                                _main.DefaultValue = (ushort)0;
                                break;
                            case ProtoTypeCode.UInt32:
                                _main.DefaultValue = (uint)0;
                                break;
                            case ProtoTypeCode.UInt64:
                                _main.DefaultValue = (ulong)0;
                                break;
                            case ProtoTypeCode.TimeSpan:
                                _main.DefaultValue = TimeSpan.Zero;
                                break;
                            case ProtoTypeCode.Guid:
                                _main.DefaultValue = Guid.Empty;
                                break;
                        }
                    }

                }

                //#if WINRT
                if (_main.DefaultValue != null && _model.MapType(_main.DefaultValue.GetType()) != MemberType)
                    //#else
                    //            if (defaultValue != null && !memberType.IsInstanceOfType(defaultValue))
                    //#endif
                {
                    _main.DefaultValue = ParseDefaultValue(MemberType, _main.DefaultValue);
                }

                #endregion

                // TODO strict check, exception
                if (level0.Collection.Append == null || (!level0.Collection.Append.Value && !Helpers.CanWrite(_model, Member))) level0.Collection.Append = true;


                bool asReference;
                bool asLateReference = false;

                #region Reference and dynamic type

                {
                    asReference = GetAsReferenceDefault(_model, MemberType, false, false);

                    EnhancedMode wm = level0.EnhancedWriteMode;
                    if (level0.EnhancedFormat == false)
                    {
                        asReference = false;
                        wm = EnhancedMode.NotSpecified;
                    }
                    else if (wm != EnhancedMode.NotSpecified)
                        asReference = wm == EnhancedMode.LateReference || wm == EnhancedMode.Reference;


                    bool dynamicType = level0.WriteAsDynamicType.GetValueOrDefault();
                    if (dynamicType && level0.EnhancedFormat == false) throw new ProtoException("Dynamic type write mode strictly requires enhanced MemberFormat: " + Member);
                    if (wm == EnhancedMode.LateReference)
                    {
                        int key = _model.GetKey(MemberType, false, false);
                        // we check here only theoretical possibility for type but not whether it's enabled on model level
                        if (key <= 0 || !ValueSerializerBuilder.CanTypeBeAsLateReference(_model[key]))
                            throw new ProtoException(Member + " can't be written as late reference because of its type.");

                        asLateReference = true;
                    }

                    if (asReference)
                   {
                        // we check here only theoretical possibility for type but not whether it's enabled on model level
                        if (!ValueSerializerBuilder.CanTypeBeAsReference(MemberType, false))
                            throw new ProtoException(Member + " can't be written as reference because of its type.");

                        level0.EnhancedFormat = true;
                        level0.EnhancedWriteMode = wm = asLateReference ? EnhancedMode.LateReference : EnhancedMode.Reference;
                        if (asLateReference && dynamicType)
                            throw new ProtoException("Dynamic type write mode is not available for LateReference enhanced mode: " + Member);
                    }
                }

                #endregion

                #region Collections
                {
                    int idx = _model.FindOrAddAuto(MemberType, false, true, false);
                    if (idx >= 0 && _model[MemberType].IgnoreListHandling)
                        ResetCollectionSettings(ref level0);
                    else
                    {
                        Type newCollectionConcreteType = null;
                        Type newItemType = null;

                        MetaType.ResolveListTypes(_model, MemberType, ref newItemType, ref newCollectionConcreteType);

                        if (level0.Collection.ItemType == null)
                            level0.Collection.ItemType = newItemType;

                        // should not override with default because what if specified something like List<string> for IList? 
                        if (level0.CollectionConcreteType == null)
                            level0.CollectionConcreteType = newCollectionConcreteType;
                        else if (!Helpers.IsAssignableFrom(MemberType, level0.CollectionConcreteType))
                        {
                            throw new ProtoException(
                                "Specified CollectionConcreteType " + level0.CollectionConcreteType.Name + " is not assignable to member " + Member);
                        }

                        if (level0.Collection.ItemType == null)
                            ResetCollectionSettings(ref level0);
                        else if (!level0.Collection.Append.GetValueOrDefault() && !Helpers.CanWrite(_model, Member))
                        {
                            if (level0.Collection.Append == null)
                                level0.Collection.Append = true;
                            else
                                throw new ProtoException("The property " + Member + " is not writable but AppendCollection was set to false");
                        }
                    }
                }

                #endregion


                SetSettings(level0, 0, true);

                var ser = ValueSerializerBuilder.BuildValueFinalSerializer(
                    MemberType,
                    new ValueSerializerBuilder.CollectionSettings(level0.Collection.ItemType)
                    {
                        Append = level0.Collection.Append.GetValueOrDefault(),
                        DefaultType = level0.CollectionConcreteType,
                        IsPacked =
                            level0.Collection.ItemType != null 
                            && level0.Collection.Format == CollectionFormat.Google 
                            && !RuntimeTypeModel.CheckTypeIsCollection(_model, level0.Collection.ItemType)
                            && ListDecorator.CanPack(HelpersInternal.GetWireType(HelpersInternal.GetTypeCode(level0.Collection.ItemType), BinaryDataFormat.Default)),
                        ReturnList = Helpers.CanWrite(_model, Member)
                    },
                    level0.WriteAsDynamicType.GetValueOrDefault(),
                    asReference,
                    level0.ContentBinaryFormatHint.GetValueOrDefault(),
                    true,
                    // real type members always handle references if applicable
                    _main.DefaultValue,
#if FORCE_LATE_REFERENCE
                    true,
#else
                    asLateReference,
#endif
                    _model);

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
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
        }

        static void ResetCollectionSettings(ref MemberLevelSettingsValue level0)
        {

            level0.Collection.ItemType = null;
            level0.Collection.PackedWireTypeForRead = null;
            level0.Collection.Format = CollectionFormat.NotSpecified;
            level0.CollectionConcreteType = null;
        }

        #region Setting accessors
        // TODO wrapper
        public MemberLevelSettingsValue GetSettingsCopy(int level = 0)
        {
            return (_levels.Count <= level ? null : _levels[level]) ?? _defaultLevel;
        }
        
        public void SetSettings(MemberLevelSettingsValue value, int level = 0)
        {
            SetSettings(value, level, false);
        }

        void SetSettings(MemberLevelSettingsValue value, int level, bool bypassFrozenCheck)
        {
            if (!bypassFrozenCheck) ThrowIfFrozen();
            while (level >= _levels.Count)
                _levels.Add(null);
            _levels[level] = value;
        }

        /// <summary>
        /// Allows to set EnhancedFormat and EnhancedWriteMode for all levels
        /// </summary>
        [Obsolete("Use EnhancedFormat and EnhancedWriteMode")]
        public bool AsReference
        {
            set
            {
                SetForAllLevel(
                    x =>
                        {
                            if (value)
                            {
                                x.EnhancedFormat = true;
                                _defaultLevel.EnhancedFormat = true;
                                if (x.EnhancedWriteMode != EnhancedMode.LateReference)
                                    x.EnhancedWriteMode = EnhancedMode.Reference;
                                if (_defaultLevel.EnhancedWriteMode != EnhancedMode.LateReference)
                                    _defaultLevel.EnhancedWriteMode = EnhancedMode.Reference;
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
                    x =>
                        {
                            x.CollectionConcreteType = value;
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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
                SetForAllLevel(
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

        void SetForAllLevel(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter, bool bypassFrozenCheck = false)
        {
            if (!bypassFrozenCheck) ThrowIfFrozen();
            _defaultLevel = setter(_defaultLevel);
            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level == null) continue;
                _levels[i] = setter(level.Value);
            }
        }

        #endregion

        internal static bool GetAsReferenceDefault(RuntimeTypeModel model, Type memberType, bool isProtobufNetLegacyMember, bool isDeterminatedAsAutoTuple)
        {
            if (ValueSerializerBuilder.CanTypeBeAsReference(memberType, isDeterminatedAsAutoTuple))
            {
                if (RuntimeTypeModel.CheckTypeDoesntRequireContract(model, memberType))
                    isProtobufNetLegacyMember = false; // inbuilt behavior types doesn't depend on member legacy behavior

                MetaType type = model.FindWithoutAdd(memberType);
                if (type != null)
                {
                    type = type.GetSurrogateOrSelf();
                    return type.AsReferenceDefault;
                }
                else
                { // we need to scan the hard way; can't risk recursion by fully walking it
                    return model.AutoAddStrategy.GetAsReferenceDefault(memberType, isProtobufNetLegacyMember);
                }
            }
            return false;
        }

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
        private static object ParseDefaultValue(Type type, object value)
        {
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;
            }
            if (value is string)
            {
                string s = (string)value;
                if (Helpers.IsEnum(type)) return Helpers.ParseEnum(type, s);

                switch (Helpers.GetTypeCode(type))
                {
                    case ProtoTypeCode.Boolean: return bool.Parse(s);
                    case ProtoTypeCode.Byte: return byte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Char: // char.Parse missing on CF/phone7
                        if (s.Length == 1) return s[0];
                        throw new FormatException("Single character expected: \"" + s + "\"");
                    case ProtoTypeCode.DateTime: return DateTime.Parse(s, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Decimal: return decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Double: return double.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int16: return short.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int32: return int.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int64: return long.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.SByte: return sbyte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Single: return float.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.String: return s;
                    case ProtoTypeCode.UInt16: return ushort.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.UInt32: return uint.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.UInt64: return ulong.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.TimeSpan: return TimeSpan.Parse(s);
                    case ProtoTypeCode.Uri: return s; // Uri is decorated as string
                    case ProtoTypeCode.Guid: return new Guid(s);
                }
            }
#if FEAT_IKVM
            if (Helpers.IsEnum(type)) return value; // return the underlying type instead
            System.Type convertType = null;
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.SByte: convertType = typeof(sbyte); break;
                case ProtoTypeCode.Int16: convertType = typeof(short); break;
                case ProtoTypeCode.Int32: convertType = typeof(int); break;
                case ProtoTypeCode.Int64: convertType = typeof(long); break;
                case ProtoTypeCode.Byte: convertType = typeof(byte); break;
                case ProtoTypeCode.UInt16: convertType = typeof(ushort); break;
                case ProtoTypeCode.UInt32: convertType = typeof(uint); break;
                case ProtoTypeCode.UInt64: convertType = typeof(ulong); break;
                case ProtoTypeCode.Single: convertType = typeof(float); break;
                case ProtoTypeCode.Double: convertType = typeof(double); break;
                case ProtoTypeCode.Decimal: convertType = typeof(decimal); break;
            }
            if (convertType != null) return Convert.ChangeType(value, convertType, CultureInfo.InvariantCulture);
            throw new ArgumentException("Unable to process default value: " + value + ", " + type.FullName);
#else
            if (Helpers.IsEnum(type)) return Enum.ToObject(type, value);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
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
            vm._levels = new List<MemberLevelSettingsValue?>(vm._levels);
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