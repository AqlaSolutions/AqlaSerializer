﻿// Code by Vladyslav Taranov for AqlaSerializer, 2014

#if !NO_RUNTIME && !FEAT_IKVM

using AqlaSerializer;

namespace ProtoBuf.Meta.Data
{
    [SerializableType]
    public class ModelTypeRelationsData
    {
        public TypeData[] Types { get; set; }
    }
}
#endif