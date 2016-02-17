// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Meta.Mapping;
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

namespace AqlaSerializer
{
    public class NormalizedProtoMember
    {
        public bool IsReadOnly { get; set; }
        public bool ForcedTag { get; set; }
        public MemberState MappingState { get; set; }
    }
}
#endif