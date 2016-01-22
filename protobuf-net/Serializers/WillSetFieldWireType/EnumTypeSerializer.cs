//// Modified by Vladyslav Taranov for AqlaSerializer, 2016

//#if !NO_RUNTIME
//using System;
//using System.Runtime.InteropServices;
//using AqlaSerializer.Compiler;
//using AqlaSerializer.Meta;
//#if FEAT_IKVM
//using Type = IKVM.Reflection.Type;
//using IKVM.Reflection;
//#else
//using System.Reflection;

//#endif

//namespace AqlaSerializer.Serializers
//{
//    sealed class EnumTypeSerializer : IProtoTypeSerializer
//    {
//        readonly Type _type;
//        readonly EnumSerializer _enumSerializer;

//        public EnumTypeSerializer(Type type, EnumSerializer.EnumPair[] map)
//        {
//            _type = type;
//            _enumSerializer = new EnumSerializer(type, map);
//        }

//#if !FEAT_IKVM
//        public object Read(object value, ProtoReader source)
//        {
//            return _enumSerializer.Read(value, source);
//        }

//        public void Write(object value, ProtoWriter dest)
//        {
//            _enumSerializer.Write(value, dest);
//        }

//        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
//        {

//        }

//        public object CreateInstance(ProtoReader source)
//        {
//            return Activator.CreateInstance(_type);
//        }
//#endif

//#if FEAT_COMPILER
//        public void EmitWrite(CompilerContext ctx, Local valueFrom)
//        {
//            // TODO
//            ((IProtoSerializer)_enumSerializer).EmitWrite(ctx, valueFrom);
//        }

//        public void EmitRead(CompilerContext ctx, Local entity)
//        {
//            ((IProtoSerializer)_enumSerializer).EmitRead(ctx, entity);
//        }
//#endif
//#if FEAT_COMPILER

//        public void EmitCreateInstance(CompilerContext ctx)
//        {
//            using (var local = new Local(ctx, _type))
//            {
//                ctx.LoadAddress(local, _type);
//                ctx.EmitCtor(_type);
//                ctx.LoadValue(local);
//            }
//        }

//        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
//        {
        
//        }

//#endif

//        public bool RequiresOldValue => ((IProtoSerializer)_enumSerializer).RequiresOldValue;

//        public bool ReturnsValue => ((IProtoSerializer)_enumSerializer).ReturnsValue;

//        public Type ExpectedType => _enumSerializer.ExpectedType;

//        public bool CanCreateInstance()
//        {
//            return true;
//        }

//        public bool HasCallbacks(TypeModel.CallbackType callbackType)
//        {
//            return false;
//        }

//    }
//}