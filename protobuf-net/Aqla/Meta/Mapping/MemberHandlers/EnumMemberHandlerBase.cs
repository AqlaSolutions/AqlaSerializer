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
    public abstract class EnumMemberHandlerBase : MemberMappingHandlerBase
    {
        protected abstract AttributeType RequiredAttributeType { get; }

        protected abstract bool HasIgnore(MemberState s);

        protected abstract AttributeMap GetAttribute(MemberState s);

        protected override MemberHandlerResult TryRead(
            MemberState s, ref MemberMainSettingsValue main, ref List<MemberLevelSettingsValue?> levels, MemberInfo member, RuntimeTypeModel model)
        {
            // always consider SerializableMember if not strict ProtoBuf
            if (!s.Input.AsEnum) return MemberHandlerResult.NotFound;
            if (!s.Input.CanUse(RequiredAttributeType)) return MemberHandlerResult.NotFound;
            if (HasIgnore(s)) return MemberHandlerResult.Ignore;

            AttributeMap attrib = GetAttribute(s);
            if (attrib == null) return MemberHandlerResult.NotFound;

            if (string.IsNullOrEmpty(main.Name)) attrib.TryGetNotDefault("Name", ref main.Name);

            object tmp;
            main.Tag = attrib.TryGet("Value", out tmp) ? (int)tmp : Helpers.GetEnumMemberUnderlyingValue(member);

            s.TagIsPinned = main.Tag > 0;

            return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial;
        }
    }
}
#endif