// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using AqlaSerializer.Compiler;
#if FEAT_COMPILER
using System.Reflection.Emit;

#endif

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class BlobSerializer : IProtoSerializerWithAutoType
    {
        public Type ExpectedType { get { return expectedType; } }

#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(byte[]);
#endif

        public BlobSerializer(AqlaSerializer.Meta.TypeModel model, bool overwriteList)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(byte[]));
#endif
            this.overwriteList = overwriteList;
        }

        private readonly bool overwriteList;
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            var result = ProtoReader.AppendBytes(overwriteList ? null : (byte[])value, source);
            if (overwriteList || value == null)
                ProtoReader.NoteObject(result, source);
            return result;
        }

        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteBytes((byte[])value, dest);
        }
#endif
        bool IProtoSerializer.RequiresOldValue { get { return !overwriteList; } }
        bool IProtoSerializer.ReturnsValue { get { return true; } }
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicWrite("WriteBytes", valueFrom);
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (var value = ctx.GetLocalWithValueForEmitRead(this, valueFrom)) // overwriteList ? null : value
            using (var result = value?.AsCopy() ?? ctx.Local(ExpectedType))
            {
                g.Assign(result, g.ReaderFunc.AppendBytes(value));
                if (!value.IsNullRef()) g.If(value.AsOperand == null);
                {
                    //if (overwriteList || value == null)
                    g.Reader.NoteObject(result);
                }
                if (!value.IsNullRef()) g.End();

                if (overwriteList)
                    ctx.LoadValue(result);
            }
        }
#endif
    }
}

#endif