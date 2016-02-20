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
    public class AqlaSerializableHandler : TypeAttributeMappingHandlerBase
    {
        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            if (a.HasFamily(MetaType.AttributeFamily.Aqla) && CheckAqlaModelId(item, model))
            {
                var attr = item.GetRuntimeAttribute<SerializableTypeAttribute>(model);
                s.SettingsValue = attr.TypeSettings;
                if (s.SettingsValue.EnumPassthru.GetValueOrDefault())
                    s.AsEnum = false;

                if (!s.AsEnum)
                {
                    object tmp;
                    if (item.TryGet("DataMemberOffset", out tmp)) s.DataMemberOffset = (int)tmp;

#if !FEAT_IKVM
                    // IKVM can't access InferTagFromNameHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                    if (item.TryGet("InferTagFromNameHasValue", false, out tmp) && (bool)tmp)
#endif
                    {
                        if (item.TryGet("InferTagFromName", out tmp)) s.InferTagByName = (bool)tmp;
                    }

                    if (item.TryGet("ImplicitFields", out tmp) && tmp != null)
                        s.ImplicitMode = (ImplicitFieldsMode)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                    else
                        s.ImplicitMode = ImplicitFieldsMode.PublicProperties;

                    if (item.TryGet("ExplicitPropertiesContract", out tmp) && tmp != null)
                        s.ExplicitPropertiesContract = (bool)tmp;
                    else
                        s.ExplicitPropertiesContract = true;

                    if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) s.ImplicitFirstTag = (int)tmp;

                    if (s.ImplicitMode != ImplicitFieldsMode.None) s.ImplicitAqla = true;
                }

                return TypeAttributeHandlerResult.Done;
            }
            return TypeAttributeHandlerResult.Continue;
        }
    }
}

#endif