// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq; using System.Linq;
using AqlaSerializer.Internal;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    internal interface IValueSerializerBuilder
    {
        IProtoSerializerWithWireType BuildValueFinalSerializer(ValueSerializationSettings settings, bool isMemberOrNested, out WireType wireType);

        IProtoSerializerWithWireType TryGetSimpleCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType);
    }
}
#endif