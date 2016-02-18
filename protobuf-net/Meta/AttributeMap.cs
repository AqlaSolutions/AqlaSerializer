// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AltLinq;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    public abstract class AttributeMap
    {
#if DEBUG
        [Obsolete("Please use AttributeType instead", true)]
        new public Type GetType() { return AttributeType; }
#endif

        public bool TryGetNotDefault<T>(string memberName, ref T value, T notSpecifiedValue = default(T), bool publicOnly = true)
        {
            object obj;
            if (!this.TryGet(memberName, publicOnly, out obj) || obj == null) return false;
            var newValue = (T)obj;
            if (Equals(newValue, notSpecifiedValue)) return false;
            value = newValue;
            return true;
        }
        
        public bool TryGetNotEmpty(string memberName, ref string value, bool publicOnly = true)
        {
            object obj;
            if (!this.TryGet(memberName, publicOnly, out obj) || obj == null) return false;
            var newValue = (string)obj;
            if (string.IsNullOrEmpty(newValue)) return false;
            value = newValue;
            return true;
        }
        
        public abstract bool TryGet(string key, bool publicOnly, out object value);
        public bool TryGet(string key, out object value)
        {
            return TryGet(key, true, out value);
        }
        public abstract Type AttributeType { get; }
        public static AttributeMap[] Create(TypeModel model, Type type, bool inherit)
        {
#if FEAT_IKVM
            Type attribType = model.MapType(typeof(System.Attribute));
            System.Collections.Generic.IList<CustomAttributeData> all = type.__GetCustomAttributes(attribType, inherit);
            AttributeMap[] result = new AttributeMap[all.Count];
            int index = 0;
            foreach (CustomAttributeData attrib in all)
            {
                result[index++] = new AttributeDataMap(attrib);
            }
            return result;
#else
#if WINRT
            Attribute[] all = System.Linq.Enumerable.ToArray(type.GetTypeInfo().GetCustomAttributes(inherit));
#else
            object[] all = type.GetCustomAttributes(inherit);
#endif
            AttributeMap[] result = new AttributeMap[all.Length];
            for(int i = 0 ; i < all.Length ; i++)
            {
                result[i] = new ReflectionAttributeMap((Attribute)all[i]);
            }
            return result;
#endif
        }

        public static AttributeMap GetAttribute(AttributeMap[] attribs, string fullName)
        {
            for (int i = 0; i < attribs.Length; i++)
            {
                AttributeMap attrib = attribs[i];
                if (attrib != null && attrib.AttributeType.FullName == fullName) return attrib;
            }
            return null;
        }

        public static AttributeMap[] GetAttributes(AttributeMap[] attribs, string fullName)
        {
            return attribs.Where(a => a != null && a.AttributeType.FullName == fullName).ToArray();
        }

        internal static T[] CreateRuntime<T>(TypeModel model, MemberInfo member, bool inherit)
        {
#if FEAT_IKVM
            return member.__GetCustomAttributes(model.MapType(typeof(T)), inherit).Select(attr => (T)IKVMAttributeFactory.Create(attr)).ToArray();
#else
            return member.GetCustomAttributes(typeof(T), inherit).Select(attr => attr).Select(a => (T)a).ToArray();
#endif
        }
        
        public static AttributeMap[] Create(TypeModel model, MemberInfo member, bool inherit)
        {
#if FEAT_IKVM
            System.Collections.Generic.IList<CustomAttributeData> all = member.__GetCustomAttributes(model.MapType(typeof(Attribute)), inherit);
            AttributeMap[] result = new AttributeMap[all.Count];
            int index = 0;
            foreach (CustomAttributeData attrib in all)
            {
                result[index++] = new AttributeDataMap(attrib);
            }
            return result;
#else
#if WINRT
            Attribute[] all = System.Linq.Enumerable.ToArray(member.GetCustomAttributes(inherit));
#else
            object[] all = member.GetCustomAttributes(inherit);
#endif
            AttributeMap[] result = new AttributeMap[all.Length];
            for(int i = 0 ; i < all.Length ; i++)
            {
                result[i] = new ReflectionAttributeMap((Attribute)all[i]);
            }
            return result;
#endif
        }
        public static AttributeMap[] Create(TypeModel model, Assembly assembly)
        {
            
#if FEAT_IKVM
            const bool inherit = false;
            System.Collections.Generic.IList<CustomAttributeData> all = assembly.__GetCustomAttributes(model.MapType(typeof(Attribute)), inherit);
            AttributeMap[] result = new AttributeMap[all.Count];
            int index = 0;
            foreach (CustomAttributeData attrib in all)
            {
                result[index++] = new AttributeDataMap(attrib);
            }
            return result;
#else
#if WINRT
            Attribute[] all = System.Linq.Enumerable.ToArray(assembly.GetCustomAttributes());
#else
            const bool inherit = false;
            object[] all = assembly.GetCustomAttributes(inherit);
#endif
            AttributeMap[] result = new AttributeMap[all.Length];
            for(int i = 0 ; i < all.Length ; i++)
            {
                result[i] = new ReflectionAttributeMap((Attribute)all[i]);
            }
            return result;
#endif
        }

        public abstract T GetRuntimeAttribute<T>(TypeModel model);

#if FEAT_IKVM
        private sealed class AttributeDataMap : AttributeMap
        {
            public override Type AttributeType { get { return attribute.Constructor.DeclaringType; } }
            private readonly CustomAttributeData attribute;

            public AttributeDataMap(CustomAttributeData attribute)
            {
                this.attribute = attribute;
            }

            public override bool TryGet(string key, bool publicOnly, out object value)
            {
                foreach (CustomAttributeNamedArgument arg in attribute.NamedArguments)
                {
                    if (string.Equals(arg.MemberInfo.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = arg.TypedValue.Value;
                        return true;
                    }
                }


                int index = 0;
                ParameterInfo[] parameters = attribute.Constructor.GetParameters();
                foreach (CustomAttributeTypedArgument arg in attribute.ConstructorArguments)
                {
                    if (string.Equals(parameters[index++].Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = arg.Value;
                        return true;
                    }
                }
                value = null;
                return false;
            }

            volatile object _runtime;

            public override T GetRuntimeAttribute<T>(TypeModel model)
            {
                return (T)(_runtime ?? (_runtime = IKVMAttributeFactory.Create(attribute)));

            }
        }
#else
        public abstract object Target { get; }

        public sealed class ReflectionAttributeMap : AttributeMap
        {
            readonly ReflectionObjectMap _impl;
            public override T GetRuntimeAttribute<T>(TypeModel model)
            {
                return (T)Target;
            }

            public override object Target => _impl.Target;

            public override Type AttributeType => Target.GetType();

            public override bool TryGet(string key, bool publicOnly, out object value)
            {
                return _impl.TryGet(key, publicOnly, out value);
            }

            public ReflectionAttributeMap(Attribute attribute)
            {
                _impl = new ReflectionObjectMap(attribute);
            }
        }
#endif
    }
}
#endif