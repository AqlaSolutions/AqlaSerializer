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
    public class DataContractMemberHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryMap(
            MemberState s, ref MemberMainSettingsValue main, ref List<MemberLevelSettingsValue?> levels, MemberInfo member, RuntimeTypeModel model)
        {
            if (!s.Input.CanUse(AttributeType.DataContract)) return MemberHandlerResult.NotFound;

            if (!s.Input.HasFamily(MetaType.AttributeFamily.DataContractSerialier)) return MemberHandlerResult.NotFound;
            var attrib = AttributeMap.GetAttribute(s.Input.Attributes, "System.Runtime.Serialization.DataMemberAttribute");
            if (attrib == null) return MemberHandlerResult.NotFound;

            if (main.Tag <= 0) attrib.TryGetNotDefault("Order", ref main.Tag);
            if (string.IsNullOrEmpty(main.Name)) attrib.TryGetNotEmpty("Name", ref main.Name);
            if (!main.IsRequiredInSchema) attrib.TryGetNotDefault("IsRequired", ref main.IsRequiredInSchema);
            bool done = main.Tag >= s.MinAcceptFieldNumber;
            if (done)
            {
                main.Tag += s.Input.DataMemberOffset; // dataMemberOffset only applies to DCS flags, to allow us to "bump" WCF by a notch
                return MemberHandlerResult.Done;
            }
            return MemberHandlerResult.Partial;
        }
    }
}
#endif