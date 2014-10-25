// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2014
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;


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
        public virtual void ApplyDefaultBehaviour(MetaType metaType)
        {
            var type = metaType.Type;
            Type baseType = metaType.GetBaseType();
            if (baseType != null && GetContractFamily(baseType) != AttributeFamily.None)
            {
                FindOrAddType(baseType, true, false, false);
            }

            AttributeMap[] typeAttribs = AttributeMap.Create(_model, type, false);
            AttributeFamily family = GetContractFamily(type, typeAttribs);
            if (family == AttributeFamily.AutoTuple)
            {
                metaType.IsAutoTuple = true;
            }
            bool isEnum = !metaType.EnumPassthru && Helpers.IsEnum(type);
            if (family == AttributeFamily.None && !isEnum) return; // and you'd like me to do what, exactly?
            BasicList partialIgnores = null, partialMembers = null;
            int dataMemberOffset = 0, implicitFirstTag = 1;
            bool inferTagByName = _model.InferTagFromNameDefault;
            ImplicitFields implicitMode = ImplicitFields.None;
            bool implicitAqla = false;
            bool explicitPropertiesContract = false;
            string name = null;

            if (family == AttributeFamily.ImplicitFallback)
            {
                implicitMode = ImplicitFallbackMode;
                implicitAqla = true;
                explicitPropertiesContract = true;
            }
            else
            {
                for (int i = 0; i < typeAttribs.Length; i++)
                {
                    AttributeMap item = (AttributeMap)typeAttribs[i];
                    object tmp;
                    string fullAttributeTypeName = item.AttributeType.FullName;
                    if (!isEnum && fullAttributeTypeName == "ProtoBuf.ProtoIncludeAttribute" && CanUse(AttributeType.ProtoBuf))
                    {
                        int tag = 0;
                        if (item.TryGet("tag", out tmp)) tag = (int)tmp;
                        DataFormat dataFormat = DataFormat.Default;
                        if (item.TryGet("DataFormat", out tmp))
                        {
                            dataFormat = (DataFormat)(int)tmp;
                        }
                        Type knownType = null;
                        try
                        {
                            if (item.TryGet("knownTypeName", out tmp)) knownType = _model.GetType((string)tmp, type
#if WINRT
                            .GetTypeInfo()
#endif
.Assembly);
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

                    if (!isEnum && fullAttributeTypeName == "AqlaSerializer.SerializeDerivedTypeAttribute" && CanUse(AttributeType.Aqla))
                    {
                        int tag = 0;
                        if (item.TryGet("tag", out tmp)) tag = (int)tmp;
                        DataFormat dataFormat = DataFormat.Default;
                        if (item.TryGet("DataFormat", out tmp))
                        {
                            dataFormat = (DataFormat)(int)tmp;
                        }
                        Type knownType = null;
                        try
                        {
                            if (item.TryGet("knownTypeName", out tmp)) knownType = _model.GetType((string)tmp, type
#if WINRT
                            .GetTypeInfo()
#endif
.Assembly);
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
                    {
                        if (item.TryGet("MemberName", out tmp) && tmp != null)
                        {
                            if (partialIgnores == null) partialIgnores = new BasicList();
                            partialIgnores.Add((string)tmp);
                        }
                    }
                    else if (fullAttributeTypeName == "AqlaSerializer.PartialNonSerializableMemberAttribute" && CanUse(AttributeType.Aqla))
                    {
                        if (item.TryGet("MemberName", out tmp) && tmp != null)
                        {
                            if (partialIgnores == null) partialIgnores = new BasicList();
                            partialIgnores.Add((string)tmp);
                        }
                    }

                    if (!isEnum && fullAttributeTypeName == "ProtoBuf.ProtoPartialMemberAttribute" && CanUse(AttributeType.ProtoBuf))
                    {
                        if (partialMembers == null) partialMembers = new BasicList();
                        partialMembers.Add(item);
                    }
                    else if (!isEnum && fullAttributeTypeName == "AqlaSerializer.SerializablePartialMemberAttribute" && CanUse(AttributeType.Aqla))
                    {
                        if (partialMembers == null) partialMembers = new BasicList();
                        partialMembers.Add(item);
                    }

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
                                    if (metaType.EnumPassthru) isEnum = false; // no longer treated as an enum
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
                                implicitMode = (ImplicitFields)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
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
                                    if (metaType.EnumPassthru) isEnum = false; // no longer treated as an enum
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
                                implicitMode = (ImplicitFields)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                                if (implicitMode != ImplicitFields.None) implicitAqla = true;
                            }
                            if (item.TryGet("ExplicitPropertiesContract", out tmp) && tmp != null)
                            {
                                explicitPropertiesContract = (bool)tmp;
                            }
                            if (item.TryGet("SkipConstructor", out tmp)) metaType.UseConstructor = !(bool)tmp;
                            if (item.TryGet("IgnoreListHandling", out tmp)) metaType.IgnoreListHandling = (bool)tmp;
                            if (item.TryGet("NotAsReferenceDefault", out tmp)) metaType.AsReferenceDefault = !(bool)tmp;
                            if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) implicitFirstTag = (int)tmp;
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
            }
            if (!Helpers.IsNullOrEmpty(name)) metaType.Name = name;
            if (implicitMode != ImplicitFields.None)
            {
                if (family == AttributeFamily.ImplicitFallback)
                {
                    family = AttributeFamily.None;
                    if (CanUse(AttributeType.ProtoBuf))
                        family |= AttributeFamily.ProtoBuf;
                    if (CanUse(AttributeType.Aqla))
                        family |= AttributeFamily.Aqla;
                }
                else
                {
                    if (implicitAqla)
                        family &= AttributeFamily.Aqla;
                    else
                        family &= AttributeFamily.ProtoBuf; // with implicit fields, **only** proto attributes are important
                }
            }
            MethodInfo[] callbacks = null;

            BasicList members = new BasicList();

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
            MemberInfo[] foundList = type.GetMembers(isEnum ? BindingFlags.Public | BindingFlags.Static
                : BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif
            foreach (MemberInfo member in foundList)
            {
                if (member.DeclaringType != type) continue;
                if (member.IsDefined(_model.MapType(typeof(ProtoIgnoreAttribute)), true)) continue;
                if (partialIgnores != null && partialIgnores.Contains(member.Name)) continue;

                bool forced = false, isPublic, isField;
                Type effectiveType;


                PropertyInfo property;
                FieldInfo field;
                MethodInfo method;
                if ((property = member as PropertyInfo) != null)
                {
                    if (isEnum) continue; // wasn't expecting any props!

                    effectiveType = property.PropertyType;
                    isPublic = Helpers.GetGetMethod(property, false, false) != null;
                    if (isPublic && explicitPropertiesContract)
                        isPublic = Helpers.GetSetMethod(property, false, false) != null;
                    isField = false;
                    ApplyDefaultBehaviour_AddMembers(family, isEnum, partialMembers, dataMemberOffset, inferTagByName, implicitMode, members, member, ref forced, isPublic, isField, ref effectiveType);
                }
                else if ((field = member as FieldInfo) != null)
                {
                    effectiveType = field.FieldType;
                    isPublic = field.IsPublic;
                    isField = true;
                    if (isEnum && !field.IsStatic)
                    { // only care about static things on enums; WinRT has a __value instance field!
                        continue;
                    }
                    ApplyDefaultBehaviour_AddMembers(family, isEnum, partialMembers, dataMemberOffset, inferTagByName, implicitMode, members, member, ref forced, isPublic, isField, ref effectiveType);
                }
                else if ((method = member as MethodInfo) != null)
                {
                    if (isEnum) continue;
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
            var arr = new AqlaSerializer.SerializableMemberAttribute[members.Count];
            members.CopyTo(arr, 0);

            if (inferTagByName || implicitMode != ImplicitFields.None)
            {
                Array.Sort(arr);
                int nextTag = implicitFirstTag;
                foreach (var normalizedAttribute in arr)
                {
                    if (!normalizedAttribute.TagIsPinned) // if ProtoMember etc sets a tag, we'll trust it
                    {
                        normalizedAttribute.Rebase(nextTag++);
                    }
                }
            }

            foreach (var normalizedAttribute in arr)
            {
                ApplyDefaultBehaviour(metaType, isEnum, normalizedAttribute);
            }

            if (callbacks != null)
            {
                metaType.SetCallbacks(Coalesce(callbacks, 0, 4, 8), Coalesce(callbacks, 1, 5, 9),
                    Coalesce(callbacks, 2, 6, 10), Coalesce(callbacks, 3, 7, 11));
            }

            foreach (var normalizedAttribute in arr)
            {
                if (!isEnum && normalizedAttribute.Tag > 0)
                {
                    Type memberType = Helpers.GetMemberType(normalizedAttribute.Member);
                    if (!DisableAutoAddingMemberTypes
                        && GetContractFamily(memberType) != AttributeFamily.None)
                    {
                        FindOrAddType(memberType, true, false, false);
                    }
                }
            }
            
            if (baseType != null && GetContractFamily(baseType) != AttributeFamily.None)
            {
                if (FindMetaTypeWithoutAdd(baseType) != null)
                {
                    MetaType meta = _model[baseType];
                    if (!DisableAutoRegisteringSubtypes && !meta.IsList)
                        meta.AddSubType(meta.GetNextFreeFieldNumber(AutoRegisteringSubtypesFirstTag), type);
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

        protected virtual void ApplyDefaultBehaviour_AddMembers(AttributeFamily family, bool isEnum, IEnumerable partialMembers, int dataMemberOffset, bool inferTagByName, ImplicitFields implicitMode, IList members, MemberInfo member, ref bool forced, bool isPublic, bool isField, ref Type effectiveType)
        {
            switch (implicitMode)
            {
                case ImplicitFields.PublicFields:
                    if (isField & isPublic) forced = true;
                    break;
                case ImplicitFields.AllFields:
                    if (isField) forced = true;
                    break;
                case ImplicitFields.AllProperties:
                    if (!isField) forced = true;
                    break;
                case ImplicitFields.PublicFieldsAndProperties:
                    if (isPublic) forced = true;
                    break;
                case ImplicitFields.PublicProperties:
                    if (isPublic && !isField) forced = true;
                    break;
                case ImplicitFields.AllFieldsAndProperties:
                    forced = true;
                    break;
            }

            // we just don't like delegate types ;p
#if WINRT
            if (effectiveType.GetTypeInfo().IsSubclassOf(typeof(Delegate))) effectiveType = null;
#else
            if (effectiveType.IsSubclassOf(_model.MapType(typeof(Delegate)))) effectiveType = null;
#endif
            if (effectiveType != null)
            {
                var normalizedAttribute = NormalizeProtoMember(member, family, forced, isEnum, partialMembers, dataMemberOffset, inferTagByName);
                if (normalizedAttribute != null) members.Add(normalizedAttribute);
            }
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
            AttributeFamily family = AttributeFamily.None;

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
                if (family == AttributeFamily.None && ImplicitFallbackMode != ImplicitFields.None)
                {
                    if (Helpers.GetTypeCode(type) == ProtoTypeCode.Unknown
                        && type != _model.MapType(typeof(object))
                        && type != _model.MapType(typeof(ValueType)))
                    {
                        family = AttributeFamily.ImplicitFallback;
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

        protected virtual SerializableMemberAttribute NormalizeProtoMember(MemberInfo member, AttributeFamily family, bool forced, bool isEnum, IEnumerable partialMembers, int dataMemberOffset, bool inferByTagName)
        {
            if (member == null || (family == AttributeFamily.None && !isEnum)) return null; // nix
            int fieldNumber = int.MinValue, minAcceptFieldNumber = inferByTagName ? -1 : 1;
            string name = null;
            bool isPacked = false, ignore = false, done = false, isRequired = false, notAsReference = false, notAsReferenceHasValue = false, dynamicType = false, tagIsPinned = false, appendCollection = false;

            bool readOnly = !Helpers.CanWrite(_model, member);
            if (readOnly)
                appendCollection = true;

            DataFormat dataFormat = DataFormat.Default;
            if (isEnum) forced = true;
            AttributeMap[] attribs = AttributeMap.Create(_model, member, true);
            AttributeMap attrib = null;

            if (isEnum)
            {
                attrib = AttributeMap.GetAttribute(attribs, "ProtoBuf.ProtoIgnoreAttribute");
                if (attrib != null && CanUse(AttributeType.ProtoBuf))
                {
                    ignore = true;
                }
                else
                {
                    attrib = AttributeMap.GetAttribute(attribs, "AqlaSerializer.NonSerializableMemberAttribute");
                    if (attrib != null && CanUse(AttributeType.Aqla))
                    {
                        ignore = true;
                    }
                    else
                    {
#if WINRT || PORTABLE || CF || FX11
                    fieldNumber = Convert.ToInt32(((FieldInfo)member).GetValue(null));
#else
                        fieldNumber = Convert.ToInt32(((FieldInfo)member).GetRawConstantValue());
#endif
                        attrib = AttributeMap.GetAttribute(attribs, "ProtoBuf.ProtoEnumAttribute");
                        if (attrib != null && CanUse(AttributeType.ProtoBuf))
                        {
                            GetFieldName(ref name, attrib, "Name");
#if !FEAT_IKVM // IKVM can't access HasValue, but conveniently, Value will only be returned if set via ctor or property
                            if ((bool)Helpers.GetInstanceMethod(attrib.AttributeType
#if WINRT
                             .GetTypeInfo()
#endif
, "HasValue").Invoke(attrib.Target, null))
#endif
                            {
                                object tmp;
                                if (attrib.TryGet("Value", out tmp)) fieldNumber = (int)tmp;
                            }
                        }

                        attrib = AttributeMap.GetAttribute(attribs, "AqlaSerializer.EnumSerializableValueAttribute");
                        if (attrib != null && CanUse(AttributeType.Aqla))
                        {
#if !FEAT_IKVM // IKVM can't access HasValue, but conveniently, Value will only be returned if set via ctor or property
                            if ((bool)Helpers.GetInstanceMethod(attrib.AttributeType
#if WINRT
                             .GetTypeInfo()
#endif
, "HasValue").Invoke(attrib.Target, null))
#endif
                            {
                                object tmp;
                                if (attrib.TryGet("Value", out tmp)) fieldNumber = (int)tmp;
                            }
                        }
                    }

                }
                done = true;
            }

            if (!ignore && !done)
            {
                // always consider ProtoMember if not strict Aqla
                if (CanUse(AttributeType.ProtoBuf))
                {
                    attrib = AttributeMap.GetAttribute(attribs, "ProtoBuf.ProtoMemberAttribute");
                    GetIgnore(ref ignore, attrib, attribs, "ProtoBuf.ProtoIgnoreAttribute");

                    if (!ignore && attrib != null)
                    {
                        GetFieldNumber(ref fieldNumber, attrib, "Tag");
                        GetFieldName(ref name, attrib, "Name");
                        GetFieldBoolean(ref isRequired, attrib, "IsRequired");
                        GetFieldBoolean(ref isPacked, attrib, "IsPacked");
                        bool overwriteList = false;
                        GetFieldBoolean(ref overwriteList, attrib, "OverwriteList");
                        appendCollection = !overwriteList;

                        GetDataFormat(ref dataFormat, attrib, "DataFormat");

                        bool asRefHasValue = false;
#if !FEAT_IKVM
                        // IKVM can't access AsReferenceHasValue, but conveniently, AsReference will only be returned if set via ctor or property
                        GetFieldBoolean(ref asRefHasValue, attrib, "AsReferenceHasValue", false);
                        if (asRefHasValue)
#endif
                        {
                            bool value = false;
                            asRefHasValue = GetFieldBoolean(ref value, attrib, "AsReference", true);
                            if (asRefHasValue && !value) // if AsReference = true - use defaults
                            {
                                notAsReferenceHasValue = true;
                                notAsReference = true;
                            }
                        }

                        if (!asRefHasValue)
                        {
                            // by default enable for ProtoMember
                            notAsReferenceHasValue = true;
                            notAsReference = true;
                        }

                        GetFieldBoolean(ref dynamicType, attrib, "DynamicType");
                        done = tagIsPinned = fieldNumber > 0; // note minAcceptFieldNumber only applies to non-proto
                    }
                }

                // always consider SerializableMember if not strict ProtoBuf
                if (!done && !ignore && CanUse(AttributeType.Aqla))
                {
                    attrib = AttributeMap.GetAttribute(attribs, "AqlaSerializer.SerializableMemberAttribute");
                    GetIgnore(ref ignore, attrib, attribs, "AqlaSerializer.NonSerializableMemberAttribute");

                    if (!ignore && attrib != null)
                    {
                        GetFieldNumber(ref fieldNumber, attrib, "Tag");
                        GetFieldName(ref name, attrib, "Name");
                        GetFieldBoolean(ref isRequired, attrib, "IsRequired");
                        GetFieldBoolean(ref isPacked, attrib, "IsPacked");
                        GetFieldBoolean(ref appendCollection, attrib, "AppendCollection");
                        GetDataFormat(ref dataFormat, attrib, "DataFormat");

#if !FEAT_IKVM //
                        // IKVM can't access AsReferenceHasValue, but conveniently, AsReference will only be returned if set via ctor or property
                        GetFieldBoolean(ref notAsReferenceHasValue, attrib, "NotAsReferenceHasValue", false);
                        if (notAsReferenceHasValue)
#endif
                        {
                            notAsReferenceHasValue = GetFieldBoolean(ref notAsReference, attrib, "NotAsReference", true);
                        }
                        GetFieldBoolean(ref dynamicType, attrib, "DynamicType");
                        done = tagIsPinned = fieldNumber > 0; // note minAcceptFieldNumber only applies to non-proto
                    }
                }

                if (!done && partialMembers != null)
                {
                    foreach (AttributeMap ppma in partialMembers)
                    {
                        object tmp;
                        if (ppma.TryGet("MemberName", out tmp) && (string)tmp == member.Name)
                        {
                            GetFieldNumber(ref fieldNumber, ppma, "Tag");
                            GetFieldName(ref name, ppma, "Name");
                            GetFieldBoolean(ref isRequired, ppma, "IsRequired");
                            GetFieldBoolean(ref isPacked, ppma, "IsPacked");
                            GetDataFormat(ref dataFormat, ppma, "DataFormat");

                            if (ppma.AttributeType.FullName == "AqlaSerializer.NonSerializableMemberAttribute")
                            {
                                GetFieldBoolean(ref appendCollection, attrib, "AppendCollection");

#if !FEAT_IKVM //
                                // IKVM can't access AsReferenceHasValue, but conveniently, AsReference will only be returned if set via ctor or property
                                GetFieldBoolean(ref notAsReferenceHasValue, attrib, "NotAsReferenceHasValue", false);
                                if (notAsReferenceHasValue)
#endif
                                {
                                    notAsReferenceHasValue = GetFieldBoolean(ref notAsReference, attrib, "NotAsReference", true);
                                }
                            }
                            else // proto
                            {
                                bool overwriteList = false;
                                GetFieldBoolean(ref overwriteList, attrib, "OverwriteList");
                                appendCollection = !overwriteList;

                                bool asRefHasValue = false;
#if !FEAT_IKVM
                                // IKVM can't access AsReferenceHasValue, but conveniently, AsReference will only be returned if set via ctor or property
                                GetFieldBoolean(ref asRefHasValue, attrib, "AsReferenceHasValue", false);
                                if (asRefHasValue)
#endif
                                {
                                    bool value = false;
                                    asRefHasValue = GetFieldBoolean(ref value, attrib, "AsReference", true);
                                    if (asRefHasValue && !value) // if AsReference = true - use defaults
                                    {
                                        notAsReferenceHasValue = true;
                                        notAsReference = true;
                                    }
                                }

                                if (!asRefHasValue)
                                {
                                    // by default enable for ProtoMember
                                    notAsReferenceHasValue = true;
                                    notAsReference = true;
                                }

                            }

                            GetFieldBoolean(ref dynamicType, ppma, "DynamicType");
                            if (done = tagIsPinned = fieldNumber > 0) break; // note minAcceptFieldNumber only applies to non-proto
                        }
                    }
                }
            }

            if (!ignore && !done && HasFamily(family, AttributeFamily.DataContractSerialier))
            {
                attrib = AttributeMap.GetAttribute(attribs, "System.Runtime.Serialization.DataMemberAttribute");
                if (attrib != null)
                {
                    GetFieldNumber(ref fieldNumber, attrib, "Order");
                    GetFieldName(ref name, attrib, "Name");
                    GetFieldBoolean(ref isRequired, attrib, "IsRequired");
                    done = fieldNumber >= minAcceptFieldNumber;
                    if (done) fieldNumber += dataMemberOffset; // dataMemberOffset only applies to DCS flags, to allow us to "bump" WCF by a notch
                }
            }
            if (!ignore && !done && HasFamily(family, AttributeFamily.XmlSerializer))
            {
                attrib = AttributeMap.GetAttribute(attribs, "System.Xml.Serialization.XmlElementAttribute");
                if (attrib == null)
                {
                    attrib = AttributeMap.GetAttribute(attribs, "System.Xml.Serialization.XmlArrayAttribute");
                }
                GetIgnore(ref ignore, attrib, attribs, "System.Xml.Serialization.XmlIgnoreAttribute");
                if (attrib != null && !ignore)
                {
                    GetFieldNumber(ref fieldNumber, attrib, "Order");
                    GetFieldName(ref name, attrib, "ElementName");
                    done = fieldNumber >= minAcceptFieldNumber;
                }
            }
            if (!ignore && !done)
            {
                if (AttributeMap.GetAttribute(attribs, "System.NonSerializedAttribute") != null) ignore = true;
            }
            if (ignore || (fieldNumber < minAcceptFieldNumber && !forced)) return null;
            var result = new AqlaSerializer.SerializableMemberAttribute(fieldNumber, forced || inferByTagName);
            result.NotAsReference = notAsReference;
            result.NotAsReferenceHasValue = notAsReferenceHasValue;
            result.DataFormat = dataFormat;
            result.DynamicType = dynamicType;
            result.IsPacked = isPacked;
            if (readOnly && !appendCollection)
                throw new ProtoException("The property " + member.Name + " of " + member.DeclaringType.Name + " is not writable but AppendCollection is true!");
            result.AppendCollection = appendCollection;
            result.IsRequired = isRequired;
            result.Name = Helpers.IsNullOrEmpty(name) ? member.Name : name;
            result.Member = member;
            result.TagIsPinned = tagIsPinned;
            return result;
        }

        protected virtual void ApplyDefaultBehaviour(MetaType metaType, bool isEnum, AqlaSerializer.SerializableMemberAttribute normalizedAttribute)
        {
            MemberInfo member;
            if (normalizedAttribute == null || (member = normalizedAttribute.Member) == null) return; // nix

            AttributeMap[] attribs = AttributeMap.Create(_model, member, true);
            AttributeMap attrib;

            bool defaultValueSpecified = false;

            object defaultValue = null;
            if ((attrib = AttributeMap.GetAttribute(attribs, "System.ComponentModel.DefaultValueAttribute")) != null)
            {
                object tmp;
                if (attrib.TryGet("Value", out tmp))
                {
                    defaultValue = tmp;
                    defaultValueSpecified = true;
                }
            }

            var memberType = Helpers.GetMemberType(member);

            Type defaultType = null;
            if (metaType.Type.IsInterface)
                defaultType = FindDefaultInterfaceImplementation(memberType);

            if (isEnum || normalizedAttribute.Tag > 0)
            {
                if (defaultValueSpecified)
                    metaType.Add(normalizedAttribute, member, defaultValue, defaultType);
                else
                    metaType.Add(normalizedAttribute, member, defaultType);
            }
        }
        
        protected static void GetDataFormat(ref DataFormat value, AttributeMap attrib, string memberName)
        {
            if ((attrib == null) || (value != DataFormat.Default)) return;
            object obj;
            if (attrib.TryGet(memberName, out obj) && obj != null) value = (DataFormat)obj;
        }

        protected static void GetIgnore(ref bool ignore, AttributeMap attrib, AttributeMap[] attribs, string fullName)
        {
            if (ignore) return;
            ignore = AttributeMap.GetAttribute(attribs, fullName) != null;
            return;
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

        protected static void GetFieldNumber(ref int value, AttributeMap attrib, string memberName)
        {
            if (attrib == null || value > 0) return;
            object obj;
            if (attrib.TryGet(memberName, out obj) && obj != null) value = (int)obj;
        }
        protected static void GetFieldName(ref string name, AttributeMap attrib, string memberName)
        {
            if (attrib == null || !Helpers.IsNullOrEmpty(name)) return;
            object obj;
            if (attrib.TryGet(memberName, out obj) && obj != null) name = (string)obj;
        }

        public virtual bool GetAsReferenceDefault(Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
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
            return true;
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

        readonly RuntimeTypeModel _model;
        protected RuntimeTypeModel Model { get { return _model; } }

        static bool CanUse(AttributeType check, AttributeType required)
        {
            return (check & required) != 0;
        }

        bool CanUse(AttributeType required)
        {
            return (_acceptableAttributes & required) != 0;
        }

        private AttributeType _acceptableAttributes = AttributeType.All;

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
        /// Set to <see cref="ImplicitFields.AllFields"/> and use <see cref="DisableAutoTuples"/> to perform as BinaryFormatter
        /// </summary>
        public ImplicitFields ImplicitFallbackMode { get; set; }
        
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

        [Flags]
        public enum AttributeType
        {
            None = 0,
            Aqla = 1,
            ProtoBuf = 2,
            Xml = 4,
            DataContract = 8,

            All = Aqla | ProtoBuf | Xml | DataContract,
            NoAqla = ProtoBuf | Xml | DataContract,
            NoProtobuf = Aqla | Xml | DataContract,
        }

        public DefaultAutoAddStrategy(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            _model = model;
        }

    }
}
#endif