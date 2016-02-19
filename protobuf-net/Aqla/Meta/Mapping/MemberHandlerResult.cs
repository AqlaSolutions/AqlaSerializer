#if !NO_RUNTIME
using System;
using System.Collections;
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

namespace AqlaSerializer.Meta.Mapping
{
    public enum MemberHandlerResult
    {
        NotFound = 0,
        Partial,
        Ignore,
        Done,
    }
}
#endif