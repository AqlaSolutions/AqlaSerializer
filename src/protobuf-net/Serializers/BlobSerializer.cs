﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
using System.Reflection.Emit;

#endif

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class BlobSerializer : IProtoSerializerWithAutoType
    {
        public Type ExpectedType => expectedType;

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
            this._overwriteList = overwriteList;
        }

        private readonly bool _overwriteList;
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            var result = ProtoReader.AppendBytes(_overwriteList ? null : (byte[])value, source);
            if (_overwriteList || value == null)
                ProtoReader.NoteObject(result, source);
            return result;
        }

        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteBytes((byte[])value, dest);
        }
#endif
        bool IProtoSerializer.RequiresOldValue => !_overwriteList;
        
        public bool CanCancelWriting { get; }


#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteBytes", valueFrom);
            }
        }
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                using (var value = ctx.GetLocalWithValueForEmitRead(this, valueFrom)) // overwriteList ? null : value
                using (var result = ctx.Local(ExpectedType))
                {
                    g.Assign(result, g.ReaderFunc.AppendBytes(value));
                    if (!value.IsNullRef()) g.If(value.AsOperand == null);
                    {
                        //if (overwriteList || value == null)
                        g.Reader.NoteObject(result);
                    }
                    if (!value.IsNullRef()) g.End();
                    
                    ctx.LoadValue(result);
                }
            }
        }
#endif
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this, !_overwriteList ? "append" : null);
        }
    }
}

#endif