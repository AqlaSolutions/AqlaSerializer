// Code by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;


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

namespace AqlaSerializer
{
    public interface IAutoAddStrategy
    {
        bool GetIgnoreListHandling(Type type);
        bool GetAsReferenceDefault(Type type, bool isProtobufNetLegacyMember);
        void ApplyDefaultBehaviour(MetaType type);
        MetaType.AttributeFamily GetContractFamily(Type type);
    }
}
#endif