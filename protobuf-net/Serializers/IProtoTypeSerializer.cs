// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using AqlaSerializer.Meta;
namespace AqlaSerializer.Serializers
{
    interface IProtoTypeSerializer : IProtoSerializerWithWireType
    {
        bool HasCallbacks(TypeModel.CallbackType callbackType);
        bool CanCreateInstance();
#if !FEAT_IKVM
        /// <summary>
        /// Should call ProtoReader.NoteObject!
        /// </summary>
        object CreateInstance(ProtoReader source);
        void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context);
#endif
#if FEAT_COMPILER
        void EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType);
#endif
#if FEAT_COMPILER
        void EmitCreateInstance(Compiler.CompilerContext ctx);
#endif
    }
}
#endif