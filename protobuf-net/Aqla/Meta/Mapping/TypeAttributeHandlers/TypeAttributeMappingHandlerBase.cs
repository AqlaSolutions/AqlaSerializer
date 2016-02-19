namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public abstract class TypeAttributeMappingHandlerBase : MappingHandlerBase, ITypeAttributeHandler
    {
        TypeAttributeHandlerResult ITypeAttributeHandler.TryMap(AttributeMap item, TypeState s)
        {
            return TryMap(item, s, s.Input, s.Model);
        }

        protected abstract TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model);

    }
}