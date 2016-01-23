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
    /// Writes value if it's not null
    /// </summary>
    sealed class NullDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        private readonly Type expectedType;
        
        public NullDecorator(TypeModel model, IProtoSerializerWithWireType tail) : base(tail)
        {
            if (!tail.ReturnsValue)
                throw new NotSupportedException("NullDecorator only supports implementations that return values");

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

#if !FEAT_IKVM
        public override object Read(object value, ProtoReader source)
        {
            return Tail.Read(value, source);
        }
        public override void Write(object value, ProtoWriter dest)
        {
            if(value != null)
            {
                Tail.Write(value, dest);
            }
        }
#endif
    }
}
#endif