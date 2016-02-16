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
        [Obsolete("Please use AttributeType instead")]
        new public Type GetType() { return AttributeType; }
#endif
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

        internal static ReflectionObjectMap[] CreateRuntime(TypeModel model, MemberInfo member, System.Type attributeType, bool inherit)
        {
#if FEAT_IKVM
            return CreateRuntime(model, member, model.MapType(attributeType), inherit);
#else
            return member.GetCustomAttributes(attributeType, inherit).Select(attr => new ReflectionObjectMap(attr)).ToArray();
#endif
        }

#if FEAT_IKVM
        internal static ReflectionObjectMap[] CreateRuntime(TypeModel model, MemberInfo member, Type attributeType, bool inherit)
        {
            return member.__GetCustomAttributes(attributeType, inherit).Select(attr => new ReflectionObjectMap(IKVMAttributeFactory.Create(attr))).ToArray();
        }
#endif
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

#if FEAT_IKVM
        private sealed class AttributeDataMap : AttributeMap
        {
            public override Type AttributeType
            {
                get { return attribute.Constructor.DeclaringType; }
            }
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
        }
#else
        public abstract object Target { get; }

        public sealed class ReflectionAttributeMap : AttributeMap
        {
            readonly ReflectionObjectMap _impl;
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