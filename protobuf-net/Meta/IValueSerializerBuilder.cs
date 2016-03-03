// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq;
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

        IProtoSerializerWithWireType TryGetCoreSerializer(
            BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
            ref ValueFormat format, bool dynamicType, bool appendCollection, bool isPackedCollection, bool allowComplexTypes, ref object defaultValue);

        bool CanBePackedCollection(MemberLevelSettingsValue level);
    }

    static class ValueSerializerBuilderExtensions
    {
        public static IProtoSerializerWithWireType TryGetSimpleCoreSerializer(this IValueSerializerBuilder builder, BinaryDataFormat dataFormat, Type type, out WireType defaultWireType)
        {
            object dummy = null;
            ValueFormat format = ValueFormat.Compact;
            return builder.TryGetCoreSerializer(dataFormat, type, out defaultWireType, ref format, false, false, false, false, ref dummy);
        }

    }
}
#endif