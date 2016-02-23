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
    public class ProtobufNetPartialMemberHandler : MemberMappingHandlerBase
    {
        readonly IMemberAttributeHandlerStrategy _strategy;

        public ProtobufNetPartialMemberHandler(IMemberAttributeHandlerStrategy strategy)
        {
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            _strategy = strategy;
        }

        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (!s.Input.CanUse(AttributeType.ProtoBuf)) return MemberHandlerResult.NotFound;
            if (HasProtobufNetIgnore(s.Input.Attributes, model)) return MemberHandlerResult.Ignore;
            MemberHandlerResult result = MemberHandlerResult.NotFound;
            foreach (AttributeMap ppma in s.Input.PartialMembers)
            {
                object tmp;
                if (!ppma.TryGet("MemberName", out tmp) || tmp as string != member.Name) continue;

                if (ppma.AttributeType.FullName == "ProtoBuf.ProtoPartialIgnoreAttribute") return MemberHandlerResult.Ignore;

                MemberHandlerResult newResult;
                if (ppma.AttributeType.FullName == "ProtoBuf.ProtoPartialMemberAttribute")
                {
                    newResult = _strategy.TryRead(ppma, s, member, model);
                    // we have ref!
                    main = s.MainValue;
                }
                else newResult = MemberHandlerResult.NotFound;

                if (newResult == MemberHandlerResult.Done) return MemberHandlerResult.Done;
                if (newResult == MemberHandlerResult.Partial) result = newResult;
            }
            return result;
        }
    }
}
#endif