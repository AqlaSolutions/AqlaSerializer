// DynamicMethodCompiler.cs is from http://www.codeproject.com/Articles/14973/Dynamic-Code-Generation-vs-Reflection
// The Code Project Open License(CPOL) 1.02
// http://www.codeproject.com/info/cpol10.aspx
// Modified by Vladyslav Taranov 2016, for AqlaSerializer

#if !NO_RUNTIME && FEAT_COMPILER && !FEAT_IVKM 
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AqlaSerializer;
using AqlaSerializer.Internal;

namespace DynamicCompilationSpike
{
    sealed class DynamicMethodCompiler
    {
        // DynamicMethodCompiler
        private DynamicMethodCompiler() { }

        // CreateInstantiateObjectDelegate
        public static AccessorsCache.InstantiateObjectHandler CreateInstantiateObjectHandler(Type type)
        {
            ConstructorInfo constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null);
            if (constructorInfo == null)
            {
                throw new ArgumentException(string.Format("The type {0} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).", type));
            }

            DynamicMethod dynamicMethod = new DynamicMethod("InstantiateObject", MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard,
                typeof(object), null, type, true
                );
            ILGenerator generator = dynamicMethod.GetILGenerator();
            generator.Emit(OpCodes.Newobj, constructorInfo);
            generator.Emit(OpCodes.Ret);
            return (AccessorsCache.InstantiateObjectHandler)dynamicMethod.CreateDelegate(typeof(AccessorsCache.InstantiateObjectHandler));
        }

        #region Property Get

        public static AccessorsCache.GetHandler CreateGetHandler(Type type, PropertyInfo propertyInfo)
        {
            // we don't allow get for properties because if they mutate struct the changes won't be saved
            if (!propertyInfo.CanRead || propertyInfo.DeclaringType.IsValueType) return null;
            MethodInfo methodInfo;
            try
            {
                methodInfo = propertyInfo.GetGetMethod(true);
            }
            catch
            {
                try
                {
                    methodInfo = propertyInfo.GetGetMethod();
                }
                catch
                {
                    return null;
                }
            }
            if (methodInfo == null) return null;
            return CreateGetHandler(type, methodInfo);
        }

        public static AccessorsCache.GetHandler CreateGetHandler(Type type, MethodInfo methodInfo)
        {
            try
            {
                return CreateGetHandler(type, methodInfo, true);
            }
            catch
            {
                try
                {
                    return CreateGetHandler(type, methodInfo, false);
                }
                catch
                {
                    try
                    {
                        return CreateGetHandler(methodInfo.ReflectedType, methodInfo, false);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        static AccessorsCache.GetHandler CreateGetHandler(Type type, MethodInfo methodInfo, bool skipVisibility)
        {
            MethodInfo getMethodInfo = methodInfo;
            DynamicMethod dynamicGet = CreateGetDynamicMethod(type, skipVisibility);
            ILGenerator g = dynamicGet.GetILGenerator();
            bool isValueType = methodInfo.DeclaringType.IsValueType;
            LocalBuilder local = null;
            if (isValueType)
                local = g.DeclareLocal(Helpers.GetNullableUnderlyingType(methodInfo.DeclaringType) ?? methodInfo.DeclaringType);
            g.Emit(OpCodes.Ldarg_0);
            UnboxIfNeeded(methodInfo.DeclaringType, g);
            if (isValueType)
            {
                g.Emit(OpCodes.Stloc_S, local);
                g.Emit(OpCodes.Ldloca_S, local);
            }
            g.Emit(isValueType ? OpCodes.Call : OpCodes.Callvirt, methodInfo);
            BoxIfNeeded(getMethodInfo.ReturnType, g);
            g.Emit(OpCodes.Ret);

            return (AccessorsCache.GetHandler)dynamicGet.CreateDelegate(typeof(AccessorsCache.GetHandler));
        }

        #endregion
        
        #region Property Set

        internal static AccessorsCache.SetHandler CreateSetHandler(Type type, PropertyInfo propertyInfo)
        {
            if (!propertyInfo.CanWrite || propertyInfo.DeclaringType.IsValueType) return null;
            MethodInfo methodInfo;
            try
            {
                methodInfo = propertyInfo.GetSetMethod(true);
            }
            catch
            {
                try
                {
                    methodInfo = propertyInfo.GetSetMethod();
                }
                catch
                {
                    return null;
                }
            }

            if (methodInfo == null) return null;
            return CreateSetHandler(type, methodInfo);
        }

        public static AccessorsCache.SetHandler CreateSetHandler(Type type, MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType.IsValueType) return null;
            try
            {
                return CreateSetHandler(type, methodInfo, true);
            }
            catch
            {
                try
                {
                    return CreateSetHandler(type, methodInfo, false);
                }
                catch
                {
                    try
                    {
                        return CreateSetHandler(methodInfo.ReflectedType, methodInfo, false);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        static AccessorsCache.SetHandler CreateSetHandler(Type type, MethodInfo methodInfo, bool skipVisibility)
        {
            DynamicMethod dynamicSet = CreateSetDynamicMethod(type, skipVisibility);
            ILGenerator setGenerator = dynamicSet.GetILGenerator();
            setGenerator.Emit(OpCodes.Ldarg_0);
            UnboxIfNeeded(methodInfo.DeclaringType, setGenerator);
            setGenerator.Emit(OpCodes.Ldarg_1);
            UnboxIfNeeded(methodInfo.GetParameters()[0].ParameterType, setGenerator);
            setGenerator.Emit(methodInfo.DeclaringType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, methodInfo);
            setGenerator.Emit(OpCodes.Ret);

            return (AccessorsCache.SetHandler)dynamicSet.CreateDelegate(typeof(AccessorsCache.SetHandler));
        }

        #endregion

        #region Field Get

        public static AccessorsCache.GetHandler CreateGetHandler(Type type, FieldInfo fieldInfo)
        {
            try
            {
                return CreateGetHandler(type, fieldInfo, true);
            }
            catch
            {
                try
                {
                    return CreateGetHandler(type, fieldInfo, false);
                }
                catch
                {
                    return null;
                }
            }
        }

        static AccessorsCache.GetHandler CreateGetHandler(Type type, FieldInfo fieldInfo, bool skipVisibility)
        {
            DynamicMethod dynamicGet = CreateGetDynamicMethod(type, skipVisibility);
            ILGenerator getGenerator = dynamicGet.GetILGenerator();

            getGenerator.Emit(OpCodes.Ldarg_0);
            UnboxIfNeeded(fieldInfo.DeclaringType, getGenerator);
            getGenerator.Emit(OpCodes.Ldfld, fieldInfo);
            BoxIfNeeded(fieldInfo.FieldType, getGenerator);
            getGenerator.Emit(OpCodes.Ret);

            return (AccessorsCache.GetHandler)dynamicGet.CreateDelegate(typeof(AccessorsCache.GetHandler));
        }

        #endregion

        #region Field Set

        public static AccessorsCache.SetHandler CreateSetHandler(Type type, FieldInfo fieldInfo)
        {
            if (fieldInfo.DeclaringType.IsValueType|| fieldInfo.DeclaringType.IsValueType) return null;
            try
            {
                return CreateSetHandler(type, fieldInfo, true);
            }
            catch
            {
                try
                {
                    return CreateSetHandler(type, fieldInfo, false);
                }
                catch
                {
                    return null;
                }
            }
        }

        static AccessorsCache.SetHandler CreateSetHandler(Type type, FieldInfo fieldInfo, bool skipVisibility)
        {
            DynamicMethod dynamicSet = CreateSetDynamicMethod(type, skipVisibility);
            ILGenerator setGenerator = dynamicSet.GetILGenerator();

            setGenerator.Emit(OpCodes.Ldarg_0);
            UnboxIfNeeded(fieldInfo.DeclaringType, setGenerator);
            setGenerator.Emit(OpCodes.Ldarg_1);
            UnboxIfNeeded(fieldInfo.FieldType, setGenerator);
            setGenerator.Emit(OpCodes.Stfld, fieldInfo);
            setGenerator.Emit(OpCodes.Ret);

            return (AccessorsCache.SetHandler)dynamicSet.CreateDelegate(typeof(AccessorsCache.SetHandler));
        }

        #endregion

        // CreateGetDynamicMethod
        private static DynamicMethod CreateGetDynamicMethod(Type type, bool skipVisibility)
        {
            return new DynamicMethod("DynamicGet", typeof(object), new Type[] { typeof(object) }, type, skipVisibility);
        }

        // CreateSetDynamicMethod
        private static DynamicMethod CreateSetDynamicMethod(Type type, bool skipVisibility)
        {
            return new DynamicMethod("DynamicSet", typeof(void), new Type[] { typeof(object), typeof(object) }, type, skipVisibility);
        }

        // BoxIfNeeded
        private static void BoxIfNeeded(Type type, ILGenerator generator)
        {
            if (type.IsValueType)
            {
                generator.Emit(OpCodes.Box, type);
            }
        }

        // UnboxIfNeeded
        private static void UnboxIfNeeded(Type type, ILGenerator generator)
        {
            if (type.IsValueType)
            {
                generator.Emit(OpCodes.Unbox_Any, type);
            }
        }
    }
}
#endif