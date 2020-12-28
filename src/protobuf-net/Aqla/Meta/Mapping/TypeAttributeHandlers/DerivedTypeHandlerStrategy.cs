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

namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public class DerivedTypeHandlerStrategy : ITypeAttributeHandler
    {
        public TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s)
        {
            RuntimeTypeModel model = s.Model;
            object tmp;
            int tag = 0;
            if (item.TryGet("tag", out tmp)) tag = (int)tmp;
            Type knownType = null;
            try
            {
                if (item.TryGet("knownTypeName", out tmp)) knownType = model.GetType((string)tmp, Helpers.GetAssembly(s.Type));
                else if (item.TryGet("knownType", out tmp)) knownType = (Type)tmp;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to resolve sub-type of: " + s.Type.FullName, ex);
            }
            if (knownType == null)
            {
                throw new InvalidOperationException("Unable to resolve sub-type of: " + s.Type.FullName);
            }
            s.DerivedTypes.Add(new DerivedTypeCandidate(tag, knownType));
            return TypeAttributeHandlerResult.Done;
        }
    }
}

#endif