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

namespace AqlaSerializer.Meta.Mapping
{
    public class DerivedTypeCandidate
    {
        public int Tag { get; set; }
        public Type Type { get; set; }
        public BinaryDataFormat DataFormat { get; set; }

        public DerivedTypeCandidate(int tag, Type type, BinaryDataFormat dataFormat)
        {
            DataFormat = dataFormat;
            Tag = tag;
            Type = type;
        }
    }
}
#endif