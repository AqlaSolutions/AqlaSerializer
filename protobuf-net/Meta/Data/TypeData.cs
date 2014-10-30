// Code by Vladyslav Taranov for AqlaSerializer, 2014

using System;
using AqlaSerializer;

#if !NO_RUNTIME && !FEAT_IKVM

namespace AqlaSerializer.Meta.Data
{
    [SerializableType]
    public class TypeData
    {
        public SubtypeData[] Subtypes { get; set; }
        public Type Type { get; set; }
    }
}

#endif