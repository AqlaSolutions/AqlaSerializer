// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Diagnostics;

using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class StringSerializer : IProtoSerializerWithAutoType
    {
        private StringSerializer() { }
        internal static readonly StringSerializer Instance = new StringSerializer();
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(string);
#endif
        public StringSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(string));
#endif
        }
        public Type ExpectedType => expectedType;

        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            ProtoWriter.WriteString((string)value, dest);
        }
        bool IRuntimeProtoSerializerNode.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value == null); // since replaces
            
            return source.ReadString();
        }
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteString", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadString", ExpectedType);
            }
        }
#endif
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this);
        }

        void IRuntimeProtoSerializerNode.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using var loc = ctx.GetLocalWithValue(typeof(string), valueFrom);
            ctx.LoadState();
            ctx.LoadValue(loc);
            ctx.LoadNullRef(); // map
            ctx.EmitCall(typeof(ProtoWriter.State).GetMethod(nameof(ProtoWriter.State.WriteString), BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(string), typeof(StringMap) }, null));
        }
        void IRuntimeProtoSerializerNode.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity)
        {
            ctx.LoadState();
            ctx.LoadNullRef(); // map
            ctx.EmitCall(typeof(ProtoReader.State).GetMethod(nameof(ProtoReader.State.ReadString), BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(StringMap) }, null));
        }

        bool IDirectWriteNode.CanEmitDirectWrite(WireType wireType) => wireType == WireType.String;

        void IDirectWriteNode.EmitDirectWrite(int fieldNumber, WireType wireType, Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using var loc = ctx.GetLocalWithValue(typeof(string), valueFrom);
            ctx.LoadState();
            ctx.LoadValue(fieldNumber);
            ctx.LoadValue(loc);
            ctx.LoadNullRef(); // map
            ctx.EmitCall(typeof(ProtoWriter.State).GetMethod(nameof(ProtoWriter.State.WriteString), BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(int), typeof(string), typeof(StringMap) }, null));
        }
    }
}
