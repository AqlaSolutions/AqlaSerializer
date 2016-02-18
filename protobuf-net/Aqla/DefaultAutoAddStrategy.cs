// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Meta.Mapping.MemberHandlers;
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
                FindOrAddType(baseType, true, false, false);
            }

            try
            {
                AttributeMap[] typeAttribs = AttributeMap.Create(_model, type, false);
                AttributeFamily family = GetContractFamily(type, typeAttribs);
                if (family == AttributeFamily.AutoTuple)
                {
                    metaType.IsAutoTuple = true;
                }
                bool asEnum = !metaType.EnumPassthru && Helpers.IsEnum(type);
                if (family == AttributeFamily.None && !asEnum) return; // and you'd like me to do what, exactly?
                var partialMembers = new List<AttributeMap>();
                int dataMemberOffset = 0, implicitFirstTag = 1;
                bool inferTagByName = _model.InferTagFromNameDefault;
                ImplicitFieldsMode implicitMode = ImplicitFieldsMode.None;
                bool implicitAqla = false;
                bool explicitPropertiesContract = false;
                string name = null;

                if (family == AttributeFamily.ImplicitFallback)
                {
                    implicitMode = ImplicitFallbackMode;
                    implicitAqla = true;
                    explicitPropertiesContract = true;
                }
                for (int i = 0; i < typeAttribs.Length; i++)
                {
                    AttributeMap item = (AttributeMap)typeAttribs[i];
                    object tmp;
                    string fullAttributeTypeName = item.AttributeType.FullName;


                    // we check CanUse everywhere but not family because GetContractFamily is based on CanUse
                    // and CanUse is based on the settings
                    // except is for SerializableAttribute which family is not returned if other families are present
                    if (!asEnum && fullAttributeTypeName == "System.SerializableAttribute" && HasFamily(family, AttributeFamily.SystemSerializable))
                    {
                        implicitMode = ImplicitFieldsMode.AllFields;
                        implicitAqla = true;
                    }

                    if (!asEnum && fullAttributeTypeName == "ProtoBuf.ProtoIncludeAttribute" && CanUse(AttributeType.ProtoBuf))
                    {
                        int tag = 0;
                        if (item.TryGet("tag", out tmp)) tag = (int)tmp;
                        BinaryDataFormat dataFormat = BinaryDataFormat.Default;
                        if (item.TryGet("DataFormat", out tmp))
                        {
                            dataFormat = (BinaryDataFormat)(int)tmp;
                        }
                        Type knownType = null;
                        try
                        {
                            if (item.TryGet("knownTypeName", out tmp)) knownType = _model.GetType((string)tmp, Helpers.GetAssembly(type));
                            else if (item.TryGet("knownType", out tmp)) knownType = (Type)tmp;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Unable to resolve sub-type of: " + type.FullName, ex);
                        }
                        if (knownType == null)
                        {
                            throw new InvalidOperationException("Unable to resolve sub-type of: " + type.FullName);
                        }
                        if (metaType.IsValidSubType(knownType)) metaType.AddSubType(tag, knownType, dataFormat);
                    }

                    if (!asEnum && fullAttributeTypeName == "AqlaSerializer.SerializeDerivedTypeAttribute" && CanUse(AttributeType.Aqla))
                    {
                        int tag = 0;
                        if (item.TryGet("tag", out tmp)) tag = (int)tmp;
                        BinaryDataFormat dataFormat = BinaryDataFormat.Default;
                        if (item.TryGet("DataFormat", out tmp))
                        {
                            dataFormat = (BinaryDataFormat)(int)tmp;
                        }
                        Type knownType = null;
                        try
                        {
                            if (item.TryGet("knownTypeName", out tmp)) knownType = _model.GetType((string)tmp, Helpers.GetAssembly(type));
                            else if (item.TryGet("knownType", out tmp)) knownType = (Type)tmp;
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("Unable to resolve sub-type of: " + type.FullName, ex);
                        }
                        if (knownType == null)
                        {
                            throw new InvalidOperationException("Unable to resolve sub-type of: " + type.FullName);
                        }
                        if (metaType.IsValidSubType(knownType)) metaType.AddSubType(tag, knownType, dataFormat);
                    }

                    if (fullAttributeTypeName == "ProtoBuf.ProtoPartialIgnoreAttribute" && CanUse(AttributeType.ProtoBuf))
                        partialMembers.Add(item);
                    else if (fullAttributeTypeName == "AqlaSerializer.PartialNonSerializableMemberAttribute" && CanUse(AttributeType.Aqla))
                        partialMembers.Add(item);
                    else if (!asEnum && fullAttributeTypeName == "ProtoBuf.ProtoPartialMemberAttribute" && CanUse(AttributeType.ProtoBuf))
                        partialMembers.Add(item);
                    else if (!asEnum && fullAttributeTypeName == "AqlaSerializer.SerializablePartialMemberAttribute" && CanUse(AttributeType.Aqla))
                        partialMembers.Add(item);

                    if (fullAttributeTypeName == "ProtoBuf.ProtoContractAttribute" && HasFamily(family, AttributeFamily.ProtoBuf))
                    {
                        if (item.TryGet("Name", out tmp)) name = (string)tmp;
                        if (Helpers.IsEnum(type)) // note this is subtly different to isEnum; want to do this even if [Flags]
                        {
#if !FEAT_IKVM
                            // IKVM can't access EnumPassthruHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                            if (item.TryGet("EnumPassthruHasValue", false, out tmp) && (bool)tmp)
#endif
                            {
                                if (item.TryGet("EnumPassthru", out tmp))
                                {
                                    metaType.EnumPassthru = (bool)tmp;
                                    if (metaType.EnumPassthru) asEnum = false; // no longer treated as an enum
                                }
                            }
                        }
                        else
                        {
                            if (item.TryGet("DataMemberOffset", out tmp)) dataMemberOffset = (int)tmp;

#if !FEAT_IKVM
                            // IKVM can't access InferTagFromNameHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                            if (item.TryGet("InferTagFromNameHasValue", false, out tmp) && (bool)tmp)
#endif
                            {
                                if (item.TryGet("InferTagFromName", out tmp)) inferTagByName = (bool)tmp;
                            }

                            if (item.TryGet("ImplicitFields", out tmp) && tmp != null)
                            {
                                implicitMode = (ImplicitFieldsMode)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                            }
                            if (item.TryGet("ExplicitPropertiesContract", out tmp) && tmp != null)
                            {
                                explicitPropertiesContract = (bool)tmp;
                            }
                            if (item.TryGet("SkipConstructor", out tmp)) metaType.UseConstructor = !(bool)tmp;
                            if (item.TryGet("IgnoreListHandling", out tmp)) metaType.IgnoreListHandling = (bool)tmp;
                            if (item.TryGet("AsReferenceDefault", out tmp)) metaType.AsReferenceDefault = (bool)tmp;
                            if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) implicitFirstTag = (int)tmp;
                        }
                    }

                    if (fullAttributeTypeName == "AqlaSerializer.SerializableTypeAttribute" && HasFamily(family, AttributeFamily.Aqla))
                    {
                        if (item.TryGet("Name", out tmp)) name = (string)tmp;
                        if (Helpers.IsEnum(type)) // note this is subtly different to isEnum; want to do this even if [Flags]
                        {
#if !FEAT_IKVM
                            // IKVM can't access EnumPassthruHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                            if (item.TryGet("EnumPassthruHasValue", false, out tmp) && (bool)tmp)
#endif
                            {
                                if (item.TryGet("EnumPassthru", out tmp))
                                {
                                    metaType.EnumPassthru = (bool)tmp;
                                    if (metaType.EnumPassthru) asEnum = false; // no longer treated as an enum
                                }
                            }
                        }
                        else
                        {
                            if (item.TryGet("DataMemberOffset", out tmp)) dataMemberOffset = (int)tmp;

#if !FEAT_IKVM
                            // IKVM can't access InferTagFromNameHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                            if (item.TryGet("InferTagFromNameHasValue", false, out tmp) && (bool)tmp)
#endif
                            {
                                if (item.TryGet("InferTagFromName", out tmp)) inferTagByName = (bool)tmp;
                            }

                            if (item.TryGet("ImplicitFields", out tmp) && tmp != null)
                            {
                                implicitMode = (ImplicitFieldsMode)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                                if (implicitMode != ImplicitFieldsMode.None) implicitAqla = true;
                            }
                            if (item.TryGet("ExplicitPropertiesContract", out tmp) && tmp != null)
                            {
                                explicitPropertiesContract = (bool)tmp;
                            }
                            if (item.TryGet("SkipConstructor", out tmp)) metaType.UseConstructor = !(bool)tmp;
                            if (item.TryGet("IgnoreListHandling", out tmp)) metaType.IgnoreListHandling = (bool)tmp;
                            if (item.TryGet("NotAsReferenceDefault", out tmp)) metaType.AsReferenceDefault = !(bool)tmp;
                            if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) implicitFirstTag = (int)tmp;
                            if (item.TryGet("ConstructType", out tmp)) metaType.ConstructType = (Type)tmp;
                        }
                    }

                    if (fullAttributeTypeName == "System.Runtime.Serialization.DataContractAttribute" && HasFamily(family, AttributeFamily.DataContractSerialier))
                    {
                        if (name == null && item.TryGet("Name", out tmp)) name = (string)tmp;
                    }
                    if (fullAttributeTypeName == "System.Xml.Serialization.XmlTypeAttribute" && HasFamily(family, AttributeFamily.XmlSerializer))
                    {
                        if (name == null && item.TryGet("TypeName", out tmp)) name = (string)tmp;
                    }
                }

                if (!Helpers.IsNullOrEmpty(name)) metaType.Name = name;
                if (implicitMode != ImplicitFieldsMode.None)
                {
                    if (family == AttributeFamily.ImplicitFallback)
                    {
                        family = AttributeFamily.None;
                        if (CanUse(AttributeType.ProtoBuf))
                            family |= AttributeFamily.ProtoBuf;
                        if (CanUse(AttributeType.Aqla))
                            family |= AttributeFamily.Aqla;
                    }
                    else if (HasFamily(family, AttributeFamily.Aqla) || HasFamily(family, AttributeFamily.ProtoBuf))
                    {
                        if (implicitAqla)
                            family &= AttributeFamily.Aqla;
                        else
                            family &= AttributeFamily.ProtoBuf; // with implicit fields, **only** proto attributes are important
                    }
                }
                MethodInfo[] callbacks = null;

                var members = new List<NormalizedMappedMember>();

#if WINRT
            System.Collections.Generic.IEnumerable<MemberInfo> foundList;
            if(isEnum) {
                foundList = type.GetRuntimeFields();
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
                MemberInfo[] foundList = type.GetMembers(asEnum ? BindingFlags.Public | BindingFlags.Static
                    : BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif
                foreach (MemberInfo member in foundList)
                {
                    if (member.DeclaringType != type) continue;
                    var map = AttributeMap.Create(_model, member, true);

                    var args = new MemberArgsValue(member, Helpers.GetMemberType(member), map, AcceptableAttributes, Model)
                    {
                        AsEnum = asEnum,
                        DataMemberOffset = dataMemberOffset,
                        Family = family,
                        InferTagByName = inferTagByName,
                        PartialMembers = partialMembers,
                        IgnoreNonWritableForOverwriteCollection = explicitPropertiesContract,
                    };

                    bool isPublic, isField;
                    

                    PropertyInfo property;
                    FieldInfo field;
                    MethodInfo method;
                    if ((property = member as PropertyInfo) != null)
                    {
                        if (asEnum) continue; // wasn't expecting any props!

                        isPublic = Helpers.GetGetMethod(property, false, false) != null;
                        isField = false;
                        var r = ApplyDefaultBehaviour_AddMembers(ref args, implicitMode, isPublic, isField, metaType.ConstructType);
                        if (r != null) members.Add(r);
                    }
                    else if ((field = member as FieldInfo) != null)
                    {
                        isPublic = field.IsPublic;
                        isField = true;
                        if (asEnum && !field.IsStatic)
                        { // only care about static things on enums; WinRT has a __value instance field!
                            continue;
                        }
                        var r = ApplyDefaultBehaviour_AddMembers(ref args, implicitMode, isPublic, isField, metaType.ConstructType);
                        if (r != null) members.Add(r);
                    }
                    else if ((method = member as MethodInfo) != null)
                    {
                        if (asEnum) continue;
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
                        if (!asEnum && member.Tag > 0)
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
                                FindOrAddType(memberType, true, false, false);
                            }
                        }
                    }
            }
            finally
            {
                if (baseType != null && GetContractFamily(baseType) != AttributeFamily.None)
                {
                    if (FindMetaTypeWithoutAdd(baseType) != null)
                    {
                        MetaType baseMeta = _model[baseType];
                        if (!DisableAutoRegisteringSubtypes && !baseMeta.IsList && baseMeta.IsValidSubType(type) && CanAutoAddType(baseType))
                            baseMeta.AddSubType(baseMeta.GetNextFreeFieldNumber(AutoRegisteringSubtypesFirstTag), type);
                    }
                }
            }
        }

        protected virtual MetaType FindMetaTypeWithoutAdd(Type baseType)
        {
            return _model.FindWithoutAdd(baseType);
        }

        protected virtual void FindOrAddType(Type type, bool demand, bool addWithContractOnly, bool addEvenIfAutoDisabled)
        {
            _model.FindOrAddAuto(type, demand, addWithContractOnly, addEvenIfAutoDisabled);
        }

        protected virtual NormalizedMappedMember ApplyDefaultBehaviour_AddMembers(ref MemberArgsValue argsValue, ImplicitFieldsMode implicitMode, bool isPublic, bool isField, Type defaultType)
        {
            switch (implicitMode)
            {
                case ImplicitFieldsMode.PublicFields:
                    if (isField & isPublic) argsValue.IsForced = true;
                    break;
                case ImplicitFieldsMode.AllFields:
                    if (isField) argsValue.IsForced = true;
                    break;
                case ImplicitFieldsMode.AllProperties:
                    if (!isField) argsValue.IsForced = true;
                    break;
                case ImplicitFieldsMode.PublicFieldsAndProperties:
                    if (isPublic) argsValue.IsForced = true;
                    break;
                case ImplicitFieldsMode.PublicProperties:
                    if (isPublic && !isField) argsValue.IsForced = true;
                    break;
                case ImplicitFieldsMode.AllFieldsAndProperties:
                    argsValue.IsForced = true;
                    break;
            }

            // we just don't like delegate types ;p
#if WINRT
            if (argsValue.EffectiveMemberType.GetTypeInfo().IsSubclassOf(typeof(Delegate))) argsValue.EffectiveMemberType = null;
#else
            if (argsValue.EffectiveMemberType.IsSubclassOf(_model.MapType(typeof(Delegate)))) argsValue.EffectiveMemberType = null;
#endif
            if (argsValue.EffectiveMemberType != null)
            {
                var normalized = MemberMapper.Map(ref argsValue);
                if (normalized != null)
                {
                    var levels = normalized.MappingState.LevelValues;
                    if (levels.Count == 0)
                        levels.Add(new MemberLevelSettingsValue());
                    for (int i = 0; i < levels.Count; i++)
                    {
                        var level = levels[i].GetValueOrDefault();
                        if (level.CollectionConcreteType == null) level.CollectionConcreteType = defaultType;
                        levels[i] = level;
                    }
                    argsValue = normalized.MappingState.Input; // just to be sure
                }
                return normalized;
            }
            return null;
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
            if (type.Name == "RefPair`2")
            {

            }
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
        
        protected virtual void ApplyDefaultBehaviour(MetaType metaType, NormalizedMappedMember mappedMember, int? implicitFirstTag)
        {
            MemberInfo member;
            if (mappedMember == null || (member = mappedMember.Member) == null) return; // nix
            var s = mappedMember.MappingState;

            AttributeMap[] attribs = AttributeMap.Create(_model, member, true);
            AttributeMap attrib;
            
            if (s.MainValue.DefaultValue == null && (attrib = AttributeMap.GetAttribute(attribs, "System.ComponentModel.DefaultValueAttribute")) != null)
            {
                object tmp;
                if (attrib.TryGet("Value", out tmp))
                {
                    var m = s.MainValue;
                    m.DefaultValue = tmp;
                    s.MainValue = m;
                }
            }

            {
                var memberType = Helpers.GetMemberType(member);
                var level0 = mappedMember.MappingState.LevelValues[0].Value;
                {
                    Type defaultType = level0.CollectionConcreteType;
                    if (defaultType == null && Helpers.IsInterface(metaType.Type))
                        level0.CollectionConcreteType = FindDefaultInterfaceImplementation(memberType);
                    mappedMember.MappingState.LevelValues[0] = level0;
                }
            }

            if (implicitFirstTag.HasValue && !s.TagIsPinned)
                mappedMember.Tag = metaType.GetNextFreeFieldNumber(implicitFirstTag.Value);

            if (mappedMember.MappingState.Input.AsEnum || mappedMember.Tag > 0)
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
        
        public virtual bool GetAsReferenceDefault(Type type, bool isProtobufNetLegacyMember)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (!ValueMember.CheckCanBeAsReference(type, false)) return false;
            if (Helpers.IsEnum(type)) return false; // never as-ref
            AttributeMap[] typeAttribs = AttributeMap.Create(_model, type, false);
            for (int i = 0; i < typeAttribs.Length; i++)
            {
                if (typeAttribs[i].AttributeType.FullName == "AqlaSerializer.SerializableTypeAttribute" && CanUse(AttributeType.Aqla))
                {
                    object tmp;
                    if (typeAttribs[i].TryGet("NotAsReferenceDefault", out tmp)) return !(bool)tmp;
                }
                if (typeAttribs[i].AttributeType.FullName == "ProtoBuf.ProtoContractAttribute" && CanUse(AttributeType.ProtoBuf))
                {
                    object tmp;
                    if (typeAttribs[i].TryGet("AsReferenceDefault", out tmp)) return (bool)tmp;
                }
            }

            bool ignoreAddSettings = RuntimeTypeModel.CheckTypeDoesntRequireContract(_model, type);
            return ignoreAddSettings || (!isProtobufNetLegacyMember && !_model.AddNotAsReferenceDefault);
        }

        public virtual bool GetIgnoreListHandling(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            AttributeMap[] typeAttribs = AttributeMap.Create(_model, type, false);
            for (int i = 0; i < typeAttribs.Length; i++)
            {
                if (typeAttribs[i].AttributeType.FullName == "AqlaSerializer.SerializableTypeAttribute" && CanUse(AttributeType.Aqla))
                {
                    object tmp;
                    if (typeAttribs[i].TryGet("IgnoreListHandling", out tmp)) return (bool)tmp;
                }
                if (typeAttribs[i].AttributeType.FullName == "ProtoBuf.ProtoContractAttribute" && CanUse(AttributeType.ProtoBuf))
                {
                    object tmp;
                    if (typeAttribs[i].TryGet("IgnoreListHandling", out tmp)) return (bool)tmp;
                }
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

        public DefaultAutoAddStrategy(RuntimeTypeModel model, IMemberMapper memberMapper = null)
        {
            if (model == null) throw new ArgumentNullException("model");
            _model = model;
            MemberMapper = memberMapper ?? CreateDefaultMemberMapper();
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