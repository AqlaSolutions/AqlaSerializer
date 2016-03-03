// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Meta.Mapping.MemberHandlers;
using AqlaSerializer.Meta.Mapping.TypeAttributeHandlers;
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

namespace AqlaSerializer
{
    using AttributeFamily = MetaType.AttributeFamily;
    public class DefaultAutoAddStrategy : IAutoAddStrategy
    {
        IMemberMapper _memberMapper;

        public IMemberMapper MemberMapper
        {
            get { return _memberMapper; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _memberMapper = value;
            }
        }
     
        ITypeMapper _typeMapper;

        public ITypeMapper TypeMapper
        {
            get { return _typeMapper; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _typeMapper = value;
            }
        }
        
        public virtual bool CanAutoAddType(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (!RuntimeTypeModel.CheckTypeCanBeAdded(_model, type)) return false;
            return GetContractFamily(type) != AttributeFamily.None
                   || RuntimeTypeModel.CheckTypeDoesntRequireContract(_model, type);
        }

        public virtual void ApplyDefaultBehaviour(MetaType metaType)
        {
            var type = metaType.Type;
            Type baseType = metaType.GetBaseType();
            if (baseType != null
                && CanAutoAddType(baseType)
                && MetaType.CanHaveSubType(baseType))
            {
                _model.FindOrAddAuto(baseType, true, false, false);
            }

            try
            {
                AttributeFamily family;
                TypeState mapped;

                {
                    AttributeMap[] typeAttribs = AttributeMap.Create(_model, type, false);
                    family = GetContractFamily(type, typeAttribs);

                    mapped = TypeMapper.Map(
                        new TypeArgsValue(type, typeAttribs, AcceptableAttributes, Model)
                        {
                            Family = family,
                            ImplicitFallbackMode = ImplicitFallbackMode,
                        });

                    foreach (var candidate in mapped.DerivedTypes)
                        if (metaType.IsValidSubType(candidate.Type)) metaType.AddSubType(candidate.Tag, candidate.Type, candidate.DataFormat);

                    metaType.SettingsValue = mapped.SettingsValue;
                }

                var partialMembers = mapped.PartialMembers;
                int dataMemberOffset = mapped.DataMemberOffset;
                int implicitFirstTag = mapped.ImplicitFirstTag;
                bool inferTagByName = mapped.InferTagByName;
                ImplicitFieldsMode implicitMode = mapped.ImplicitFields;
                family = mapped.Input.Family;
                
                MethodInfo[] callbacks = null;

                var members = new List<MappedMember>();

                bool isEnum = Helpers.IsEnum(type);
#if WINRT
                System.Collections.Generic.IEnumerable<MemberInfo> foundList;
                if (isEnum) {
                    foundList = type.GetRuntimeFields().Where(x => x.IsStatic && x.IsPublic);
                }
                else
                {
                    System.Collections.Generic.List<MemberInfo> list = new System.Collections.Generic.List<MemberInfo>();
                    foreach(PropertyInfo prop in type.GetRuntimeProperties()) {
                        MethodInfo getter = Helpers.GetGetMethod(prop, false, false);
                        if(getter != null && !getter.IsStatic) list.Add(prop);
                    }
                    foreach(FieldInfo fld in type.GetRuntimeFields()) if(fld.IsPublic && !fld.IsStatic) list.Add(fld);
                    foreach(MethodInfo mthd in type.GetRuntimeMethods()) if(mthd.IsPublic && !mthd.IsStatic) list.Add(mthd);
                    foundList = list;
                }
#else
                MemberInfo[] foundList = type.GetMembers(isEnum 
                    ? BindingFlags.Public | BindingFlags.Static
                    : BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (isEnum)
                    foundList = foundList.Where(x => x is FieldInfo).ToArray();
#endif
                foreach (MemberInfo member in foundList)
                {
                    if (member.DeclaringType != type) continue;
                    var map = AttributeMap.Create(_model, member, true);

                    {
                        var args = new MemberArgsValue(member, map, AcceptableAttributes, Model)
                        {
                            DataMemberOffset = dataMemberOffset,
                            Family = family,
                            InferTagByName = inferTagByName,
                            PartialMembers = partialMembers,
                            IsEnumValueMember = isEnum
                        };
                        
                        PropertyInfo property;
                        FieldInfo field;
                        if ((property = member as PropertyInfo) != null)
                        {
                            bool isPublic = Helpers.GetGetMethod(property, false, false) != null;

                            bool canBeMapped = isPublic || Helpers.GetGetMethod(property, true, true) != null;

                            if (canBeMapped &&
                                (!mapped.ImplicitOnlyWriteable ||
                                 Helpers.CheckIfPropertyWritable
                                     (
                                         Model,
                                         property,
                                         implicitMode == ImplicitFieldsMode.AllProperties || implicitMode == ImplicitFieldsMode.AllFieldsAndProperties,
                                         false)))
                            {
                                switch (implicitMode)
                                {
                                    case ImplicitFieldsMode.AllProperties:
                                        args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.PublicProperties:
                                        if (isPublic)
                                            args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.PublicFieldsAndProperties:
                                        if (isPublic) args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.AllFieldsAndProperties:
                                        args.IsForced = true;
                                        break;
                                }
                            }

                            var r = ApplyDefaultBehaviour_AddMembers(args);
                            if (r != null)
                            {
                                if (!canBeMapped) throw new MemberAccessException("Property " + property + " should be readable to be mapped.");
                                members.Add(r);
                            }
                        }
                        else if ((field = member as FieldInfo) != null)
                        {
                            bool isPublic = field.IsPublic;
                            
                            if (!args.IsEnumValueMember)
                            {
                                switch (implicitMode)
                                {
                                    case ImplicitFieldsMode.AllFields:
                                        args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.PublicFields:
                                        if (isPublic) args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.PublicFieldsAndProperties:
                                        if (isPublic) args.IsForced = true;
                                        break;
                                    case ImplicitFieldsMode.AllFieldsAndProperties:
                                        args.IsForced = true;
                                        break;
                                }
                            }

                            var r = ApplyDefaultBehaviour_AddMembers(args);
                            if (r != null) members.Add(r);
                        }
                    }

                    MethodInfo method;
                    if ((method = member as MethodInfo) != null)
                    {
                        AttributeMap[] memberAttribs = AttributeMap.Create(_model, method, false);
                        if (memberAttribs != null && memberAttribs.Length > 0)
                        {
                            const int max = 11;
                            if (CanUse(AttributeType.ProtoBuf))
                            {
                                CheckForCallback(method, memberAttribs, "ProtoBuf.ProtoBeforeSerializationAttribute", ref callbacks, 0, max);
                                CheckForCallback(method, memberAttribs, "ProtoBuf.ProtoAfterSerializationAttribute", ref callbacks, 1, max);
                                CheckForCallback(method, memberAttribs, "ProtoBuf.ProtoBeforeDeserializationAttribute", ref callbacks, 2, max);
                                CheckForCallback(method, memberAttribs, "ProtoBuf.ProtoAfterDeserializationAttribute", ref callbacks, 3, max);
                            }

                            CheckForCallback(method, memberAttribs, "System.Runtime.Serialization.OnSerializingAttribute", ref callbacks, 4, max);
                            CheckForCallback(method, memberAttribs, "System.Runtime.Serialization.OnSerializedAttribute", ref callbacks, 5, max);
                            CheckForCallback(method, memberAttribs, "System.Runtime.Serialization.OnDeserializingAttribute", ref callbacks, 6, max);
                            CheckForCallback(method, memberAttribs, "System.Runtime.Serialization.OnDeserializedAttribute", ref callbacks, 7, max);

                            if (CanUse(AttributeType.Aqla))
                            {
                                CheckForCallback(method, memberAttribs, "AqlaSerializer.BeforeSerializationCallbackAttribute", ref callbacks, 8, max);
                                CheckForCallback(method, memberAttribs, "AqlaSerializer.AfterSerializationCallbackAttribute", ref callbacks, 9, max);
                                CheckForCallback(method, memberAttribs, "AqlaSerializer.BeforeDeserializationCallbackAttribute", ref callbacks, 10, max);
                                CheckForCallback(method, memberAttribs, "AqlaSerializer.AfterDeserializationCallbackAttribute", ref callbacks, 11, max);
                            }
                        }
                    }
                }
                if (inferTagByName || implicitMode != ImplicitFieldsMode.None)
                {
                    members.Sort();
                    foreach (var member in members)
                    {
                        if (!member.MappingState.TagIsPinned) // if ProtoMember etc sets a tag, we'll trust it
                            member.Tag = -1;
                    }
                }

                foreach (var member in members)
                {
                    ApplyDefaultBehaviour(metaType, member,
                        (inferTagByName || implicitMode != ImplicitFieldsMode.None) ? (int?)implicitFirstTag : null);
                }

                if (callbacks != null)
                {
                    metaType.SetCallbacks(Coalesce(callbacks, 0, 4, 8), Coalesce(callbacks, 1, 5, 9),
                        Coalesce(callbacks, 2, 6, 10), Coalesce(callbacks, 3, 7, 11));
                }

                if (!DisableAutoAddingMemberTypes)
                    foreach (var member in members)
                    {
                        if (!member.MappingState.Input.IsEnumValueMember && member.Tag > 0)
                        {
                            Type memberType = Helpers.GetMemberType(member.Member);
                            memberType = Helpers.GetNullableUnderlyingType(memberType) ?? memberType;
                            if (memberType.IsArray)
                            {
                                if (memberType.GetArrayRank() == 1)
                                    memberType = memberType.GetElementType();
                                else continue;
                            }
                            memberType = TypeModel.GetListItemType(_model, memberType) ?? memberType;
                            if (memberType == null) continue;

                            if (CanAutoAddType(memberType))
                            {
                                _model.FindOrAddAuto(memberType, true, false, false);
                            }
                        }
                    }
            }
            finally
            {
                if (baseType != null && GetContractFamily(baseType) != AttributeFamily.None)
                {
                    if (_model.FindWithoutAdd(baseType) != null)
                    {
                        MetaType baseMeta = _model[baseType];
                        // we can't add to frozen base type
                        // but this is not always an error
                        // e.g. dynamic member of base type doesn't need registered subtype
                        if (!baseMeta.IsFrozen && !DisableAutoRegisteringSubtypes && !baseMeta.IsList && baseMeta.IsValidSubType(type) && CanAutoAddType(baseType))
                            baseMeta.AddSubType(baseMeta.GetNextFreeFieldNumber(AutoRegisteringSubtypesFirstTag), type);
                    }
                }
            }
        }

        protected virtual MappedMember ApplyDefaultBehaviour_AddMembers(MemberArgsValue argsValue)
        {
            var memberType = Helpers.GetMemberType(argsValue.Member);
            // we just don't like delegate types ;p
            if (memberType == null || Helpers.IsSubclassOf(memberType, _model.MapType(typeof(Delegate)))) return null;

            var normalized= MemberMapper.Map(argsValue);
            if (normalized != null)
            {
                var m = normalized.MappingState.MainValue;
                if (string.IsNullOrEmpty(m.Name)) m.Name = normalized.Member.Name;
                normalized.MappingState.MainValue = m;
            }
            return normalized;
        }


        static MethodInfo Coalesce(MethodInfo[] arr, params int[] indexes)
        {
            MethodInfo mi = null;
            for (int i = 0; mi == null && i < indexes.Length; i++)
            {
                mi = arr[indexes[i]];

            }
            return mi;
        }

        public AttributeFamily GetContractFamily(Type type)
        {
            var attributes = AttributeMap.Create(_model, type, false);
            return GetContractFamily(type, attributes);
        }

        protected virtual AttributeFamily GetContractFamily(Type type, AttributeMap[] attributes)
        {
            if (Helpers.GetNullableUnderlyingType(type) != null) return AttributeFamily.None;
            if (!Helpers.IsEnum(type) && Helpers.GetTypeCode(type) != ProtoTypeCode.Unknown) return AttributeFamily.None; // known types are not contracts
            AttributeFamily family = AttributeFamily.None;
            bool isList = type.IsArray || TypeModel.GetListItemType(_model, type) != null;
            for (int i = 0; i < attributes.Length; i++)
            {
                switch (attributes[i].AttributeType.FullName)
                {
                    case "ProtoBuf.ProtoContractAttribute":
                        {
                            if (CanUse(AttributeType.ProtoBuf))
                            {
                                bool tmp = false;
                                GetFieldBoolean(ref tmp, attributes[i], "UseProtoMembersOnly");
                                if (tmp) return AttributeFamily.ProtoBuf;
                                family |= AttributeFamily.ProtoBuf;
                            }
                        }
                        break;
                    case "AqlaSerializer.SerializableTypeAttribute":
                        {
                            if (CanUse(AttributeType.Aqla))
                            {
                                bool tmp = false;
                                GetFieldBoolean(ref tmp, attributes[i], "UseAqlaMembersOnly");
                                if (tmp) return AttributeFamily.Aqla;
                                family |= AttributeFamily.Aqla;
                            }
                        }
                        break;
                    case "System.Xml.Serialization.XmlTypeAttribute":
                        if (CanUse(AttributeType.Xml))
                        {
                            family |= AttributeFamily.XmlSerializer;
                        }
                        break;
                    case "System.Runtime.Serialization.DataContractAttribute":
                        if (CanUse(AttributeType.DataContract))
                        {
                            family |= AttributeFamily.DataContractSerialier;
                        }
                        break;
                }
            }

            if (family == AttributeFamily.None)
            {
                if (Helpers.IsEnum(type))
                {
                    // it's not required to specify attributes on enum

                    if (CanUse(AttributeType.ProtoBuf) && !CanUse(AttributeType.Aqla))
                        family |= AttributeFamily.ProtoBuf;
                    else
                        family |= AttributeFamily.Aqla;
                }
                else if (!DisableAutoTuples)
                {
                    // check for obvious tuples

                    // AqlaSerializer: as-reference is 
                    // a default behavior for classes
                    // and if type attribute is not set
                    // such behavior should apply.
                    // This will not be called if 
                    // there are any attributes!

                    MemberInfo[] mapping;
                    if (MetaType.ResolveTupleConstructor(type, out mapping) != null)
                    {
                        family |= AttributeFamily.AutoTuple;
                    }
                }
                if (family == AttributeFamily.None && ImplicitFallbackMode != ImplicitFieldsMode.None && !isList)
                {
                    if (Helpers.GetTypeCode(type) == ProtoTypeCode.Unknown
                        && type != _model.MapType(typeof(object))
                        && type != _model.MapType(typeof(ValueType)))
                    {
                        family = AttributeFamily.ImplicitFallback;
                    }
                }
                if (family == AttributeFamily.None && CanUse(AttributeType.SystemSerializable) && !isList)
                {
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        switch (attributes[i].AttributeType.FullName)
                        {
                            case "System.SerializableAttribute":
                                family |= AttributeFamily.SystemSerializable;
                                break;
                        }
                    }
                }
            }
            return family;
        }

        protected static void CheckForCallback(MethodInfo method, AttributeMap[] attributes, string callbackTypeName, ref MethodInfo[] callbacks, int index, int max)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].AttributeType.FullName == callbackTypeName)
                {
                    if (callbacks == null) { callbacks = new MethodInfo[max + 1]; }
                    else if (callbacks[index] != null)
                    {
#if WINRT || FEAT_IKVM
                        Type reflected = method.DeclaringType;
#else
                        Type reflected = method.ReflectedType;
#endif
                        throw new ProtoException("Duplicate " + callbackTypeName + " callbacks on " + reflected.FullName);
                    }
                    callbacks[index] = method;
                }
            }
        }
        protected static bool HasFamily(AttributeFamily value, AttributeFamily required)
        {
            return (value & required) == required;
        }
        
        protected virtual void ApplyDefaultBehaviour(MetaType metaType, MappedMember mappedMember, int? implicitFirstTag)
        {
            MemberInfo member;
            if (mappedMember == null || (member = mappedMember.Member) == null) return; // nix
            var s = mappedMember.MappingState;

            AttributeMap[] attribs = AttributeMap.Create(_model, member, true);
            AttributeMap attrib;
            
            if (s.SerializationSettings.DefaultValue == null && (attrib = AttributeMap.GetAttribute(attribs, "System.ComponentModel.DefaultValueAttribute")) != null)
            {
                object tmp;
                if (attrib.TryGet("Value", out tmp))
                {
                    var m = s.MainValue;
                    s.SerializationSettings.DefaultValue = tmp;
                    s.MainValue = m;
                }
            }

            {
                var level0 = mappedMember.MappingState.SerializationSettings.GetSettingsCopy(0).Basic;
                {
                    Type defaultType = level0.Collection.ConcreteType;
                    if (defaultType == null)
                    {
                        var memberType = level0.EffectiveType ?? Helpers.GetMemberType(mappedMember.Member);
                        if (Helpers.IsInterface(memberType) || Helpers.IsAbstract(memberType))
                            level0.Collection.ConcreteType = FindDefaultInterfaceImplementation(memberType);
                    }
                }
                mappedMember.MappingState.SerializationSettings.SetSettings(level0, 0);
            }

            if (implicitFirstTag != null && !s.TagIsPinned)
                mappedMember.Tag = metaType.GetNextFreeFieldNumber(implicitFirstTag.Value);

            if (mappedMember.MappingState.Input.IsEnumValueMember || mappedMember.Tag > 0)
                metaType.Add(mappedMember);
        }

        protected static void GetFieldBoolean(ref bool value, AttributeMap attrib, string memberName)
        {
            GetFieldBoolean(ref value, attrib, memberName, true);
        }
        protected static bool GetFieldBoolean(ref bool value, AttributeMap attrib, string memberName, bool publicOnly)
        {
            if (attrib == null) return false;
            if (value) return true;
            object obj;
            if (attrib.TryGet(memberName, publicOnly, out obj) && obj != null)
            {
                value = (bool)obj;
                return true;
            }
            return false;
        }
        
        public bool DisableAutoTuples { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public delegate Type ImplementationMappingResolveFunc(Type interfaceType);

        /// <summary>
        /// 
        /// </summary>
        public event ImplementationMappingResolveFunc InterfaceImplementationMapping;

        /// <summary>
        /// 
        /// </summary>
        protected virtual Type FindDefaultInterfaceImplementation(Type interfaceType)
        {
            var mapping = InterfaceImplementationMapping;
            if (mapping != null)
                foreach (ImplementationMappingResolveFunc d in mapping.GetInvocationList())
                {
                    Type r = d(interfaceType);
                    if (r != null)
                        return r;
                }
            return null;
        }

        RuntimeTypeModel _model;
        protected RuntimeTypeModel Model { get { return _model; } }

        static bool CanUse(AttributeType check, AttributeType required)
        {
            return (check & required) != 0;
        }

        bool CanUse(AttributeType required)
        {
            return (_acceptableAttributes & required) != 0;
        }

        private AttributeType _acceptableAttributes = AttributeType.Default;

        /// <summary>
        /// Global default that determines whether types are considered serializable
        /// if they have [DataContract] / [XmlType] / [ProtoContract] / [SerializableType].
        /// </summary>
        public AttributeType AcceptableAttributes
        {
            get { return _acceptableAttributes; }
            set
            {
                if (!CanUse(_acceptableAttributes, AttributeType.Aqla) && !CanUse(_acceptableAttributes, AttributeType.ProtoBuf))
                    throw new ArgumentException("Either Aqla or ProtoBuf or both attributes should be enabled");
                _acceptableAttributes = value;
            }
        }

        /// <summary>
        /// When no attributes found and this is not an AutoTuple then the specified mode will be used. 
        /// Set to <see cref="ImplicitFieldsMode.AllFields"/> and use <see cref="DisableAutoTuples"/> to perform as BinaryFormatter
        /// </summary>
        public ImplicitFieldsMode ImplicitFallbackMode { get; set; }

        /// <summary>
        /// By default all derived types add themselves to the corresponding base types
        /// </summary>
        public bool DisableAutoRegisteringSubtypes { get; set; }

        int _autoRegisteringSubtypesFirstTag = 200;

        /// <summary>
        /// What tag to try from?
        /// </summary>
        public int AutoRegisteringSubtypesFirstTag
        {
            get { return _autoRegisteringSubtypesFirstTag; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("value", "Should be > 0");
                _autoRegisteringSubtypesFirstTag = value;
            }
        }

        public bool DisableAutoAddingMemberTypes { get; set; }

        public DefaultAutoAddStrategy(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            _model = model;
            MemberMapper = CreateDefaultMemberMapper();
            TypeMapper = CreateDefaultTypeMapper();
        }

        public static IMemberMapper CreateDefaultMemberMapper()
        {
            return new MemberMapper(
                new IMemberHandler[]
                {
                    new SystemNonSerializableHandler(),
                    new AqlaEnumMemberHandler(),
                    new ProtobufNetEnumMemberHandler(),
                    new AqlaMemberHandler(),
                    new AqlaPartialMemberHandler(),
                    new ProtobufNetMemberHandler(new ProtobufNetMemberHandlerStrategy()),
                    new ProtobufNetPartialMemberHandler(new ProtobufNetMemberHandlerStrategy()),
                    new DataContractMemberHandler(),
                    new XmlContractMemberHandler(),
                    new ProtobufNetImplicitMemberHandler(new ProtobufNetMemberHandlerStrategy()), 
                });
        }

        public static ITypeMapper CreateDefaultTypeMapper()
        {
            return new TypeMapper(
                new[]
                {
                    new TypeMapper.Handler("System.SerializableAttribute", new SystemSerializableHandler()),
                    new TypeMapper.Handler("AqlaSerializer.SerializableTypeAttribute", new AqlaContractHandler()),
                    new TypeMapper.Handler("ProtoBuf.ProtoContractAttribute", new ProtoContractHandler()),
                    new TypeMapper.Handler("ProtoBuf.ProtoIncludeAttribute", new ProtoIncludeHandler(new DerivedTypeHandlerStrategy())),
                    new TypeMapper.Handler("AqlaSerializer.SerializeDerivedTypeAttribute", new SerializeDerivedTypeHandler(new DerivedTypeHandlerStrategy())),
                    new TypeMapper.Handler("AqlaSerializer.PartialNonSerializableMemberAttribute", new AqlaPartialHandler()),
                    new TypeMapper.Handler("AqlaSerializer.SerializablePartialMemberAttribute", new AqlaPartialHandler()),
                    new TypeMapper.Handler("ProtoBuf.ProtoPartialIgnoreAttribute", new ProtoPartialHandler()),
                    new TypeMapper.Handler("ProtoBuf.ProtoPartialMemberAttribute", new ProtoPartialHandler()),
                    new TypeMapper.Handler("System.Runtime.Serialization.DataContractAttribute", new DataContractHandler()),
                    new TypeMapper.Handler("System.Xml.Serialization.XmlTypeAttribute", new XmlContractHandler()),
                });
        }

        public virtual IAutoAddStrategy Clone(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var s = (DefaultAutoAddStrategy)MemberwiseClone();
            s._model = model;
            return s;
        }
    }
}
#endif