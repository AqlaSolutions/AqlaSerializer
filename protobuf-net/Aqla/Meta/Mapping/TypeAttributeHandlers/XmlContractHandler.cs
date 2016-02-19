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

namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public class XmlContractHandler : TypeAttributeMappingHandlerBase
    {
        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            if (a.HasFamily(MetaType.AttributeFamily.XmlSerializer))
            {
                var main = s.SettingsValue;
                object tmp;
                if (main.Name == null && item.TryGet("TypeName", out tmp)) main.Name = (string)tmp;
                s.SettingsValue = main;
                return TypeAttributeHandlerResult.Done;
            }
            return TypeAttributeHandlerResult.Continue;
        }
    }
}

#endif