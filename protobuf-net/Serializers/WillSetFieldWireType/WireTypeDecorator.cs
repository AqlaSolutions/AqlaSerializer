// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif


namespace AqlaSerializer.Serializers
{
    /// <summary>
    /// When we expect an extended wire type like SignedVariant we need to "upgrade" it to our version in reader
    /// </summary>
    sealed class WireTypeDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
        public bool DemandWireTypeStabilityStatus() => true;

        public WireTypeDecorator(WireType wireType, IProtoSerializerWithAutoType tail, bool strict = false)
            : base(tail)
        {
            _strict = strict;
            _wireType = wireType;
        }

        readonly WireType _wireType;

        readonly bool _strict;

        private bool NeedsHint
        {
            get { return ((int)_wireType & ~7) != 0; }
        }

#if !FEAT_IKVM
        public override object Read(object value, ProtoReader source)
        {
            // ReadFieldHeader called outside
            // so wireType is read already too!

            if (_strict) { source.Assert(_wireType); }
            else if (NeedsHint) { source.Hint(_wireType); }
            return Tail.Read(value, source);
        }
        public override void Write(object value, ProtoWriter dest)
        {
            // WriteFieldHeaderBegin called outside
            // but wireType is not set yet
            ProtoWriter.WriteFieldHeaderCompleteAnyType(_wireType, dest);
            Tail.Write(value, dest);
        }
#endif


#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
#if DEBUG_EMIT
            ctx.LoadValue("WireType dec");
            ctx.DiscardValue();
#endif
            ctx.LoadValue((int)_wireType);
            ctx.LoadReaderWriter();
            ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("WriteFieldHeaderCompleteAnyType"));
            Tail.EmitWrite(ctx, valueFrom);
        }
        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
#if DEBUG_EMIT
            ctx.LoadValue("WireType dec");
            ctx.DiscardValue();
#endif
            if (_strict || NeedsHint)
            {
                ctx.LoadReaderWriter();
                ctx.LoadValue((int)_wireType);
                ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod(_strict ? "Assert" : "Hint"));
            }
            Tail.EmitRead(ctx, valueFrom);
        }
#endif
        public override bool RequiresOldValue => Tail.RequiresOldValue;
        
        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            IProtoTypeSerializer pts = Tail as IProtoTypeSerializer;
            return pts != null && pts.HasCallbacks(callbackType);
        }

        public bool CanCreateInstance()
        {
            IProtoTypeSerializer pts = Tail as IProtoTypeSerializer;
            return pts != null && pts.CanCreateInstance();
        }
#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)Tail).CreateInstance(source);
        }
        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            IProtoTypeSerializer pts = Tail as IProtoTypeSerializer;
            if (pts != null) pts.Callback(value, callbackType, context);
        }
#endif
#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => Tail.EmitReadReturnsValue;
        public void EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            // we only expect this to be invoked if HasCallbacks returned true, so implicitly Tail
            // **must** be of the correct type
            ((IProtoTypeSerializer)Tail).EmitCallback(ctx, valueFrom, callbackType);
        }
        public void EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)Tail).EmitCreateInstance(ctx);
        }
#endif
        public override Type ExpectedType
        {
            get { return Tail.ExpectedType; }
        }
    }

}
#endif