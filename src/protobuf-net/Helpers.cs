// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections;
using System.ComponentModel;
using System.Threading;
using System.Text;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

#if WINRT
using System.Linq;
#endif

using AqlaSerializer.Meta;
using AltLinq; using System.Linq;

namespace AqlaSerializer
{
    static class NullRefExtensions
    {
        public static bool IsNullRef(this object obj)
        {
            return ReferenceEquals(obj, null);
        }
    }

    /// <summary>
    /// Not all frameworks are created equal (fx1.1 vs fx2.0,
    /// micro-framework, compact-framework,
    /// silverlight, etc). This class simply wraps up a few things that would
    /// otherwise make the real code unnecessarily messy, providing fallback
    /// implementations if necessary.
    /// </summary>
    internal sealed class Helpers
    {
        private Helpers() { }

#if WINRT
        public static TypeInfo GetTypeInfo(Type type)
        {
            return type.GetTypeInfo();
        }

        public static TypeInfo GetTypeInfo(TypeInfo type)
        {
            return type;
        }

        
        public static Delegate CreateDelegate(Type type, MethodInfo method)
        {
            return method.CreateDelegate(type);
        }
#else
        public static Type GetTypeInfo(Type type)
        {
            return type;
        }

        public static Delegate CreateDelegate(System.Type type, System.Reflection.MethodInfo method)
        {
            return Delegate.CreateDelegate(type, method);
        }

#endif

        public static void MemoryBarrier()
        {
#if !WINRT
            Thread.MemoryBarrier();
#else
            Interlocked.MemoryBarrier();
#endif
        }

        public static int GetEnumMemberUnderlyingValue(MemberInfo member)
        {
#if WINRT || PORTABLE || CF || FX11
            return Convert.ToInt32(((FieldInfo)member).GetValue(null));
#else
            return Convert.ToInt32(((FieldInfo)member).GetRawConstantValue());
#endif
        }

#if FEAT_IKVM
        public static int GetEnumMemberUnderlyingValue(System.Reflection.MemberInfo member)
        {
            return Convert.ToInt32(((System.Reflection.FieldInfo)member).GetRawConstantValue());
        }
#endif
        public static bool IsInstanceOfType(Type type, object obj)
        {
#if WINRT
            return obj != null && type.GetTypeInfo().IsAssignableFrom(obj.GetType().GetTypeInfo());
#elif FEAT_IKVM
            throw new NotSupportedException();
#else
            return obj != null && type.IsInstanceOfType(obj);
#endif
        }

        public static bool IsInterface(Type type)
        {
#if WINRT
            return type.GetTypeInfo().IsInterface;
#else
            return type.IsInterface;
#endif
        }

        public static bool IsAbstract(Type type)
        {
#if WINRT
            return type.GetTypeInfo().IsAbstract;
#else
            return type.IsAbstract;
#endif
        }

        public static Assembly GetAssembly(Type type)
        {
#if WINRT
            return type.GetTypeInfo().Assembly;
#else
            return type.Assembly;
#endif
        }

        public static bool IsGenericTypeDefinition(Type type)
        {
#if WINRT
            return type.GetTypeInfo().IsGenericTypeDefinition;
#else
            return type.IsGenericTypeDefinition;
#endif
        }

        public static bool IsGenericType(Type type)
        {
#if WINRT
            return type.GetTypeInfo().IsGenericType;
#else
            return type.IsGenericType;
#endif
        }

        public static Type[] GetTypes(Assembly assembly)
        {
#if WINRT
            return assembly.DefinedTypes.Select(x=>x.AsType()).ToArray();
#else
#if FEAT_IKVM
            return assembly.GetTypes();
#else
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types;
            }
#endif
#endif
        }

        public static Type[] GetExportedTypes(Assembly assembly)
        {
#if FEAT_IKVM
            return GetTypes(assembly);
#else
#if WINRT
            return assembly.ExportedTypes.ToArray();
#else
#if FEAT_IKVM
            return assembly.GetExportedTypes();
#else
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types;
            }
#endif
#endif
#endif
        }

        public static StringBuilder AppendLine(StringBuilder builder)
        {
            return builder.AppendLine();
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugWriteLine(string message, object obj)
        {
#if DEBUG
            string suffix;
            try
            {
                suffix = obj?.ToString() ?? "(null)";
            }
            catch
            {
                suffix = "(exception)";
            }
            DebugWriteLine(message + ": " + suffix);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugWriteLine(string message)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(message);
#endif
        }
        [System.Diagnostics.Conditional("TRACE")]
        public static void TraceWriteLine(string message)
        {
#if TRACE
#if MF
            Microsoft.SPOT.Trace.Print(message);
#elif SILVERLIGHT || MONODROID || CF2 || WINRT || IOS || PORTABLE
            System.Diagnostics.Debug.WriteLine(message);
#else
            System.Diagnostics.Trace.WriteLine(message);
#endif
#endif
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugAssert(bool condition, string message)
        {
#if DEBUG
            if (!condition)
            {
#pragma warning disable RCS1178 // Call Debug.Fail instead of Debug.Assert.
                System.Diagnostics.Debug.Assert(false, message);
#pragma warning restore RCS1178 // Call Debug.Fail instead of Debug.Assert.
            }
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugAssert(bool condition, string message, params object[] args)
        {
#if DEBUG
            if (!condition) DebugAssert(false, string.Format(message, args));
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugAssert(bool condition)
        {
#if DEBUG   
            if (!condition && System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
            System.Diagnostics.Debug.Assert(condition);
#endif
        }

        public static void Sort(int[] keys, object[] values)
        {
#if !WINRT && !PORTABLE
            Array.Sort(keys, values);
#else
            // bubble-sort; it'll work on MF, has small code,
            // and works well-enough for our sizes. This approach
            // also allows us to do `int` compares without having
            // to go via IComparable etc, so win:win
            bool swapped;
            do {
                swapped = false;
                for (int i = 1; i < keys.Length; i++) {
                    if (keys[i - 1] > keys[i]) {
                        int tmpKey = keys[i];
                        keys[i] = keys[i - 1];
                        keys[i - 1] = tmpKey;
                        object tmpValue = values[i];
                        values[i] = values[i - 1];
                        values[i - 1] = tmpValue;
                        swapped = true;
                    }
                }
            } while (swapped);
#endif
        }

        public static void Sort<T, V>(T[] keys, V[] values, System.Collections.Generic.IComparer<T> comparer = null)
        {
            if (comparer == null) comparer = System.Collections.Generic.Comparer<T>.Default;
#if !WINRT && !PORTABLE
            Array.Sort(keys, values, comparer);
#else
            // bubble-sort; it'll work on MF, has small code,
            // and works well-enough for our sizes. This approach
            // also allows us to do `int` compares without having
            // to go via IComparable etc, so win:win
            bool swapped;
            do {
                swapped = false;
                for (int i = 1; i < keys.Length; i++) {
                    if (comparer.Compare(keys[i - 1], keys[i])>0) {
                        var tmpKey = keys[i];
                        keys[i] = keys[i - 1];
                        keys[i - 1] = tmpKey;
                        var tmpValue = values[i];
                        values[i] = values[i - 1];
                        values[i - 1] = tmpValue;
                        swapped = true;
                    }
                }
            } while (swapped);
#endif
        }
        
        internal static MethodInfo GetInstanceMethod(Type declaringType, string name)
        {
            return declaringType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        internal static MethodInfo GetStaticMethod(Type declaringType, string name)
        {
            return declaringType.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
        internal static MethodInfo GetInstanceMethod(Type declaringType, string name, Type[] types)
        {
            if(types == null) types = EmptyTypes;
#if PORTABLE
            MethodInfo method = declaringType.GetMethod(name, types);
            if (method != null && method.IsStatic) method = null;
            return method;
#else
            return declaringType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, types, null);
#endif
        }

        internal static bool IsSubclassOf(Type type, Type baseClass)
        {
            return type.IsSubclassOf(baseClass);
        }
        public static readonly Type[] EmptyTypes =
#if PORTABLE || WINRT || CF2 || CF35
            new Type[0];
#else
            Type.EmptyTypes;
#endif
        
        public static object GetPropertyValue(System.Reflection.PropertyInfo prop, object instance)
        {
            return GetPropertyValue(prop, instance, null);
        }

        public static object GetPropertyValue(System.Reflection.PropertyInfo prop, object instance, object[] index)
        {
#if !UNITY && (PORTABLE || WINRT || CF2 || CF35)
            return prop.GetValue(instance, index);
#else
            return prop.GetValue(instance, index);
#endif
        }

#if FEAT_IKVM
        public static ProtoTypeCode GetTypeCode(IKVM.Reflection.Type type)
        {
            TypeCode code = IKVM.Reflection.Type.GetTypeCode(type);
            switch (code)
            {
                case TypeCode.Empty:
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                case TypeCode.String:
                    return (ProtoTypeCode)code;
            }
            switch(type.FullName)
            {
                case "System.TimeSpan": return ProtoTypeCode.TimeSpan;
                case "System.Guid": return ProtoTypeCode.Guid;
                case "System.Uri": return ProtoTypeCode.Uri;
                case "System.Byte[]": return ProtoTypeCode.ByteArray;
                case "System.Type": return ProtoTypeCode.Type;
            }
            return ProtoTypeCode.Unknown;
        }
#endif

        public static ProtoTypeCode GetTypeCode(System.Type type)
        {
            if (IsAssignableFrom(typeof(System.Type), type)) return ProtoTypeCode.Type;
#if WINRT
            
            int idx = Array.IndexOf<Type>(knownTypes, type);
            if (idx >= 0) return knownCodes[idx];
            return type == null ? ProtoTypeCode.Empty : ProtoTypeCode.Unknown;
#else
            TypeCode code = System.Type.GetTypeCode(type);
            switch (code)
            {
                case TypeCode.Empty:
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                case TypeCode.String:
                    return (ProtoTypeCode)code;
            }
            if (type == typeof(TimeSpan)) return ProtoTypeCode.TimeSpan;
            if (type == typeof(Guid)) return ProtoTypeCode.Guid;
            if (type == typeof(Uri)) return ProtoTypeCode.Uri;
#if PORTABLE
            // In PCLs, the Uri type may not match (WinRT uses Internal/Uri, .Net uses System/Uri), so match on the full name instead
            if (type.FullName == typeof(Uri).FullName) return ProtoTypeCode.Uri;
#endif
            if (type == typeof(byte[])) return ProtoTypeCode.ByteArray;
            
            return ProtoTypeCode.Unknown;
#endif
        }

        
#if FEAT_IKVM
        internal static IKVM.Reflection.Type GetNullableUnderlyingType(IKVM.Reflection.Type type)
        {
            if (type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition().FullName == "System.Nullable`1")
            {
                return type.GetGenericArguments()[0];
            }
            return null;
        }
#endif

        internal static System.Type GetNullableUnderlyingType(System.Type type)
        {
            return Nullable.GetUnderlyingType(type);
        }
        
        internal static MethodInfo GetGetMethod(PropertyInfo property, bool nonPublic, bool allowInternal)
        {
            if (property == null) return null;
            MethodInfo method = property.GetGetMethod(nonPublic);
            if (method == null && !nonPublic && allowInternal)
            { // could be "internal" or "protected internal"; look for a non-public, then back-check
                method = property.GetGetMethod(true);
                if (method != null && !(method.IsAssembly || method.IsFamilyOrAssembly))
                {
                    method = null;
                }
            }
            return method;
        }
#if FEAT_IKVM
        internal static System.Reflection.MethodInfo GetGetMethod(System.Reflection.PropertyInfo property, bool nonPublic, bool allowInternal)
        {
            if (property == null) return null;
            var method = property.GetGetMethod(nonPublic);
            if (method == null && !nonPublic && allowInternal)
            { // could be "internal" or "protected internal"; look for a non-public, then back-check
                method = property.GetGetMethod(true);
                if (method != null && !(method.IsAssembly || method.IsFamilyOrAssembly))
                {
                    method = null;
                }
            }
            return method;
        }
#endif

        internal static MethodInfo GetSetMethod(PropertyInfo property, bool nonPublic, bool allowInternal)
        {
            if (property == null) return null;
            MethodInfo method = property.GetSetMethod(nonPublic);
            if (method == null && !nonPublic && allowInternal)
            { // could be "internal" or "protected internal"; look for a non-public, then back-check
                method = property.GetSetMethod(true);
                if (method != null && !(method.IsAssembly || method.IsFamilyOrAssembly))
                {
                    method = null;
                }
            }
            return method;

        }
#if FEAT_IKVM
        internal static System.Reflection.MethodInfo GetSetMethod(System.Reflection.PropertyInfo property, bool nonPublic, bool allowInternal)
        {
            if (property == null) return null;
            var method = property.GetSetMethod(nonPublic);
            if (method == null && !nonPublic && allowInternal)
            { // could be "internal" or "protected internal"; look for a non-public, then back-check
                method = property.GetSetMethod(true);
                if (method != null && !(method.IsAssembly || method.IsFamilyOrAssembly))
                {
                    method = null;
                }
            }
            return method;
        }
#endif

#if FEAT_IKVM
        internal static bool IsMatch(IKVM.Reflection.ParameterInfo[] parameters, IKVM.Reflection.Type[] parameterTypes)
        {
            if (parameterTypes == null) parameterTypes = Helpers.EmptyTypes;
            if (parameters.Length != parameterTypes.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i]) return false;
            }
            return true;
        }
#endif

        internal static ConstructorInfo GetConstructor(Type type, Type[] parameterTypes, bool nonPublic)
        {
#if PORTABLE
            // pretty sure this will only ever return public, but...
            ConstructorInfo ctor = type.GetConstructor(parameterTypes);
            return (ctor != null && (nonPublic || ctor.IsPublic)) ? ctor : null;
#else
            return type.GetConstructor(
                nonPublic ? BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                          : BindingFlags.Instance | BindingFlags.Public,
                    null, parameterTypes, null);
#endif

        }
        internal static ConstructorInfo[] GetConstructors(Type type, bool nonPublic)
        {
            return type.GetConstructors(
                nonPublic ? BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                          : BindingFlags.Instance | BindingFlags.Public);
        }
        internal static PropertyInfo GetProperty(Type type, string name, bool nonPublic)
        {
            return type.GetProperty(name,
                nonPublic ? BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                          : BindingFlags.Instance | BindingFlags.Public);
        }


        internal static object ParseEnum(Type type, string value)
        {
        #if FEAT_IKVM
                    FieldInfo[] fields = type.GetFields();
                    foreach (FieldInfo field in fields)
                    {
                        if (string.Equals(field.Name, value, StringComparison.OrdinalIgnoreCase)) return field.GetRawConstantValue();
                    }
                    throw new ArgumentException("Enum value could not be parsed: " + value + ", " + type.FullName);
        #else
    		        return Enum.Parse(type, value, true);
        #endif
        }


        internal static MemberInfo[] GetInstanceFieldsAndProperties(Type type, bool publicOnly)
        {
            BindingFlags flags = publicOnly ? BindingFlags.Public | BindingFlags.Instance : BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo[] props = type.GetProperties(flags);
            FieldInfo[] fields = type.GetFields(flags);
            MemberInfo[] members = new MemberInfo[fields.Length + props.Length];
            props.CopyTo(members, 0);
            fields.CopyTo(members, props.Length);
            return members;
        }
#if FEAT_IKVM
        internal static System.Reflection.MemberInfo[] GetInstanceFieldsAndProperties(System.Type type, bool publicOnly)
        {
            var flags = publicOnly ? System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance : System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var props = type.GetProperties(flags);
            var fields = type.GetFields(flags);
            var members = new System.Reflection.MemberInfo[fields.Length + props.Length];
            props.CopyTo(members, 0);
            fields.CopyTo(members, props.Length);
            return members;
        }
#endif
        internal static Type GetMemberType(MemberInfo member)
        {
#if WINRT || PORTABLE
            PropertyInfo prop = member as PropertyInfo;
            if (prop != null) return prop.PropertyType;
            FieldInfo fld = member as FieldInfo;
            return fld == null ? null : fld.FieldType;
#else
            switch(member.MemberType)
            {
                case MemberTypes.Field: return ((FieldInfo) member).FieldType;
                case MemberTypes.Property: return ((PropertyInfo) member).PropertyType;
                default: return null;
            }
#endif
        }

        internal static bool IsAssignableFrom(Type target, Type type)
        {
#if WINRT
            return target.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
#else
            return target.IsAssignableFrom(type);
#endif
        }

#if FEAT_IKVM
        internal static bool IsAssignableFrom(System.Type target, System.Type type)
        {
#if WINRT
            return target.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
#else
            return target.IsAssignableFrom(type);
#endif
        }
#endif
        public static MethodInfo GetShadowSetter(TypeModel model, PropertyInfo property)
        {
#if WINRT
            MethodInfo method = Helpers.GetInstanceMethod(property.DeclaringType.GetTypeInfo(), "Set" + property.Name, new Type[] { property.PropertyType });
#else

#if FEAT_IKVM
            Type reflectedType = property.DeclaringType;
#else
            Type reflectedType = property.ReflectedType;
#endif
            MethodInfo method = Helpers.GetInstanceMethod(reflectedType, "Set" + property.Name, new Type[] { property.PropertyType });
#endif
            if (method == null || !method.IsPublic || method.ReturnType != model.MapType(typeof(void))) return null;
            return method;
        }

        public static bool CheckIfPropertyWritable(TypeModel model, PropertyInfo property, bool nonPublic, bool allowInternal)
        {
            return GetShadowSetter(model, property) != null  || (property.CanWrite && Helpers.GetSetMethod(property, nonPublic, allowInternal) != null);
        }

        public static bool CanWrite(TypeModel model, MemberInfo member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));

            PropertyInfo prop = member as PropertyInfo;
            if (prop != null) return CheckIfPropertyWritable(model, prop, true, true);

            return member is FieldInfo; // fields are always writeable; anything else: JUST SAY NO!
        }


        internal static T WrapExceptions<T>(Func<T> action, Func<Exception, string> messageGenerator)
        {
            T r = default(T);
            WrapExceptions(new Action(() => r = action()), messageGenerator);
            return r;
        }

        internal static void WrapExceptions(Action action, Func<Exception, string> messageGenerator)
        {
#if !DEBUG
            try
#endif
            {
                action();
            }
#if DEBUG
            try { }
#endif
            catch (Exception ex)
            {
                string rethrowMsg = messageGenerator(ex);
                if (rethrowMsg == null) throw;

                RethrowSpecific(ex, rethrowMsg);

            }
        }

        internal static void RethrowSpecific(Exception ex, string rethrowMsg)
        {
            if (ex is ProtoException)
                throw new ProtoException(rethrowMsg, ex);
            if (ex is InvalidOperationException)
                throw new InvalidOperationException(rethrowMsg, ex);
            if (ex is NotSupportedException)
                throw new NotSupportedException(rethrowMsg, ex);
            if (ex is NotImplementedException)
                throw new NotImplementedException(rethrowMsg, ex);
            if (ex is ArgumentNullException)
                throw new ArgumentNullException(rethrowMsg, ex);
            if (ex is ArgumentOutOfRangeException)
                throw new ArgumentOutOfRangeException(rethrowMsg, ex);
            if (ex is ArgumentException)
            {
#if SILVERLIGHT || PORTABLE
                throw new ArgumentException(rethrowMsg, ex);
#else
                throw new ArgumentException(rethrowMsg, ((ArgumentException)ex).ParamName, ex);
#endif
            }
            if (ex is System.MissingMemberException)
                throw new System.MissingMemberException(rethrowMsg, ex);
            if (ex is MemberAccessException)
                throw new MemberAccessException(rethrowMsg, ex);


            throw new ProtoException(rethrowMsg, ex);
        }

        internal static string TryGetWrappedExceptionMessage(Exception ex, Type t)
        {
            return ex.Message.IndexOf(t.FullName, System.StringComparison.Ordinal) < 0
                       ? (ex.Message + " (" + t.FullName + ")")
                       : null;
        }

        internal static byte[] GetBuffer(System.IO.MemoryStream ms)
        {
#if NETSTANDARD
            ArraySegment<byte> segment;
            if(!ms.TryGetBuffer(out segment))
            {
                throw new InvalidOperationException("Unable to obtain underlying MemoryStream buffer");
            } else if(segment.Offset != 0)
            {
                throw new InvalidOperationException("Underlying MemoryStream buffer was not zero-offset");
            } else
            {
                return segment.Array;
            }
#elif PORTABLE
            return ms.ToArray();
#else
            return ms.GetBuffer();
#endif
        }
    }

    namespace Internal
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public sealed class HelpersInternal
        {
            [EditorBrowsable(EditorBrowsableState.Never)]
            public static ProtoTypeCode GetTypeCode(System.Type type)
            {
                return Helpers.GetTypeCode(type);
            }

#if FEAT_IKVM
            [EditorBrowsable(EditorBrowsableState.Never)]
            public static ProtoTypeCode GetTypeCode(IKVM.Reflection.Type type)
            {
                return Helpers.GetTypeCode(type);
            }
#endif

            [EditorBrowsable(EditorBrowsableState.Never)]
            public static WireType GetWireType(ProtoTypeCode code, BinaryDataFormat format)
            {
                switch (code)
                {
                    case ProtoTypeCode.Int64:
                    case ProtoTypeCode.UInt64:
                        {
                            return format == BinaryDataFormat.FixedSize ? WireType.Fixed64 : WireType.Variant;

                        }
                    case ProtoTypeCode.Int16:
                    case ProtoTypeCode.Int32:
                    case ProtoTypeCode.UInt16:
                    case ProtoTypeCode.UInt32:
                    case ProtoTypeCode.Boolean:
                    case ProtoTypeCode.SByte:
                    case ProtoTypeCode.Byte:
                    case ProtoTypeCode.Char:
                        {
                            return format == BinaryDataFormat.FixedSize ? WireType.Fixed32 : WireType.Variant;
                        }
                    case ProtoTypeCode.Double:
                        {
                            return WireType.Fixed64;
                        }
                    case ProtoTypeCode.Single:
                        {
                            return WireType.Fixed32;
                        }
                    case ProtoTypeCode.String:
                    case ProtoTypeCode.DateTime:
                    case ProtoTypeCode.Decimal:
                    case ProtoTypeCode.ByteArray:
                    case ProtoTypeCode.TimeSpan:
                    case ProtoTypeCode.Guid:
                    case ProtoTypeCode.Uri:
                    case ProtoTypeCode.Type:
                        {
                            return WireType.String;
                        }
                }
                return WireType.None;
            }

            public static bool IsAssignableFrom(Type target, Type type)
            {
                return Helpers.IsAssignableFrom(target, type);
            }

#if FEAT_IKVM
            public static bool IsAssignableFrom(System.Type target, System.Type type)
            {
                return Helpers.IsAssignableFrom(target, type);
            }
#endif
        }
    }

    /// <summary>
    /// Intended to be a direct map to regular TypeCode, but:
    /// - with missing types
    /// - existing on WinRT
    /// </summary>
    public enum ProtoTypeCode
    {
        Empty = 0,
        Unknown = 1, // maps to TypeCode.Object
        Boolean = 3,
        Char = 4,
        SByte = 5,
        Byte = 6,
        Int16 = 7,
        UInt16 = 8,
        Int32 = 9,
        UInt32 = 10,
        Int64 = 11,
        UInt64 = 12,
        Single = 13,
        Double = 14,
        Decimal = 15,
        DateTime = 16,
        String = 18,

        // additions
        TimeSpan = 100,
        ByteArray = 101,
        Guid = 102,
        Uri = 103,
        Type = 104
    }
}