using ProtoBuf.Meta;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Diagnostics;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class DateTimeSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(DateTime);
        private static DateTimeSerializer s_Timestamp;
#endif
        public Type ExpectedType => expectedType;

        bool IRuntimeProtoSerializerNode.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }


        private readonly bool _includeKind;

        public static DateTimeSerializer Create(CompatibilityLevel compatibilityLevel, TypeModel model)
            =>  compatibilityLevel >= CompatibilityLevel.Level240
                ? s_Timestamp ??= new DateTimeSerializer(true, false)
                : new DateTimeSerializer(false, model.HasOption(TypeModel.TypeModelOptions.IncludeDateTimeKind));

        public DateTimeSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(DateTime));
#endif
            _includeKind = model != null && model.SerializeDateTimeKind();
        }
#if !FEAT_IKVM
        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadDateTime(source);
        }
        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            if(_includeKind)
                BclHelpers.WriteDateTimeWithKind((DateTime)value, dest);
            else
                BclHelpers.WriteDateTime((DateTime)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), _includeKind ? "WriteDateTimeWithKind" : "WriteDateTime", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadDateTime", ExpectedType);
            }
        }
#endif

        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this);
        }

        void IRuntimeProtoSerializerNode.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitStateBasedWrite(
                _useTimestamp ? nameof(BclHelpers.WriteTimestamp)
                : _includeKind ? nameof(BclHelpers.WriteDateTimeWithKind) : nameof(BclHelpers.WriteDateTime), valueFrom, typeof(BclHelpers));
        }

        void IRuntimeProtoSerializerNode.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity)
        {
            if (_useTimestamp) ctx.LoadValue(entity);
            ctx.EmitStateBasedRead(typeof(BclHelpers),
                _useTimestamp ? nameof(BclHelpers.ReadTimestamp) : nameof(BclHelpers.ReadDateTime),
                ExpectedType);
        }
    }
}
