#if !NO_RUNTIME && FEAT_COMPILER && !FEAT_IKVM
#define ENABLED
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AltLinq;
#if ENABLED
using DynamicCompilationSpike;
#endif

namespace AqlaSerializer.Internal
{
    public class AccessorsCache
    {
        public delegate object GetHandler(object source);

        public delegate void SetHandler(object source, object value);

        public delegate object InstantiateObjectHandler();

        internal struct Accessors
        {
            public SetHandler Set { get; set; }
            public GetHandler Get { get; set; }

            public Accessors(GetHandler get, SetHandler set)
            {
                Get = get;
                Set = set;
            }
        }
#if ENABLED
        static readonly Dictionary<PropertyInfo, Accessors> Properties = new Dictionary<PropertyInfo, Accessors>();

        internal static Accessors GetAccessors(PropertyInfo member)
        {
            if (AccessorsCacheCheck.PropertiesDisabled)
                return new Accessors(inst => Helpers.GetPropertyValue(member, inst), (inst, v) => member.SetValue(inst, v, null));
            Accessors accessors;
            lock (Properties)
            {
                if (Properties.TryGetValue(member, out accessors))
                    return accessors;
            }

            GetHandler getter = DynamicMethodCompiler.CreateGetHandler(member.ReflectedType, member) ?? (inst => Helpers.GetPropertyValue(member, inst));
            SetHandler setter = DynamicMethodCompiler.CreateSetHandler(member.ReflectedType, member) ?? ((inst, v) => member.SetValue(inst, v, null));

            accessors = new Accessors(getter, setter);

            lock (Properties)
                Properties[member] = accessors;

            return accessors;
        }

        static readonly Dictionary<MethodInfo, SetHandler> ShadowSetters = new Dictionary<MethodInfo, SetHandler>();

        internal static SetHandler GetShadowSetter(MethodInfo method)
        {
            if (AccessorsCacheCheck.PropertiesDisabled)
                return (inst, v) => method.Invoke(inst, new[] { v });

            SetHandler setter;
            lock (ShadowSetters)
            {
                if (ShadowSetters.TryGetValue(method, out setter))
                    return setter;
            }
            
            
            setter = DynamicMethodCompiler.CreateSetHandler(method.ReflectedType, method) ?? ((inst, v) => method.Invoke(inst, new[] { v }));

            lock (ShadowSetters)
                ShadowSetters[method] = setter;

            return setter;
        }

        static readonly Dictionary<MethodInfo, GetHandler> ShadowGetters = new Dictionary<MethodInfo, GetHandler>();

        internal static GetHandler GetShadowGetter(MethodInfo method)
        {
            if (AccessorsCacheCheck.PropertiesDisabled)
                return (inst) => method.Invoke(inst, null);

            GetHandler getter;
            lock (ShadowGetters)
            {
                if (ShadowGetters.TryGetValue(method, out getter))
                    return getter;
            }
            
            
            getter = DynamicMethodCompiler.CreateGetHandler(method.ReflectedType, method) ?? ((inst) => method.Invoke(inst, null));

            lock (ShadowGetters)
                ShadowGetters[method] = getter;

            return getter;
        }

        static readonly Dictionary<FieldInfo, Accessors> Fields = new Dictionary<FieldInfo, Accessors>();

        internal static Accessors GetAccessors(FieldInfo member)
        {
            if (AccessorsCacheCheck.PropertiesDisabled)
                return new Accessors(member.GetValue, member.SetValue);
            Accessors accessors;
            lock (Fields)
            {
                if (Fields.TryGetValue(member, out accessors))
                    return accessors;
            }

            GetHandler getter = DynamicMethodCompiler.CreateGetHandler(member.ReflectedType, member) ?? member.GetValue;
            SetHandler setter = DynamicMethodCompiler.CreateSetHandler(member.ReflectedType, member) ?? member.SetValue;

            accessors = new Accessors(getter, setter);

            lock (Fields)
                Fields[member] = accessors;

            return accessors;
        }
#endif
    }
#if ENABLED
    class AccessorsCacheCheck
    {
        class TestClass
        {
#pragma warning disable 649
            int x;
#pragma warning restore 649

            public int GetX()
            {
                return this.x;
            }

            int Y { get; set; }

            public int GetY()
            {
                return this.Y;
            }
        }

        public static bool FieldsDisabled { get; }
        public static bool PropertiesDisabled { get; }

        static AccessorsCacheCheck()
        {
            var test = new TestClass();
            try
            {
                DynamicMethodCompiler.CreateSetHandler(
                    typeof(AccessorsCache),
                    typeof(TestClass).GetField("x", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))(test, 55);
            }
            catch
            {
            }
            if (test.GetX() != 55) FieldsDisabled = true;

            try
            {
                DynamicMethodCompiler.CreateSetHandler(
                    typeof(AccessorsCache),
                    typeof(TestClass).GetProperty("Y", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))(test, 55);
            }
            catch
            {
            }
            if (test.GetY() != 55) PropertiesDisabled = true;
        }
    }
#endif
}
