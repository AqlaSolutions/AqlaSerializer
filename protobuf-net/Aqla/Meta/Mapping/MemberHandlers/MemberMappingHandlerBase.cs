// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
using System.Collections.Generic;
using System.Diagnostics;
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
    public abstract class MemberMappingHandlerBase : MappingHandlerBase, IMemberHandler
    {
        [DebuggerStepThrough]
        public MemberHandlerResult TryMap(MemberState state)
        {
            var main = state.MainValue;
            var levels = state.LevelValues;
            var model = state.Input.Model;
            try
            {
                return TryMap(state, ref main, ref levels, state.Input.Member, model);
            }
            finally
            {
                state.MainValue = main;
                state.LevelValues = levels;
            }
        }

        protected abstract MemberHandlerResult TryMap(
            MemberState s, ref MemberMainSettingsValue main, ref List<MemberLevelSettingsValue?> levels, MemberInfo member, RuntimeTypeModel model);


        protected virtual bool HasAqlaIgnore(AttributeMap[] map, RuntimeTypeModel model)
        {
            return AttributeMap.GetAttributes(map, "AqlaSerializer.NonSerializableMemberAttribute").Any(a => CheckAqlaModelId(a, model));
        }

        protected virtual bool HasProtobufNetIgnore(AttributeMap[] map, RuntimeTypeModel model)
        {
            return AttributeMap.GetAttribute(map, "ProtoBuf.ProtoIgnoreAttribute") != null;
        }
    }
}
#endif