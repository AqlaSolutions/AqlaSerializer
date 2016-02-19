namespace AqlaSerializer.Meta.Mapping
{
    public abstract class MappingHandlerBase
    {
        protected virtual bool CheckAqlaModelId(AttributeMap attrib, RuntimeTypeModel model)
        {
            if (attrib == null) return false;
            object actual;
            return attrib.TryGet(nameof(NonSerializableMemberAttribute.ModelId), out actual) && CheckAqlaModelId(actual, model);
        }

        protected virtual bool CheckAqlaModelId(SerializableMemberAttribute attr, RuntimeTypeModel model)
        {
            return CheckAqlaModelId(attr.ModelId, model);
        }

        protected virtual bool CheckAqlaModelId(object actualId, RuntimeTypeModel model)
        {
            return object.Equals(model.ModelId, actualId);
        }
    }
}