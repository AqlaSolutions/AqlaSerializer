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
            if (!s.AsEnum && a.CanUse(AttributeType.Aqla))
                return _derivedTypeStrategy.TryMap(item, s);
            return TypeAttributeHandlerResult.Continue;
        }
    }
}