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

namespace AqlaSerializer.Meta.Mapping
{
    public abstract class MappingHandlerBase
    {
        protected virtual bool CheckAqlaModelId(AttributeMap attrib, RuntimeTypeModel model)
        {
            if (attrib == null) return false;
            object actual;
            return attrib.TryGet(nameof(NonSerializableMemberAttribute.ModelId), out actual) && CheckAqlaModelId(actual, model);
        }

        protected virtual bool CheckAqlaModelId(SerializableMemberAttribute attr, RuntimeTypeModel model)
        {
            return CheckAqlaModelId(attr.ModelId, model);
        }

        protected virtual bool CheckAqlaModelId(object actualId, RuntimeTypeModel model)
        {
            return object.Equals(model.ModelId, actualId);
        }
    }
}
#endif