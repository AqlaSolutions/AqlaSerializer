#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
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

using System;

namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public class SerializeDerivedTypeHandler : TypeAttributeMappingHandlerBase
    {
        readonly ITypeAttributeHandler _derivedTypeStrategy;

        public SerializeDerivedTypeHandler(ITypeAttributeHandler derivedTypeStrategy)
        {
            if (derivedTypeStrategy == null) throw new ArgumentNullException(nameof(derivedTypeStrategy));
            _derivedTypeStrategy = derivedTypeStrategy;
        }

        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            if (a.CanUse(AttributeType.Aqla))
                return _derivedTypeStrategy.TryMap(item, s);
            return TypeAttributeHandlerResult.Continue;
        }
    }
}
#endif