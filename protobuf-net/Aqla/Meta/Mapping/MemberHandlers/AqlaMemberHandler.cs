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
    public class AqlaMemberHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (s.Input.IsEnumValueMember) return MemberHandlerResult.NotFound;
            // always consider SerializableMember if not strict ProtoBuf
            // even if no [SerializableType] was declared!
            if (!s.Input.CanUse(AttributeType.Aqla)) return MemberHandlerResult.NotFound;
            if (HasAqlaIgnore(s.Input.Attributes, model)) return MemberHandlerResult.Ignore;
            var memberRtAttr = AttributeMap.CreateRuntime<SerializableMemberAttribute>(model, member, true).FirstOrDefault(attr => CheckAqlaModelId(attr, model));
            if (memberRtAttr == null) return MemberHandlerResult.NotFound;

            SerializableMemberNestedAttribute[] nested = AttributeMap
                .CreateRuntime<SerializableMemberNestedAttribute>(model, member, true)
                .Where(a => a.ModelId == model.ModelId)
                .ToArray();

            main = memberRtAttr.MemberSettings;

            s.SerializationSettings.SetSettings(memberRtAttr.LevelSettings, 0);

            s.SerializationSettings.DefaultValue = memberRtAttr.DefaultValue;

            foreach (var lvl in nested)
            {
                if (s.SerializationSettings.HasSettingsSpecified(lvl.Level)) throw new InvalidOperationException("Level " + lvl.Level + " settings for member " + member + " has been already initialized");
                s.SerializationSettings.SetSettings(lvl.LevelSettings, lvl.Level);
            }

            s.TagIsPinned = memberRtAttr.Tag > 0;
            return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial;
        }
    }
}
#endif