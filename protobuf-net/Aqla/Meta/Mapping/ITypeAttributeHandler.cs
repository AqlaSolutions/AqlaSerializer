namespace AqlaSerializer.Meta.Mapping
{
    public interface ITypeAttributeHandler
    {
        TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s);
    }
}