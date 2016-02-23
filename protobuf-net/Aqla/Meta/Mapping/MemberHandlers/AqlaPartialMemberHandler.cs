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
    public class AqlaPartialMemberHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (!s.Input.CanUse(AttributeType.Aqla)) return MemberHandlerResult.NotFound;
            if (HasAqlaIgnore(s.Input.Attributes, model)) return MemberHandlerResult.Ignore;
            MemberHandlerResult result = MemberHandlerResult.NotFound;
            foreach (AttributeMap ppma in s.Input.PartialMembers)
            {
                object tmp;
                if (!ppma.TryGet("MemberName", out tmp) || tmp as string != member.Name) continue;

                if (ppma.AttributeType.FullName == "AqlaSerializer.PartialNonSerializableMemberAttribute" && CheckAqlaModelId(ppma, model)) return MemberHandlerResult.Ignore;

                MemberHandlerResult newResult;
                if (Helpers.IsAssignableFrom(model.MapType(typeof(SerializablePartialMemberAttribute)), ppma.AttributeType))
                {
                    var attr = ppma.GetRuntimeAttribute<SerializablePartialMemberAttribute>(model);
                    main = attr.MemberSettings;
                    s.SerializationSettings.DefaultValue = attr.DefaultValue;
                    s.SerializationSettings.SetSettings(attr.LevelSettings, 0);

                    s.TagIsPinned = main.Tag > 0;
                    newResult = s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial;
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