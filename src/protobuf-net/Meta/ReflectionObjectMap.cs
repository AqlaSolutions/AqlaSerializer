using System;
using System.Reflection;

namespace AqlaSerializer.Meta
{
    sealed class ReflectionObjectMap
    {
        public object Target { get; }
        readonly MemberInfo[] _membersPublic;
        readonly MemberInfo[] _membersAll;

        public bool TryGet(string key, bool publicOnly, out object value)
        {
            foreach (MemberInfo member in publicOnly ? _membersPublic : _membersAll)
            {
#if FX11
                if (member.Name.ToUpper() == key.ToUpper())
#else
                if (string.Equals(member.Name, key, StringComparison.OrdinalIgnoreCase))
#endif
                {
                    PropertyInfo prop = member as PropertyInfo;
                    if (prop != null)
                    {
                        value = Helpers.GetPropertyValue(prop, Target);
                        return true;
                    }
                    FieldInfo field = member as FieldInfo;
                    if (field != null)
                    {
                        value = field.GetValue(Target);
                        return true;
                    }

                    throw new NotSupportedException(member.GetType().Name);
                }
            }
            value = null;
            return false;
        }
        
        public ReflectionObjectMap(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Target = target;
            _membersPublic = Helpers.GetInstanceFieldsAndProperties(target.GetType(), true);
            try
            {
                _membersAll = Helpers.GetInstanceFieldsAndProperties(target.GetType(), false);
            }
            catch
            {
                _membersAll = _membersPublic;
            }
        }
    }
}