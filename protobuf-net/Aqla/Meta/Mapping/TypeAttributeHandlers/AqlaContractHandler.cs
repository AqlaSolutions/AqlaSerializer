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
    public class AqlaContractHandler : TypeAttributeMappingHandlerBase
    {
        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            if (a.HasFamily(MetaType.AttributeFamily.Aqla) && CheckAqlaModelId(item, model))
            {
                var attr = item.GetRuntimeAttribute<SerializableTypeAttribute>(model);
                s.SettingsValue = attr.TypeSettings;
                if (s.SettingsValue.EnumPassthru.GetValueOrDefault())
                    s.AsEnum = false;

                s.ImplicitOnlyWriteable = attr.ImplicitOnlyWriteable;

                if (!s.AsEnum)
                {
                    s.DataMemberOffset = attr.DataMemberOffset;
                    if (attr.InferTagFromNameHasValue)
                        s.InferTagByName = attr.InferTagFromName;
                    s.ImplicitFields = attr.ImplicitFields;
                    if (attr.ImplicitFirstTag != 0) s.ImplicitFirstTag = attr.ImplicitFirstTag;
                    
                    if (s.ImplicitFields != ImplicitFieldsMode.None) s.ImplicitAqla = true;
                }

                return TypeAttributeHandlerResult.Done;
            }
            return TypeAttributeHandlerResult.Continue;
        }
    }
}

#endif