// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using AltLinq;
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
    sealed class PropertyDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _property.Name))
                Tail.WriteDebugSchema(builder);
        }

        public bool DemandWireTypeStabilityStatus() => _tail.DemandWireTypeStabilityStatus();
        public override Type ExpectedType => _forType;
        private readonly PropertyInfo _property;
        readonly IProtoSerializerWithWireType _tail;
        private readonly Type _forType;
        public override bool RequiresOldValue => true;
        private readonly bool _canSetInRuntime;
        private readonly MethodInfo _shadowSetter;

        AccessorsCache.Accessors _accessors;

        public PropertyDecorator(TypeModel model, Type forType, PropertyInfo property, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(property != null);
            this._forType = forType;
            this._property = property;
            _tail = tail;
            SanityCheck(model, property, tail, out _canSetInRuntime, true, true, false);
            _shadowSetter = Helpers.GetShadowSetter(model, property);
#if FEAT_COMPILER && !FEAT_IKVM
            _accessors = AccessorsCache.GetAccessors(property);
            if (_shadowSetter != null)
                _accessors.Set = AccessorsCache.GetShadowSetter(_shadowSetter);
#endif
        }

        private static void SanityCheck(TypeModel model, PropertyInfo property, IProtoSerializer tail, out bool writeValue, bool nonPublic, bool allowInternal, bool forEmit)
        {
            if (property == null) throw new ArgumentNullException("property");

            writeValue =
#if FEAT_COMPILER
                (!forEmit || tail.EmitReadReturnsValue) &&
#endif
                (Helpers.CheckIfPropertyWritable(model, property, nonPublic, allowInternal));

            if (!property.CanRead || Helpers.GetGetMethod(property, nonPublic, allowInternal) == null)
            {
                throw new InvalidOperationException("Cannot serialize property without a get accessor");
            }
            if (!writeValue && (!tail.RequiresOldValue || Helpers.IsValueType(tail.ExpectedType)))
            { // so we can't save the value, and the tail doesn't use it either... not helpful
                // or: can't write the value, so the struct value will be lost
                throw new InvalidOperationException("Cannot apply changes to property " + property.DeclaringType.FullName + "." + property.Name);
            }
        }


#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            Helpers.DebugAssert(value != null);
            Tail.Write(Helpers.GetPropertyValue(_property, value), dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value != null);

            object oldVal = Tail.RequiresOldValue
                                ? _accessors.Get != null ? _accessors.Get(value) : Helpers.GetPropertyValue(_property, value)
                                : null;
            object newVal = Tail.Read(oldVal, source);
            if (_canSetInRuntime
                && (!Tail.RequiresOldValue // always set where can't check oldVal
                    // and if it's value type or nullable with changed null/not null or ref
                    || (Helpers.IsValueType(_property.PropertyType) && oldVal != null && newVal != null)
                    || !ReferenceEquals(oldVal, newVal)
                   ))
            {
                if (_accessors.Set != null)
                {
                    _accessors.Set(value, newVal);
                }
                else
                {

                    if (_shadowSetter == null)
                    {
                        _property.SetValue(value, newVal, null);
                    }
                    else
                    {
                        _shadowSetter.Invoke(value, new object[] { newVal });
                    }
                }
            }
            return value;
        }

#endif

#if FEAT_COMPILER 
        public override bool EmitReadReturnsValue { get { return Helpers.IsValueType(_forType); } }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _property.Name))
            {
                ctx.LoadAddress(valueFrom, ExpectedType);
                ctx.LoadValue(_property);
                Tail.EmitWrite(ctx, null);
            }
        }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _property.Name))
            {
                var g = ctx.G;
                bool canSet;
                SanityCheck(ctx.Model, _property, Tail, out canSet, ctx.NonPublic, ctx.AllowInternal(_property), true);

                using (Compiler.Local loc = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                using (Compiler.Local oldVal = canSet ? new Compiler.Local(ctx, _property.PropertyType) : null)
                using (Compiler.Local newVal = new Compiler.Local(ctx, _property.PropertyType))
                {
                    Compiler.Local valueForTail = null;
                    if (Tail.RequiresOldValue)
                    {
                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(_property);
                        if (!oldVal.IsNullRef())
                        {
                            if (Tail.RequiresOldValue) // we need it later
                                ctx.CopyValue();
                            ctx.StoreValue(oldVal);
                        }
                    }

                    if (Tail.RequiresOldValue) // !here! <-- we need value again
                    {
                        if (!Tail.EmitReadReturnsValue)
                        {
                            ctx.StoreValue(newVal);
                            valueForTail = newVal;
                        } // otherwise valueForTail = null (leave value on stack)
                    }

                    Tail.EmitRead(ctx, valueForTail);

                    // otherwise newVal was passed to EmitRead so already has necessary data
                    if (Tail.EmitReadReturnsValue)
                        ctx.StoreValue(newVal);

                    // check-condition:
                    //(!Tail.RequiresOldValue // always set where can't check oldVal
                    //                        // and if it's value type or nullable with changed null/not null or ref
                    //    || (Helpers.IsValueType(property.PropertyType) && oldVal != null && newVal != null)
                    //    || !ReferenceEquals(oldVal, newVal)
                    //   ))
                    if (canSet)
                    {
                        bool check = Tail.RequiresOldValue;
                        if (check)
                        {
                            var condition = !g.StaticFactory.InvokeReferenceEquals(oldVal, newVal);

                            if (Helpers.IsValueType(_property.PropertyType))
                                condition = (oldVal.AsOperand != null && newVal.AsOperand != null) || condition;

                            g.If(condition);
                        }

                        ctx.LoadAddress(loc, ExpectedType);
                        ctx.LoadValue(newVal);
                        if (_shadowSetter == null)
                        {
                            ctx.StoreValue(_property);
                        }
                        else
                        {
                            ctx.EmitCall(_shadowSetter);
                        }

                        if (check)
                            g.End();
                    }

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(loc);
                }
            }
        }
#endif
    }
}

#endif