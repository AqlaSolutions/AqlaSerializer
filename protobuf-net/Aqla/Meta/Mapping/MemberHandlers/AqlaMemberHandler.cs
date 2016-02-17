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
    public class AqlaMemberHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryRead(
            MemberState s, ref MemberMainSettingsValue main, ref List<MemberLevelSettingsValue?> levels, MemberInfo member, RuntimeTypeModel model)
        {
            // always consider SerializableMember if not strict ProtoBuf
            if (!s.Input.CanUse(AttributeType.Aqla)) return MemberHandlerResult.NotFound;
            if (HasAqlaIgnore(s.Input.Attributes, model)) return MemberHandlerResult.Ignore;
            var memberRtAttrs = AttributeMap.CreateRuntime<SerializableMemberAttribute>(model, member, true).FirstOrDefault(attr => CheckAqlaModelId(attr, model));
            if (memberRtAttrs == null) return MemberHandlerResult.NotFound;

            SerializableMemberNestedAttribute[] nested = AttributeMap
                .CreateRuntime<SerializableMemberNestedAttribute>(model, member, true)
                .Where(a => a.ModelId == model.ModelId)
                .ToArray();

            main = memberRtAttrs.MemberSettings;
            levels[0] = memberRtAttrs.LevelSettings;

            foreach (var lvl in nested)
            {
                while (lvl.Level >= levels.Count)
                    levels.Add(null);
                if (levels[lvl.Level] != null) throw new InvalidOperationException("Level " + lvl.Level + " settings for member " + member + " has been already initialized");
            }

            s.TagIsPinned = memberRtAttrs.Tag > 0;
            return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial; // TODO minAcceptFieldNumber only applies to non-proto?
        }
    }
}
#endif