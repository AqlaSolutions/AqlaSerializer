// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Diagnostics;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif


namespace AqlaSerializer.Serializers
{
    sealed class GuidSerializer : IProtoSerializerWithAutoType
    {
        private enum Variant
        {
            BclGuid = 0,
            GuidString = 1,
            GuidBytes = 2,
        }
        private readonly Variant _variant;
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(Guid);
        private static GuidSerializer s_Legacy, s_String, s_Bytes;

        internal static GuidSerializer Create(CompatibilityLevel compatibilityLevel, DataFormat dataFormat)
        {
            if (compatibilityLevel < CompatibilityLevel.Level300)
                return s_Legacy ??= new GuidSerializer(Variant.BclGuid);
            if (dataFormat == DataFormat.FixedSize)
                return s_Bytes ??= new GuidSerializer(Variant.GuidBytes);
            return s_String ??= new GuidSerializer(Variant.GuidString);
        }

        private GuidSerializer(Variant variant) => _variant = variant;
#endif
        public GuidSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(Guid));
#endif
        }

        public Type ExpectedType => expectedType;

        bool IRuntimeProtoSerializerNode.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

#if !FEAT_IKVM
        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            BclHelpers.WriteGuid((Guid)value, dest);
        }
        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadGuid(source);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), "WriteGuid", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadGuid", ExpectedType);
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
                _variant switch {
                    Variant.GuidString => nameof(BclHelpers.WriteGuidString),
                    Variant.GuidBytes => nameof(BclHelpers.WriteGuidBytes),
                    _ => nameof(BclHelpers.WriteGuid),
                }, valueFrom, typeof(BclHelpers));
        }

        void IRuntimeProtoSerializerNode.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity)
        {
            ctx.EmitStateBasedRead(typeof(BclHelpers),
                _variant switch
                {
                    Variant.GuidString => nameof(BclHelpers.ReadGuidString),
                    Variant.GuidBytes => nameof(BclHelpers.ReadGuidBytes),
                    _ => nameof(BclHelpers.ReadGuid),
                }, ExpectedType);
        }

    }
}
