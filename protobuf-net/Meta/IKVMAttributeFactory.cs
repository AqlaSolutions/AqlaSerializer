#if !NO_RUNTIME && FEAT_IKVM
using System;
using System.Reflection;
using AltLinq; using System.Linq;
using Type = IKVM.Reflection.Type;

namespace AqlaSerializer.Meta
{
    static class IKVMAttributeFactory
    {
        public static object Create(IKVM.Reflection.CustomAttributeData attribute)
        {
            var systemType = System.Type.GetType(attribute.Constructor.DeclaringType.FullName, true);
            var obj = Activator.CreateInstance(systemType, attribute.ConstructorArguments.Select(a => Convert(a.Value, a.ArgumentType)).ToArray());

            var members = Helpers.GetInstanceFieldsAndProperties(systemType, false).ToDictionary(m => m.Name, StringComparer.Ordinal);
            var membersIgnoreCase = Helpers.GetInstanceFieldsAndProperties(systemType, false).ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var arg in attribute.NamedArguments)
            {
                MemberInfo member;
                if (!members.TryGetValue(arg.MemberInfo.Name, out member))
                    if (!membersIgnoreCase.TryGetValue(arg.MemberInfo.Name, out member))
                    {
                        continue;
                    }

                PropertyInfo prop = member as PropertyInfo;
                if (prop != null)
                    Helpers.GetSetMethod(prop, true, true).Invoke(obj, new[] { Convert(arg.TypedValue.Value, arg.TypedValue.ArgumentType) });

                FieldInfo field = member as FieldInfo;
                field?.SetValue(obj, new[] { arg.TypedValue.Value });
            }

            return obj;
        }

        static object Convert(object value, Type expectedType)
        {
            if (value == null) return null;
            if (expectedType.IsEnum && !value.GetType().IsEnum)
            {
                var t = System.Type.GetType(expectedType.FullName) ?? typeof(SerializableMemberAttributeBase).Assembly.GetType(expectedType.FullName, true);
                return Enum.ToObject(t, value);
            }
            return value;
        }
    }
}

#endif