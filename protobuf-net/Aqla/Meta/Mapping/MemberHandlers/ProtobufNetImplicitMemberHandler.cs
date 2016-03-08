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
    public class ProtobufNetImplicitMemberHandler : MemberMappingHandlerBase
    {
        readonly IMemberAttributeHandlerStrategy _strategy;

        public ProtobufNetImplicitMemberHandler(IMemberAttributeHandlerStrategy strategy)
        {
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            _strategy = strategy;
        }

        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (s.Input.IsEnumValueMember
                || s.MainValue.Tag > 0
                || s.TagIsPinned
                || s.SerializationSettings.MaxSpecifiedNestedLevel != -1
                || s.Input.Family != MetaType.AttributeFamily.ProtoBuf 
                || !s.Input.IsForced)
            {
                return MemberHandlerResult.NotFound;
            }

            var l = s.SerializationSettings.GetSettingsCopy(0);
            l.Basic.UseLegacyDefaults = true;
            _strategy.SetLegacyFormat(ref l.Basic, member, model);
            s.SerializationSettings.SetSettings(l, 0);

            return MemberHandlerResult.Partial;
        }
    }
}
#endif