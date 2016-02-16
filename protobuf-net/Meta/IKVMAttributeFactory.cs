#if !NO_RUNTIME && FEAT_IKVM
using System;
using System.Reflection;
using AltLinq;

namespace AqlaSerializer.Meta
{
    static class IKVMAttributeFactory
    {
        public static object Create(IKVM.Reflection.CustomAttributeData attribute)
        {
            var systemType = System.Type.GetType(attribute.Constructor.DeclaringType.FullName, true);
            var obj = Activator.CreateInstance(systemType, attribute.ConstructorArguments.Select(a => a.Value).ToArray());

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
                    Helpers.GetSetMethod(prop, true, true).Invoke(obj, new[] { arg.TypedValue.Value });

                FieldInfo field = member as FieldInfo;
                field?.SetValue(obj, new[] { arg.TypedValue.Value });
            }

            return obj;
        }
    }
}

#endif