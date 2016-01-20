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
    sealed class EnsureWireTypeDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
        public EnsureWireTypeDecorator(WireType wireType, bool strict, IProtoSerializer tail)
            : base(tail)
        {
            _wireType = wireType;
            _strict = strict;
        }

        readonly bool _strict;
        readonly WireType _wireType;

        private bool NeedsHint
        {
            get { return ((int)_wireType & ~7) != 0; }
        }

#if !FEAT_IKVM
        public override object Read(object value, ProtoReader source)
        {
            if (_strict) { source.Assert(_wireType); }
            else if (NeedsHint) { source.Hint(_wireType); }
            return Tail.Read(value, source);
        }
        public override void Write(object value, ProtoWriter dest)
        {
            Tail.Write(value, dest);
        }
#endif


#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Tail.EmitWrite(ctx, valueFrom);
        }
        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            if (_strict || NeedsHint)
            {
                ctx.LoadReaderWriter();
                ctx.LoadValue((int)_wireType);
                ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod(_strict ? "Assert" : "Hint"));
            }
            Tail.EmitRead(ctx, valueFrom);
        }
#endif
        public override bool RequiresOldValue { get { return Tail.RequiresOldValue; } }
        public override bool ReturnsValue { get { return Tail.ReturnsValue; } }

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