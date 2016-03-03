// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using AltLinq; using System.Linq;
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
    public class AqlaEnumMemberHandler : EnumMemberHandlerBase
    {
        protected override AttributeType RequiredAttributeType => AttributeType.Aqla;

        protected override bool HasIgnore(MemberState s)
        {
            return HasAqlaIgnore(s.Input.Attributes, s.Input.Model);
        }

        protected override AttributeMap GetAttribute(MemberState s)
        {
            AttributeMap attrib = AttributeMap.GetAttribute(s.Input.Attributes, "AqlaSerializer.EnumSerializableValueAttribute");
            return attrib != null && CheckAqlaModelId(attrib, s.Input.Model) ? attrib : null;
        }
    }
}
#endif