// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
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
    partial class MetaType
    {
        internal EnumSerializer.EnumPair[] GetEnumMap()
        {
            if (!Helpers.IsEnum(Type)) return null;
            if (GetFinalSettingsCopy().EnumPassthru.GetValueOrDefault()) return null;
            var fields = _fields.Cast<ValueMember>().ToArray();
            EnumSerializer.EnumPair[] result = new EnumSerializer.EnumPair[fields.Length];
            for (int i = 0; i < result.Length; i++)
            {
                ValueMember member = (ValueMember)fields[i];
                int wireValue = member.FieldNumber;
                object value = member.GetRawEnumValue();
                result[i] = new EnumSerializer.EnumPair(wireValue, value, member.MemberType);
            }
            return result;
        }

        internal static bool IsNetObjectValueDecoratorNecessary(RuntimeTypeModel m, ValueFormat format)
        {
            if (m.ProtoCompatibility.SuppressValueEnhancedFormat) return false;

            return format != ValueFormat.Compact;
        }

        internal static MetaType GetTypeForRootSerialization(MetaType source)
        {
            if (source.OmitTypeSearchForRootSerialization) return source;
            while (true)
            {
                bool set = source._settingsValueFinalSet;
                Helpers.MemoryBarrier();
                if (!set) break;
                if (source.BaseType == null) return source;
                source = source.BaseType;
            }
            RuntimeTypeModel model = source._model;
            int opaqueToken = 0;
            try
            {
                model.TakeLock(ref opaqueToken);

                while (source.BaseType != null)
                    source = source.BaseType;
                return source;

            }
            finally
            {
                model.ReleaseLock(opaqueToken);
            }
        }

        public static ConstructorInfo ResolveTupleConstructor(Type type, out MemberInfo[] mappedMembers)
        {
            mappedMembers = null;
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type.IsAbstract) return null; // as if!
            ConstructorInfo[] ctors = Helpers.GetConstructors(type, false);
            // need to have an interesting constructor to bother even checking this stuff
            if (ctors.Length == 0 || (ctors.Length == 1 && ctors[0].GetParameters().Length == 0)) return null;

            MemberInfo[] fieldsPropsUnfiltered = Helpers.GetInstanceFieldsAndProperties(type, true);
            BasicList memberList = new BasicList();
            // for most types we'll enforce that you need readonly, because that is what protobuf-net
            // always did historically; but: if you smell so much like a Tuple that it is *in your name*,
            // we'll let you past that
            bool demandReadOnly = type.Name.IndexOf("Tuple", StringComparison.OrdinalIgnoreCase) < 0;
            for (int i = 0; i < fieldsPropsUnfiltered.Length; i++)
            {
                PropertyInfo prop = fieldsPropsUnfiltered[i] as PropertyInfo;
                if (prop != null)
                {
                    if (!prop.CanRead) return null; // no use if can't read
                    if (demandReadOnly && prop.CanWrite && Helpers.GetSetMethod(prop, false, false) != null) return null; // don't allow a public set (need to allow non-public to handle Mono's KeyValuePair<,>)
                    memberList.Add(prop);
                }
                else
                {
                    FieldInfo field = fieldsPropsUnfiltered[i] as FieldInfo;
                    if (field != null)
                    {
                        if (!field.IsInitOnly && !Helpers.IsValueType(type)) return null; // all public fields must be readonly to be counted a tuple but there is an exclusion for structs
                        memberList.Add(field);
                    }
                }
            }
            if (memberList.Count == 0)
            {
                return null;
            }

            MemberInfo[] members = new MemberInfo[memberList.Count];
            memberList.CopyTo(members, 0);

            int[] mapping = new int[members.Length];
            int found = 0;
            ConstructorInfo result = null;
            mappedMembers = new MemberInfo[mapping.Length];
            for (int i = 0; i < ctors.Length; i++)
            {
                ParameterInfo[] parameters = ctors[i].GetParameters();

                if (parameters.Length != members.Length) continue;

                // reset the mappings to test
                for (int j = 0; j < mapping.Length; j++) mapping[j] = -1;

                for (int j = 0; j < parameters.Length; j++)
                {
                    for (int k = 0; k < members.Length; k++)
                    {
                        if (string.Compare(parameters[j].Name, members[k].Name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                        Type memberType = Helpers.GetMemberType(members[k]);
                        if (memberType != parameters[j].ParameterType) continue;

                        mapping[j] = k;
                    }
                }
                // did we map all?
                bool notMapped = false;
                for (int j = 0; j < mapping.Length; j++)
                {
                    if (mapping[j] < 0)
                    {
                        notMapped = true;
                        break;
                    }
                    mappedMembers[j] = members[mapping[j]];
                }

                if (notMapped) continue;
                found++;
                result = ctors[i];

            }
            return found == 1 ? result : null;
        }

        internal static void ResolveListTypes(RuntimeTypeModel model, Type type, ref Type itemType, ref Type defaultType)
        {
            if (type == null) return;
            if (Helpers.GetTypeCode(type) != ProtoTypeCode.Unknown) return; // don't try this[type] for inbuilts
            // handle arrays
            if (type.IsArray)
            {
                itemType = type.GetElementType();
                if (itemType == model.MapType(typeof(byte)))
                {
                    defaultType = itemType = null;
                }
                else
                {
                    defaultType = type;
                }
            }
            // handle lists
            if (itemType == null) { itemType = TypeModel.GetListItemType(model, type); }

            if (itemType != null && defaultType == null)
            {
                if (type.IsClass && !type.IsAbstract && Helpers.GetConstructor(type, Helpers.EmptyTypes, true) != null)
                {
                    defaultType = type;
                }
                if (defaultType == null)
                {
                    if (type.IsInterface)
                    {
                        Type[] genArgs;
                        if (type.IsGenericType &&
                            (type.GetGenericTypeDefinition() == model.MapType(typeof(System.Collections.Generic.IDictionary<,>))
                                || type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlyDictionary`2")
                            && itemType == model.MapType(typeof(System.Collections.Generic.KeyValuePair<,>)).MakeGenericType(genArgs = type.GetGenericArguments()))
                        {
                            defaultType = model.MapType(typeof(System.Collections.Generic.Dictionary<,>)).MakeGenericType(genArgs);
                        }
                        else if (type.IsGenericType && (type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.ISet`1" || type.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlySet`1"))
                        {
                            defaultType = model.MapType(typeof(System.Collections.Generic.HashSet<>)).MakeGenericType(itemType);
                        }
                        else
                        {
                            defaultType = model.MapType(typeof(System.Collections.Generic.List<>)).MakeGenericType(itemType);
                        }
                    }
                }
                // verify that the default type is appropriate
                if (defaultType != null && !Helpers.IsAssignableFrom(type, defaultType)) { defaultType = null; }
            }
        }

        public static bool IsDictionaryOrListInterface(RuntimeTypeModel model, Type type, out Type defaultType)
        {
            defaultType = null;
            if (!Helpers.IsInterface(type)) return false;
            Type[] genArgs;
            var itemType = TypeModel.GetListItemType(model, type);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == model.MapType(typeof(System.Collections.Generic.IDictionary<,>))
                    && itemType == model.MapType(typeof(System.Collections.Generic.KeyValuePair<,>)).MakeGenericType(genArgs = type.GetGenericArguments()))
            {
                defaultType = model.MapType(typeof(System.Collections.Generic.Dictionary<,>)).MakeGenericType(genArgs);
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == model.MapType(typeof(System.Collections.Generic.IList<>)).MakeGenericType(genArgs = type.GetGenericArguments()))
            {
                defaultType = model.MapType(typeof(System.Collections.Generic.List<>)).MakeGenericType(genArgs);
                return true;
            }
            return false;

        }


        private MethodInfo ResolveMethod(string name, bool instance)
        {
            if (Helpers.IsNullOrEmpty(name)) return null;
            return instance ? Helpers.GetInstanceMethod(Type, name) : Helpers.GetStaticMethod(Type, name);
        }
        internal static Exception InbuiltType(Type type)
        {
            return new ArgumentException("Data of this type has inbuilt behaviour, and cannot be added to a model in this way: " + type.FullName);
        }
    }
}
#endif