﻿// Code by Vladyslav Taranov for AqlaSerializer, 2014

using System;
using AqlaSerializer;

#if !NO_RUNTIME && !FEAT_IKVM

namespace ProtoBuf.Meta.Data
{
    [SerializableType]
    public class SubtypeData
    {
        public DataFormat DataFormat { get; set; }
        public int FieldNumber { get; set; }
        public Type Type { get; set; }
    }
}

#endif