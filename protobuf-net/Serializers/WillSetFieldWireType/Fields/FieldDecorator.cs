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

        public override Type ExpectedType { get { return forType; } }
        private readonly FieldInfo field;
        private readonly Type forType;
        public override bool RequiresOldValue { get { return true; } }
        public override bool ReturnsValue { get { return false; } }

        AccessorsCache.Accessors _accessors;


        public FieldDecorator(Type forType, FieldInfo field, IProtoSerializerWithWireType tail) : base(tail)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(field != null);
            this.forType = forType;
            this.field = field;
#if FEAT_COMPILER && !FEAT_IKVM
            _accessors = AccessorsCache.GetAccessors(field);
#endif

        }
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            Helpers.DebugAssert(value != null);  // TODO emit without null check
            Tail.Write(field.GetValue(value), dest);
            
        }
        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value != null);
            object newVal = Tail.Read(Tail.RequiresOldValue ? field.GetValue(value) : null, source);
            if (Tail.ReturnsValue) // no need to check it's not the same as old value because field set is basically "free" operation
            {
                if (_accessors.Set != null)
                    _accessors.Set(value, newVal);
                else
                    field.SetValue(value, newVal);
            }
            return null;
        }
#endif

#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.LoadAddress(valueFrom, ExpectedType);
            ctx.LoadValue(field);
            ctx.WriteNullCheckedTail(field.FieldType, Tail, null, true);
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            {
                if (Tail.RequiresOldValue)
                {
                    ctx.LoadAddress(loc, ExpectedType);
                    ctx.LoadValue(field);  
                }
                // value is either now on the stack or not needed
                ctx.ReadNullCheckedTail(field.FieldType, Tail, null);

                if (Tail.ReturnsValue)
                {
                    using (Compiler.Local newVal = new Compiler.Local(ctx, field.FieldType))
                    {
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
                            ctx.StoreValue(field);

                            ctx.MarkLabel(allDone);
                        }
                    }
                }
            }
        }
#endif
    }
}
#endif