﻿#if !NO_RUNTIME
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
    public abstract class TypeAttributeMappingHandlerBase : MappingHandlerBase, ITypeAttributeHandler
    {
        TypeAttributeHandlerResult ITypeAttributeHandler.TryMap(AttributeMap item, TypeState s)
        {
            return TryMap(item, s, s.Input, s.Model);
        }

        protected abstract TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model);

    }
}
#endif