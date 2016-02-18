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
            Handlers = handlersCollection.ToList();
        }

        public virtual NormalizedMappedMember Map(ref MemberArgsValue args)
        {
            if (args.Member == null || (args.Family == MetaType.AttributeFamily.None && !args.AsEnum)) return null;

            if (args.Member.Name == "Wrapper")
            {

            }
            var model = args.Model;
            var member = args.Member;
            
            if (args.AsEnum) args.IsForced = true;

            var state = new MemberState(args);
            MemberMainSettingsValue m = state.MainValue;
            m.Tag = int.MinValue;
            state.MainValue = m;

            state.LevelValues.Add(new MemberLevelSettingsValue());

            MemberHandlerResult result = MemberHandlerResult.NotFound;

            foreach (var handler in Handlers)
            {
                switch (result = handler.TryRead(state))
                {
                    case MemberHandlerResult.Ignore:
                        return null;
                    case MemberHandlerResult.Done:
                        break;
                }
            }

            if (result == MemberHandlerResult.Ignore || (state.MainValue.Tag < state.MinAcceptFieldNumber && !state.Input.IsForced)) return null;

            bool readOnly = !Helpers.CanWrite(model, member);

            if (readOnly)
            {
                for (int i = 0; i < state.LevelValues.Count; i++)
                {
                    var s = state.LevelValues[i].GetValueOrDefault();
                    if (s.Collection.Append != null)
                    {
                        if (!s.Collection.Append.Value)
                        {
                            if (!state.Input.IgnoreNonWritableForOverwriteCollection)
                                throw new ProtoException("The property " + member.Name + " of " + member.DeclaringType.Name + " is not writable but AppendCollection is true!");

                            s.Collection.Append = true;
                        }
                    }
                    else s.Collection.Append = true;

                    state.LevelValues[i] = s;
                }
            }

            if (Helpers.IsNullOrEmpty(state.MainValue.Name))
            {
                m = state.MainValue;
                m.Name = member.Name;
                state.MainValue = m;
            }

            return new NormalizedMappedMember(state)
            {
                ForcedTag = state.Input.IsForced || state.Input.InferTagByName,
                IsReadOnly = readOnly,
            };
        }
    }
}

#endif