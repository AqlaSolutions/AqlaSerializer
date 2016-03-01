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

        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            // always consider SerializableMember if not strict ProtoBuf
            if (!s.Input.IsEnumValueMember) return MemberHandlerResult.NotFound;
            if (main.Tag <= 0)
            {
                try
                {
                    main.Tag = Helpers.GetEnumMemberUnderlyingValue(member);
                }
                catch (OverflowException)
                {
                    // should use EnumPassthrough for this value
                    return MemberHandlerResult.NotFound;
                }
            }
            if (!s.Input.CanUse(RequiredAttributeType)) return MemberHandlerResult.Partial;
            if (HasIgnore(s)) return MemberHandlerResult.Ignore;

            AttributeMap attrib = GetAttribute(s);
            if (attrib == null) return MemberHandlerResult.Partial;

            if (string.IsNullOrEmpty(main.Name)) attrib.TryGetNotEmpty("Name", ref main.Name);

#if !FEAT_IKVM // IKVM can't access HasValue, but conveniently, Value will only be returned if set via ctor or property
            if ((bool)Helpers.GetInstanceMethod(
                attrib.AttributeType
#if WINRT
                             .GetTypeInfo()
#endif
                ,
                "HasValue").Invoke(attrib.Target, null))
#endif
            {

                object tmp;
                if (attrib.TryGet("Value", out tmp)) main.Tag = (int)tmp;
            }

            s.TagIsPinned = main.Tag > 0;

            return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial;
        }
    }
}
#endif