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

namespace AqlaSerializer.Meta.Mapping
{
    public class MemberMapper : IMemberMapper
    {
        protected List<IMemberHandler> Handlers { get; }

        public MemberMapper(IEnumerable<IMemberHandler> handlersCollection)
        {
            if (handlersCollection == null) throw new ArgumentNullException(nameof(handlersCollection));
            Handlers = handlersCollection.ToList();
        }

        public virtual MappedMember Map(MemberArgsValue args)
        {
            if (args.Member == null || (args.Family == MetaType.AttributeFamily.None && !args.AsEnum)) return null;
            if (args.AsEnum) args.IsForced = true;
            var state = new MemberState(args);
            MemberMainSettingsValue m = state.MainValue;
            m.Tag = int.MinValue;
            state.MainValue = m;
            
            if (ProcessHandlers(state) == MemberHandlerResult.Ignore || (state.MainValue.Tag < state.MinAcceptFieldNumber && !state.Input.IsForced)) return null;

            if (state.SerializationSettings.DefaultLevel == null)
                state.SerializationSettings.DefaultLevel = state.SerializationSettings.GetSettingsCopy(0);

            return new MappedMember(state)
            {
                ForcedTag = state.Input.IsForced || state.Input.InferTagByName
            };
        }

        protected virtual MemberHandlerResult ProcessHandlers(MemberState state)
        {
            MemberHandlerResult result=MemberHandlerResult.NotFound;
            foreach (var handler in Handlers)
            {
                switch (result = handler.TryMap(state))
                {
                    case MemberHandlerResult.Ignore:
                        return result;
                    case MemberHandlerResult.Done:
                        break;
                }
            }
            return result;
        }
    }
}

#endif