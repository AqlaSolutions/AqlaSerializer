// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

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

namespace AqlaSerializer.Meta.Mapping.MemberHandlers
{
    public class SystemNonSerializableHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryRead(
            MemberState s, ref MemberMainSettingsValue main, ref List<MemberLevelSettingsValue?> levels, MemberInfo member, RuntimeTypeModel model)
        {
            if (!s.Input.CanUse(AttributeType.SystemNonSerialized)) return MemberHandlerResult.NotFound;
            var attrib = AttributeMap.GetAttribute(s.Input.Attributes, "System.NonSerializedAttribute");
            if (attrib != null) return MemberHandlerResult.Ignore;
            return MemberHandlerResult.NotFound;
        }
    }
}
#endif