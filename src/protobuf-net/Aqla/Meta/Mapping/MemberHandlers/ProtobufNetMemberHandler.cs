// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
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
    public class ProtobufNetMemberHandler : MemberMappingHandlerBase
    {
        readonly IMemberAttributeHandlerStrategy _strategy;

        public ProtobufNetMemberHandler(IMemberAttributeHandlerStrategy strategy)
        {
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            _strategy = strategy;
        }

        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (s.Input.IsEnumValueMember) return MemberHandlerResult.NotFound;
            // always consider ProtoMember if not strict Aqla
            // even if no [ProtoContract] was declared!
            if (!s.Input.CanUse(AttributeType.ProtoBuf)) return MemberHandlerResult.NotFound;
            if (HasProtobufNetIgnore(s.Input.Attributes, model)) return MemberHandlerResult.Ignore;
            var attrib = AttributeMap.GetAttribute(s.Input.Attributes, "ProtoBuf.ProtoMemberAttribute");

            if (attrib != null)
            {
                var r = _strategy.TryRead(attrib, s, member, model);
                // we have ref!
                main = s.MainValue;
                return r;
            }
            return MemberHandlerResult.NotFound;
        }
    }
}
#endif