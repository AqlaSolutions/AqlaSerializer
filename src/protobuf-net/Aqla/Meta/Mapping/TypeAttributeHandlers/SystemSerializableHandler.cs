﻿// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

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

namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public class SystemSerializableHandler : TypeAttributeMappingHandlerBase
    {
        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            // we check CanUse everywhere but not family because GetContractFamily is based on CanUse
            // and CanUse is based on the settings
            // except is for SerializableAttribute which family is not returned if other families are present
            if (a.HasFamily(MetaType.AttributeFamily.SystemSerializable))
            {
                s.ImplicitFields = ImplicitFieldsMode.AllFields;
                s.ImplicitAqla = true;
            }
            return TypeAttributeHandlerResult.Done;
        }


    }
}

#endif