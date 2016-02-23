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
    public class XmlContractMemberHandler : MemberMappingHandlerBase
    {
        protected override MemberHandlerResult TryMap(MemberState s, ref MemberMainSettingsValue main, MemberInfo member, RuntimeTypeModel model)
        {
            if (!s.Input.CanUse(AttributeType.Xml)) return MemberHandlerResult.NotFound;
            if (AttributeMap.GetAttribute(s.Input.Attributes, "System.Xml.Serialization.XmlIgnoreAttribute") != null) return MemberHandlerResult.Ignore;
            if (!s.Input.HasFamily(MetaType.AttributeFamily.XmlSerializer)) return MemberHandlerResult.NotFound;
            var attrib = AttributeMap.GetAttribute(s.Input.Attributes, "System.Xml.Serialization.XmlElementAttribute")
                         ?? AttributeMap.GetAttribute(s.Input.Attributes, "System.Xml.Serialization.XmlArrayAttribute");
            if (attrib == null) return MemberHandlerResult.NotFound;

            if (main.Tag <= 0) attrib.TryGetNotDefault("Order", ref main.Tag);
            if (string.IsNullOrEmpty(main.Name)) attrib.TryGetNotEmpty("ElementName", ref main.Name);

            return main.Tag >= s.MinAcceptFieldNumber ? MemberHandlerResult.Done : MemberHandlerResult.Partial;
        }
    }
}
#endif