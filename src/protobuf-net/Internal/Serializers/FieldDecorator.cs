using ProtoBuf.Internal;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta;
using System.Diagnostics;
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

        public override void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            Helpers.DebugAssert(value != null);
            Tail.Write(GetValue(value), dest);

        }

        public override object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value != null);
            object newVal = Tail.Read(Tail.RequiresOldValue ? GetValue(value) : null, source);
            if (_accessors.Set != null)
                _accessors.Set(value, newVal);
            else
                _field.SetValue(value, newVal);
            return value;
        }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.LoadAddress(valueFrom, ExpectedType);
            ctx.LoadValue(field);
            ctx.WriteNullCheckedTail(field.FieldType, Tail, null);
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom);
            if (Tail.RequiresOldValue)
            {
                ctx.LoadAddress(loc, ExpectedType);
                ctx.LoadValue(field);
            }
            // value is either now on the stack or not needed
            ctx.ReadNullCheckedTail(field.FieldType, Tail, null);

            // the field could be a backing field that needs to be raised back to
            // the property if we're doing a full compile
            MemberInfo member = field;
            ctx.CheckAccessibility(ref member);
            bool writeValue = member is FieldInfo;

            if (writeValue)
            {
                if (Tail.ReturnsValue)
                {
                    var localType = PropertyDecorator.ChooseReadLocalType(field.FieldType, Tail.ExpectedType);
                    using Compiler.Local newVal = new Compiler.Local(ctx, localType);
                    ctx.StoreValue(newVal);
                    if (field.FieldType.IsValueType)
                    {
                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(newVal);
                        ctx.StoreValue(field);
                    }
                    else
                    {
                        Compiler.CodeLabel allDone = ctx.DefineLabel();
                        ctx.LoadValue(newVal);
                        ctx.BranchIfFalse(allDone, true); // interpret null as "don't assign"

                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(newVal);

                        // cast if needed (this is mostly for ReadMap/ReadRepeated)
                        if (!field.FieldType.IsValueType && !localType.IsValueType
                            && !field.FieldType.IsAssignableFrom(localType))
                        {
                            ctx.Cast(field.FieldType);
                        }

                        ctx.StoreValue(field);
                        ctx.MarkLabel(allDone);
                    }
                }
            }
            else
            {
                // can't use result
                if (Tail.ReturnsValue)
                {
                    ctx.DiscardValue();
                }
            }
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => _forType.IsValueType;

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
