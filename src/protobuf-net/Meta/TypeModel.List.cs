// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.IO;

using System.Collections;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    partial class TypeModel
    {

#if WINRT
        private static readonly System.Reflection.TypeInfo ilist = typeof(IList).GetTypeInfo();
#else
        private static readonly System.Type ilist = typeof(IList);
#endif
        internal static MethodInfo ResolveListAdd(TypeModel model, Type listType, Type itemType, out bool isList)
        {
#if WINRT
            TypeInfo listTypeInfo = listType.GetTypeInfo();
#else
            Type listTypeInfo = listType;
#endif
            isList = model.MapType(ilist).IsAssignableFrom(listTypeInfo);

            Type[] types = { itemType };
            MethodInfo add = Helpers.GetInstanceMethod(listTypeInfo, "Add", types);

#if !NO_GENERICS
            if (add == null)
            {   // fallback: look for ICollection<T>'s Add(typedObject) method

                bool forceList = listTypeInfo.IsInterface &&
                    model.MapType(typeof(System.Collections.Generic.IEnumerable<>)).MakeGenericType(types)
#if WINRT
                    .GetTypeInfo()
#endif
.IsAssignableFrom(listTypeInfo);

#if WINRT
                TypeInfo constuctedListType = typeof(System.Collections.Generic.ICollection<>).MakeGenericType(types).GetTypeInfo();
#else
                Type constuctedListType = model.MapType(typeof(System.Collections.Generic.ICollection<>)).MakeGenericType(types);
#endif
                if (forceList || constuctedListType.IsAssignableFrom(listTypeInfo))
                {
                    add = Helpers.GetInstanceMethod(constuctedListType, "Add", types);
                }
            }

            if (add == null)
            {

#if WINRT
                foreach (Type tmpType in listTypeInfo.ImplementedInterfaces)
#else
                foreach (Type interfaceType in listTypeInfo.GetInterfaces())
#endif
                {
#if WINRT
                    TypeInfo interfaceType = tmpType.GetTypeInfo();
#endif
                    if (interfaceType.Name == "IProducerConsumerCollection`1" && interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition().FullName == "System.Collections.Concurrent.IProducerConsumerCollection`1")
                    {
                        add = Helpers.GetInstanceMethod(interfaceType, "TryAdd", types);
                        if (add != null) break;
                    }
                }
            }
#endif

            if (add == null)
            {   // fallback: look for a public list.Add(object) method
                types[0] = model.MapType(typeof(object));
                add = Helpers.GetInstanceMethod(listTypeInfo, "Add", types);
            }
            if (add == null && isList)
            {   // fallback: look for IList's Add(object) method
                add = Helpers.GetInstanceMethod(model.MapType(ilist), "Add", types);
            }
            return add;
        }
        internal static Type GetListItemType(TypeModel model, Type listType)
        {
            Helpers.DebugAssert(listType != null);

#if WINRT
            TypeInfo listTypeInfo = listType.GetTypeInfo();
            if (listType == typeof(string) || listType.IsArray
                || !typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(listTypeInfo)) return null;
#else
            if (listType == model.MapType(typeof(string)) || listType.IsArray
                || !model.MapType(typeof(IEnumerable)).IsAssignableFrom(listType))
                return null;
#endif

            BasicList candidates = new BasicList();
#if WINRT
            foreach (MethodInfo method in listType.GetRuntimeMethods())
#else
            foreach (MethodInfo method in listType.GetMethods())
#endif
            {
                if (method.IsStatic || method.Name != "Add") continue;
                ParameterInfo[] parameters = method.GetParameters();
                Type paramType;
                if (parameters.Length == 1 && !candidates.Contains(paramType = parameters[0].ParameterType))
                {
                    candidates.Add(paramType);
                }
            }

            string name = listType.Name;
            bool isQueueStack = name != null && (name.IndexOf("Queue", System.StringComparison.Ordinal) >= 0 || name.IndexOf("Stack", System.StringComparison.Ordinal) >= 0);
#if !NO_GENERICS
            if (!isQueueStack)
            {
                TestEnumerableListPatterns(model, candidates, listType);
#if WINRT
                foreach (Type iType in listTypeInfo.ImplementedInterfaces)
                {
                    TestEnumerableListPatterns(model, candidates, iType);
                }
#else
                foreach (Type iType in listType.GetInterfaces())
                {
                    TestEnumerableListPatterns(model, candidates, iType);
                }
#endif
            }
#endif

#if WINRT
            // more convenient GetProperty overload not supported on all platforms
            foreach (PropertyInfo indexer in listType.GetRuntimeProperties())
            {
                if (indexer.Name != "Item" || candidates.Contains(indexer.PropertyType)) continue;
                ParameterInfo[] args = indexer.GetIndexParameters();
                if (args.Length != 1 || args[0].ParameterType != typeof(int)) continue;
                MethodInfo getter = indexer.GetMethod;
                if (getter == null || getter.IsStatic) continue;
                candidates.Add(indexer.PropertyType);
            }
#else
            // more convenient GetProperty overload not supported on all platforms
            foreach (PropertyInfo indexer in listType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (indexer.Name != "Item" || candidates.Contains(indexer.PropertyType)) continue;
                ParameterInfo[] args = indexer.GetIndexParameters();
                if (args.Length != 1 || args[0].ParameterType != model.MapType(typeof(int))) continue;
                candidates.Add(indexer.PropertyType);
            }
#endif

            switch (candidates.Count)
            {
                case 0:
                    return null;
                case 1:
                    if ((Type)candidates[0] == listType) return null; // recursive
                    return (Type)candidates[0];
                case 2:
                    if ((Type)candidates[0] != listType && CheckDictionaryAccessors(model, (Type)candidates[0], (Type)candidates[1])) return (Type)candidates[0];
                    if ((Type)candidates[1] != listType && CheckDictionaryAccessors(model, (Type)candidates[1], (Type)candidates[0])) return (Type)candidates[1];
                    break;
            }

            return null;
        }

        private static void TestEnumerableListPatterns(TypeModel model, BasicList candidates, Type iType)
        {

#if WINRT
            TypeInfo iTypeInfo = iType.GetTypeInfo();
            if (iTypeInfo.IsGenericType)
            {
                Type typeDef = iTypeInfo.GetGenericTypeDefinition();
                if(
                   typeDef == model.MapType(typeof(System.Collections.Generic.IEnumerable<>))
                || typeDef == model.MapType(typeof(System.Collections.Generic.ICollection<>))
                || typeDef.GetTypeInfo().FullName == "System.Collections.Concurrent.IProducerConsumerCollection`1")
                {
                        
                    Type[] iTypeArgs = iTypeInfo.GenericTypeArguments;
                    if (!candidates.Contains(iTypeArgs[0]))
                    {
                        candidates.Add(iTypeArgs[0]);
                    }
                }
            }
#elif !NO_GENERICS
            if (iType.IsGenericType)
            {
                Type typeDef = iType.GetGenericTypeDefinition();
                if (typeDef == model.MapType(typeof(System.Collections.Generic.IEnumerable<>))
                    || typeDef == model.MapType(typeof(System.Collections.Generic.ICollection<>))
                    || typeDef.FullName == "System.Collections.Concurrent.IProducerConsumerCollection`1")
                {
                    Type[] iTypeArgs = iType.GetGenericArguments();
                    if (!candidates.Contains(iTypeArgs[0]))
                    {
                        candidates.Add(iTypeArgs[0]);
                    }
                }
            }
#endif
        }

        private static bool CheckDictionaryAccessors(TypeModel model, Type pair, Type value)
        {

#if NO_GENERICS
            return false;
#elif WINRT
            TypeInfo finalType = pair.GetTypeInfo();
            return finalType.IsGenericType && finalType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.KeyValuePair<,>)
                && finalType.GenericTypeArguments[1] == value;
#else
            return pair.IsGenericType && pair.GetGenericTypeDefinition() == model.MapType(typeof(System.Collections.Generic.KeyValuePair<,>))
                && pair.GetGenericArguments()[1] == value;
#endif
        }

#if !FEAT_IKVM
        private bool TryDeserializeList(TypeModel model, ProtoReader reader, BinaryDataFormat format, int tag, Type listType, Type itemType, bool isRoot, ref object value)
        {
            bool isList;
            MethodInfo addMethod = TypeModel.ResolveListAdd(model, listType, itemType, out isList);
            if (addMethod == null) throw new NotSupportedException("Unknown list variant: " + listType.FullName);
            bool found = false;
            object nextItem = null;
            IList list = value as IList;
            object[] args = isList ? null : new object[1];
            BasicList arraySurrogate = listType.IsArray ? new BasicList() : null;

            while (TryDeserializeAuxiliaryType(reader, format, tag, itemType, ref nextItem, true, true, true, true, isRoot))
            {
                found = true;
                if (value == null && arraySurrogate == null)
                {
                    value = CreateListInstance(listType, itemType);
                    if (value != null)
                        ProtoReader.NoteObject(value, reader);
                    list = value as IList;
                }
                if (list != null)
                {
                    list.Add(nextItem);
                }
                else if (arraySurrogate != null)
                {
                    arraySurrogate.Add(nextItem);
                }
                else
                {
                    args[0] = nextItem;
                    addMethod.Invoke(value, args);
                }
                nextItem = null;
            }
            if (arraySurrogate != null)
            {
                Array newArray;
                if (value != null)
                {
                    if (arraySurrogate.Count == 0)
                    {   // we'll stay with what we had, thanks
                    }
                    else
                    {
                        Array existing = (Array)value;
                        newArray = Array.CreateInstance(itemType, existing.Length + arraySurrogate.Count);
                        Array.Copy(existing, newArray, existing.Length);
                        arraySurrogate.CopyTo(newArray, existing.Length);
                        value = newArray;
                    }
                }
                else
                {
                    newArray = Array.CreateInstance(itemType, arraySurrogate.Count);
                    arraySurrogate.CopyTo(newArray, 0);
                    value = newArray;
                    ProtoReader.NoteObject(value, reader);
                }
            }
            return found;
        }

        private static object CreateListInstance(Type listType, Type itemType)
        {
            Type concreteListType = listType;

            if (listType.IsArray)
            {
                return Array.CreateInstance(itemType, 0);
            }

#if WINRT
            TypeInfo listTypeInfo = listType.GetTypeInfo();
            if (!listTypeInfo.IsClass || listTypeInfo.IsAbstract ||
                Helpers.GetConstructor(listTypeInfo, Helpers.EmptyTypes, true) == null)
#else
            if (!listType.IsClass || listType.IsAbstract ||
                Helpers.GetConstructor(listType, Helpers.EmptyTypes, true) == null)
#endif
            {
                string fullName;
                bool handled = false;
#if WINRT
                if (listTypeInfo.IsInterface &&
#else
                if (listType.IsInterface &&
#endif
 (fullName = listType.FullName) != null && fullName.IndexOf("Dictionary", System.StringComparison.Ordinal) >= 0) // have to try to be frugal here...
                {
#if !NO_GENERICS
#if WINRT
                    TypeInfo finalType = listType.GetTypeInfo();
                    if (finalType.IsGenericType && finalType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IDictionary<,>))
                    {
                        Type[] genericTypes = listType.GenericTypeArguments;
                        concreteListType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(genericTypes);
                        handled = true;
                    }
#else
                    if (listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IDictionary<,>))
                    {
                        Type[] genericTypes = listType.GetGenericArguments();
                        concreteListType = typeof(System.Collections.Generic.Dictionary<,>).MakeGenericType(genericTypes);
                        handled = true;
                    }
#endif
#endif
#if !SILVERLIGHT && !WINRT && !PORTABLE
                    if (!handled && listType == typeof(IDictionary))
                    {
                        concreteListType = typeof(Hashtable);
                        handled = true;
                    }
#endif
                }
#if !NO_GENERICS
                if (!handled)
                {
                    concreteListType = typeof(System.Collections.Generic.List<>).MakeGenericType(itemType);
                    handled = true;
                }
#endif

#if !SILVERLIGHT && !WINRT && !PORTABLE
                if (!handled)
                {
                    concreteListType = typeof(ArrayList);
                    handled = true;
                }
#endif
            }
            return Activator.CreateInstance(concreteListType);
        }
#endif
    }
}
