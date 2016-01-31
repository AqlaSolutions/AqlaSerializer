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
    /// Writes value if it's not null, should be only used when tail doesn't support null (e.g. no NetObjectValueDecorator)
    /// </summary>
    sealed class NoNullDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public bool DemandWireTypeStabilityStatus() => !_throwIfNull;
        readonly bool _throwIfNull;
        private readonly Type expectedType;
        
        public NoNullDecorator(TypeModel model, IProtoSerializerWithWireType tail, bool throwIfNull) : base(tail)
        {
            _throwIfNull = throwIfNull;
            Type tailType = tail.ExpectedType;
            if (Helpers.IsValueType(tailType))
            {
#if NO_GENERICS
                throw new NotSupportedException("NullDecorator cannot be used with a struct without generics support");
#else
                expectedType = model.MapType(typeof(Nullable<>)).MakeGenericType(tailType);
#endif
            }
            else
            {
                expectedType = tailType;
            }

        }

        public override Type ExpectedType
        {
            get { return expectedType; }
        }
        public override bool ReturnsValue
        {
            get { return true; }
        }
        public override bool RequiresOldValue
        {
            get { return true; }
        }

#if !FEAT_IKVM
        public override object Read(object value, ProtoReader source)
        {
            return Tail.Read(value, source);
        }
        public override void Write(object value, ProtoWriter dest)
        {
            // TODO emit
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
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (Compiler.Local oldValue = ctx.GetLocalWithValue(expectedType, valueFrom))
            {
                if (Tail.RequiresOldValue)
                {
                    if (expectedType.IsValueType)
                    {
                        ctx.LoadAddress(oldValue, expectedType);
                        ctx.EmitCall(expectedType.GetMethod("GetValueOrDefault", Helpers.EmptyTypes));
                    }
                    else
                    {
                        ctx.LoadValue(oldValue);
                    }
                }
                Tail.EmitRead(ctx, null);
                // note we demanded always returns a value
                if (expectedType.IsValueType)
                {
                    ctx.EmitCtor(expectedType, Tail.ExpectedType); // re-nullable<T> it
                }
                ctx.StoreValue(oldValue);
            }
        }
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using(Compiler.Local valOrNull = ctx.GetLocalWithValue(expectedType, valueFrom))
            {
                
                if (expectedType.IsValueType)
                {
                    ctx.LoadAddress(valOrNull, expectedType);
                    ctx.LoadValue(expectedType.GetProperty("HasValue"));
                }
                else
                {
                    ctx.LoadValue(valOrNull);
                }
                Compiler.CodeLabel @end = ctx.DefineLabel();
                ctx.BranchIfFalse(@end, false);
                if (expectedType.IsValueType)
                {
                    ctx.LoadAddress(valOrNull, expectedType);
                    ctx.EmitCall(expectedType.GetMethod("GetValueOrDefault", Helpers.EmptyTypes));
                }
                else
                {
                    ctx.LoadValue(valOrNull);
                }
                Tail.EmitWrite(ctx, null);

                ctx.MarkLabel(@end);
            }
        }
#endif
    }
}
#endif