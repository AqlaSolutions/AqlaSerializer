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
    /// <summary>
    /// Writes value if it's not null, should be only used when tail doesn't support null (e.g. no NetObjectValueDecorator)
    /// </summary>
    sealed class NoNullDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public bool DemandWireTypeStabilityStatus() => !_throwIfNull;
        readonly bool _throwIfNull;
        readonly Type _expectedType;

        public NoNullDecorator(TypeModel model, IProtoSerializerWithWireType tail, bool throwIfNull)
            : base(tail)
        {
            _throwIfNull = throwIfNull;
            Type tailType = tail.ExpectedType;
            if (Helpers.IsValueType(tailType))
            {
#if NO_GENERICS
                throw new NotSupportedException("NullDecorator cannot be used with a struct without generics support");
#else
                _expectedType = model.MapType(typeof(Nullable<>)).MakeGenericType(tailType);
#endif
            }
            else
            {
                _expectedType = tailType;
            }

        }

        public override Type ExpectedType => _expectedType;
        public override bool RequiresOldValue => true;

#if !FEAT_IKVM
        public override object Read(object value, ProtoReader source)
        {
            return Tail.Read(Tail.RequiresOldValue ? value : null, source);
        }

        public override void Write(object value, ProtoWriter dest)
        {
            if (value != null)
            {
                Tail.Write(value, dest);
            }
            else if (_throwIfNull)
                throw new NullReferenceException();
            else
                ProtoWriter.WriteFieldHeaderCancelBegin(dest);
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue { get { return true; } }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                using (Compiler.Local oldValue = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                {
                    Compiler.Local tempLocal = null;
                    Compiler.Local valueForTail;
                    Debug.Assert(Tail.RequiresOldValue || Tail.EmitReadReturnsValue);
                    if (Tail.RequiresOldValue)
                    {
                        if (_expectedType.IsValueType)
                        {
                            tempLocal = ctx.Local(Tail.ExpectedType);
                            ctx.LoadAddress(oldValue, _expectedType);
                            ctx.EmitCall(_expectedType.GetMethod("GetValueOrDefault", Helpers.EmptyTypes));
                            ctx.StoreValue(tempLocal);
                            valueForTail = tempLocal;
                        }
                        else valueForTail = oldValue;
                    }
                    else valueForTail = null;

                    // valueForTail contains:
                    // null: when not required old value
                    // oldValue local: when reference type
                    // tempLocal: when nullable value type

                    Tail.EmitRead(ctx, valueForTail);

                    if (_expectedType.IsValueType)
                    {
                        if (!Tail.EmitReadReturnsValue)
                            ctx.LoadValue(tempLocal);
                        // note we demanded always returns a value
                        ctx.EmitCtor(_expectedType, Tail.ExpectedType); // re-nullable<T> it
                    }

                    if (Tail.EmitReadReturnsValue || _expectedType.IsValueType)
                        ctx.StoreValue(oldValue);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(oldValue);

                    if (!tempLocal.IsNullRef()) tempLocal.Dispose();
                }
            }
        }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                using (Compiler.Local valOrNull = ctx.GetLocalWithValue(_expectedType, valueFrom))
                {

                    if (_expectedType.IsValueType)
                    {
                        ctx.LoadAddress(valOrNull, _expectedType);
                        ctx.LoadValue(_expectedType.GetProperty("HasValue"));
                    }
                    else
                    {
                        ctx.LoadValue(valOrNull);
                    }
                    Compiler.CodeLabel done = ctx.DefineLabel();
                    Compiler.CodeLabel onNull = ctx.DefineLabel();

                    ctx.BranchIfFalse(onNull, false);
                    // if !=null
                    {
                        if (_expectedType.IsValueType)
                        {
                            ctx.LoadAddress(valOrNull, _expectedType);
                            ctx.EmitCall(_expectedType.GetMethod("GetValueOrDefault", Helpers.EmptyTypes));
                        }
                        else
                        {
                            ctx.LoadValue(valOrNull);
                        }
                        Tail.EmitWrite(ctx, null);

                        ctx.Branch(done, true);
                    }
                    // else
                    {
                        ctx.MarkLabel(onNull);
                        if (_throwIfNull)
                        {
                            ctx.G.ThrowNullReferenceException();
                            ctx.G.ForceResetUnreachableState();
                        }
                        else
                            ctx.G.Writer.WriteFieldHeaderCancelBegin();
                    }
                    ctx.MarkLabel(done);
                }
            }
        }
#endif
    }
}

#endif