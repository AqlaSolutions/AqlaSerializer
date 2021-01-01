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
    sealed class DecimalSerializer : IProtoSerializerWithAutoType
    {
        private enum Variant
        {
            BclDecimal,
            String
        }

        private static DecimalSerializer s_BclDecimal, s_String;
        #if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(decimal);

        public static DecimalSerializer Create(CompatibilityLevel compatibilityLevel)
        {
            if (compatibilityLevel < CompatibilityLevel.Level300)
                return s_BclDecimal ??= new DecimalSerializer(Variant.BclDecimal);
            return s_String ??= new DecimalSerializer(Variant.String);
        }

        private readonly Variant _variant;
        private DecimalSerializer(Variant variant) => _variant = variant;
#endif
        public DecimalSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(decimal));
#endif
        }
        public Type ExpectedType => expectedType;

        bool IRuntimeProtoSerializerNode.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

#if !FEAT_IKVM
        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadDecimal(source);
        }
        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            BclHelpers.WriteDecimal((decimal)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), "WriteDecimal", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadDecimal", ExpectedType);
            }
        }
#endif

        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this);
        }

        void IRuntimeProtoSerializerNode.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitStateBasedWrite(_variant switch
            {
                Variant.String => nameof(BclHelpers.WriteDecimalString),
                _ => nameof(BclHelpers.WriteDecimal),
            }, valueFrom, typeof(BclHelpers));
        }
        void IRuntimeProtoSerializerNode.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity)
        {
            ctx.EmitStateBasedRead(typeof(BclHelpers), _variant switch
            {
                Variant.String => nameof(BclHelpers.ReadDecimalString),
                _ => nameof(BclHelpers.ReadDecimal),
            }, ExpectedType);
        }
    }
}
