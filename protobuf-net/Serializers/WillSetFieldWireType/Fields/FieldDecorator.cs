﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016

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
        public bool DemandWireTypeStabilityStatus() => _tail.DemandWireTypeStabilityStatus();
        public override Type ExpectedType { get { return forType; } }
        private readonly FieldInfo field;
        readonly IProtoSerializerWithWireType _tail;
        private readonly Type forType;
        public override bool RequiresOldValue { get { return true; } }
        
        AccessorsCache.Accessors _accessors;


        public FieldDecorator(Type forType, FieldInfo field, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(field != null);
            this.forType = forType;
            this.field = field;
            _tail = tail;
#if FEAT_COMPILER && !FEAT_IKVM
            _accessors = AccessorsCache.GetAccessors(field);
#endif

        }

#if !FEAT_IKVM
        object GetValue(object instance)
        {
            return _accessors.Get != null ? _accessors.Get(instance) : field.GetValue(instance);
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
                field.SetValue(value, newVal);
            return value;
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue { get { return Helpers.IsValueType(forType); } }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, field.Name))
            {
                ctx.LoadAddress(valueFrom, ExpectedType);
                ctx.LoadValue(field);
                Tail.EmitWrite(ctx, null);
            }
        }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, field.Name))
            {
                using (Compiler.Local loc = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                using (Compiler.Local newVal = new Compiler.Local(ctx, field.FieldType))
                {
                    Compiler.Local valueForTail;
                    if (Tail.RequiresOldValue)
                    {
                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(field);
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
                    ctx.StoreValue(field);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(loc);
                }
            }
        }
#endif
    }
}

#endif