// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    sealed class FieldDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _field.Name))
                Tail.WriteDebugSchema(builder);
        }

        public bool DemandWireTypeStabilityStatus() => _tail.DemandWireTypeStabilityStatus();
        public override Type ExpectedType => _forType;
        private readonly FieldInfo _field;
        readonly IProtoSerializerWithWireType _tail;
        private readonly Type _forType;
        public override bool RequiresOldValue => true;

        AccessorsCache.Accessors _accessors;


        public FieldDecorator(Type forType, FieldInfo field, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(field != null);
            this._forType = forType;
            this._field = field;
            _tail = tail;
#if FEAT_COMPILER && !FEAT_IKVM
            _accessors = AccessorsCache.GetAccessors(field);
#endif

        }

#if !FEAT_IKVM
        object GetValue(object instance)
        {
            return _accessors.Get != null ? _accessors.Get(instance) : _field.GetValue(instance);
        }

        public override void Write(object value, ProtoWriter dest)
        {
            Helpers.DebugAssert(value != null);
            Tail.Write(GetValue(value), dest);

        }

        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value != null);
            object newVal = Tail.Read(Tail.RequiresOldValue ? GetValue(value) : null, source);
            if (_accessors.Set != null)
                _accessors.Set(value, newVal);
            else
                _field.SetValue(value, newVal);
            return value;
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue { get { return Helpers.IsValueType(_forType); } }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _field.Name))
            {
                ctx.LoadAddress(valueFrom, ExpectedType);
                ctx.LoadValue(_field);
                Tail.EmitWrite(ctx, null);
            }
        }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _field.Name))
            {
                using (Compiler.Local loc = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                using (Compiler.Local newVal = new Compiler.Local(ctx, _field.FieldType))
                {
                    Compiler.Local valueForTail;
                    if (Tail.RequiresOldValue)
                    {
                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(_field);
                        if (!Tail.EmitReadReturnsValue)
                        {
                            ctx.StoreValue(newVal);
                            valueForTail = newVal;
                        }
                        else valueForTail = null; // on stack
                    }
                    else valueForTail = null;

                    Tail.EmitRead(ctx, valueForTail);

                    if (Tail.EmitReadReturnsValue)
                        ctx.StoreValue(newVal);

                    ctx.LoadAddress(loc, ExpectedType);
                    ctx.LoadValue(newVal);
                    ctx.StoreValue(_field);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(loc);
                }
            }
        }
#endif
    }
}

#endif