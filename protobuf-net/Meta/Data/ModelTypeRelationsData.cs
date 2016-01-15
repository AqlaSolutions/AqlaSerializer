// Code by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME && !FEAT_IKVM

using AqlaSerializer;

namespace AqlaSerializer.Meta.Data
{
    [SerializableType]
    public class ModelTypeRelationsData
    {
        public TypeData[] Types { get; set; }
    }
}
#endif