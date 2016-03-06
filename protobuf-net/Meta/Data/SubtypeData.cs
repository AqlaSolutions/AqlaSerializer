// Code by Vladyslav Taranov for AqlaSerializer, 2016

using System;
using AqlaSerializer;

#if !NO_RUNTIME && !FEAT_IKVM

namespace AqlaSerializer.Meta.Data
{
    [SerializableType]
    public class SubtypeData
    {
        public int FieldNumber { get; set; }
        public Type Type { get; set; }
    }
}

#endif