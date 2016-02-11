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
    sealed class DefaultValueDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public bool DemandWireTypeStabilityStatus() => false;
        public override Type ExpectedType { get { return Tail.ExpectedType; } }
        public override bool RequiresOldValue { get { return Tail.RequiresOldValue; } }
        private readonly object defaultValue;
        public DefaultValueDecorator(TypeModel model, object defaultValue, IProtoSerializerWithWireType tail) : base(tail)
        {
            if (defaultValue == null) throw new ArgumentNullException("defaultValue");
            Type type = model.MapType(defaultValue.GetType());
            // if the value is nullable we should check equality with nullable before writing
            var underlying = Helpers.GetNullableUnderlyingType(tail.ExpectedType);
            if (underlying != null)
            {
                type = model.MapType(typeof(Nullable<>)).MakeGenericType(type);
            }
            if (type != tail.ExpectedType
#if FEAT_IKVM // in IKVM, we'll have the default value as an underlying type
                && !(tail.ExpectedType.IsEnum && type == tail.ExpectedType.GetEnumUnderlyingType())
#endif
                )
            {
                throw new ArgumentException(string.Format("Default value is of incorrect type (expected {0}, actaul {1})", tail.ExpectedType, type), "defaultValue");
            }
            this.defaultValue = defaultValue;
        }
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            if (!object.Equals(value, defaultValue))
            {
                Tail.Write(value, dest);
            }
            else 
                ProtoWriter.WriteFieldHeaderCancelBegin(dest);
        }
        public override object Read(object value, ProtoReader source)
        {
            return Tail.Read(value, source);
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue { get { return Tail.EmitReadReturnsValue; } }
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Compiler.CodeLabel done = ctx.DefineLabel();
            Compiler.CodeLabel onCancel = ctx.DefineLabel();
            if (valueFrom.IsNullRef())
            {
                ctx.CopyValue(); // on the stack
                Compiler.CodeLabel needToPop = ctx.DefineLabel();

                EmitBranchIfDefaultValue(ctx, needToPop);
                // if != defaultValue
                {
                    Tail.EmitWrite(ctx, null);
                    ctx.Branch(done, true);
                }
                // else
                {
                    ctx.MarkLabel(needToPop);
                    ctx.DiscardValue();
                    // onCancel
                }
            }
            else
            {
                ctx.LoadValue(valueFrom); // variable/parameter

                EmitBranchIfDefaultValue(ctx, onCancel);
                // if != defaultValue
                {
                    Tail.EmitWrite(ctx, valueFrom);
                    ctx.Branch(done, true);
                }
                // else
                // onCancel
            }
            ctx.MarkLabel(onCancel);
            ctx.LoadReaderWriter();
            ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod(nameof(ProtoWriter.WriteFieldHeaderCancelBegin)));
            ctx.MarkLabel(done);
        }
        private void EmitBeq(Compiler.CompilerContext ctx, Compiler.CodeLabel label, Type type)
        {
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.Boolean:
                case ProtoTypeCode.Byte:
                case ProtoTypeCode.Char:
                case ProtoTypeCode.Double:
                case ProtoTypeCode.Int16:
                case ProtoTypeCode.Int32:
                case ProtoTypeCode.Int64:
                case ProtoTypeCode.SByte:
                case ProtoTypeCode.Single:
                case ProtoTypeCode.UInt16:
                case ProtoTypeCode.UInt32:
                case ProtoTypeCode.UInt64:
                    ctx.BranchIfEqual(label, false);
                    break;
                default:
                    MethodInfo method = type.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static,
                        null, new Type[] { type, type}, null);
                    if (method == null || method.ReturnType != ctx.MapType(typeof(bool)))
                    {
                        throw new InvalidOperationException("No suitable equality operator found for default-values of type: " + type.FullName);
                    }
                    ctx.EmitCall(method);
                    ctx.BranchIfTrue(label, false);
                    break;

            }
        }

        private void EmitBranchIfDefaultValue(Compiler.CompilerContext ctx, Compiler.CodeLabel label)
        {
            var g = ctx.G;
            Type nullableUnderlying = Helpers.GetNullableUnderlyingType(ExpectedType);

            if (nullableUnderlying != null)
            {
                // we another for null check
                ctx.CopyValue();
                g.If(g.GetStackValueOperand(ExpectedType).Property("HasValue"));

                // unwrap value
                g.LeaveNextReturnOnStack();
                g.Eval(g.GetStackValueOperand(ExpectedType).Property("Value"));
            }

            EmitBranchIfDefaultValue_Switch(ctx, label);

            if (nullableUnderlying != null)
            {
                g.Else();
                {
                    ctx.DiscardValue();
                }
                g.End();
            }
        }

        private void EmitBranchIfDefaultValue_Switch(Compiler.CompilerContext ctx, Compiler.CodeLabel label)
        {
            Type expected = Helpers.GetNullableUnderlyingType(ExpectedType) ?? ExpectedType;
            switch (Helpers.GetTypeCode(expected))
            {
                case ProtoTypeCode.Boolean:
                    if ((bool)defaultValue)
                    {
                        ctx.BranchIfTrue(label, false);
                    }
                    else
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    break;
                case ProtoTypeCode.Byte:
                    if ((byte)defaultValue == (byte)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(byte)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.SByte:
                    if ((sbyte)defaultValue == (sbyte)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(sbyte)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.Int16:
                    if ((short)defaultValue == (short)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(short)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.UInt16:
                    if ((ushort)defaultValue == (ushort)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(ushort)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.Int32:
                    if ((int)defaultValue == (int)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.UInt32:
                    if ((uint)defaultValue == (uint)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(uint)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.Char:
                    if ((char)defaultValue == (char)0)
                    {
                        ctx.BranchIfFalse(label, false);
                    }
                    else
                    {
                        ctx.LoadValue((int)(char)defaultValue);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.Int64:
                    ctx.LoadValue((long)defaultValue);
                    EmitBeq(ctx, label, expected);
                    break;
                case ProtoTypeCode.UInt64:
                    ctx.LoadValue((long)(ulong)defaultValue);
                    EmitBeq(ctx, label, expected);
                    break;
                case ProtoTypeCode.Double:
                    ctx.LoadValue((double)defaultValue);
                    EmitBeq(ctx, label, expected);
                    break;
                case ProtoTypeCode.Single:
                    ctx.LoadValue((float)defaultValue);
                    EmitBeq(ctx, label, expected);
                    break;
                case ProtoTypeCode.String:
                    ctx.LoadValue((string)defaultValue);
                    EmitBeq(ctx, label, expected);
                    break;
                case ProtoTypeCode.Decimal:
                    {
                        decimal d = (decimal)defaultValue;
                        ctx.LoadValue(d);
                        EmitBeq(ctx, label, expected);
                    }
                    break;
                case ProtoTypeCode.TimeSpan:
                    {
                        TimeSpan ts = (TimeSpan)defaultValue;
                        if (ts == TimeSpan.Zero)
                        {
                            ctx.LoadValue(typeof(TimeSpan).GetField("Zero"));
                        }
                        else
                        {
                            ctx.LoadValue(ts.Ticks);
                            ctx.EmitCall(ctx.MapType(typeof(TimeSpan)).GetMethod("FromTicks"));
                        }
                        EmitBeq(ctx, label, expected);
                        break;
                    }
                case ProtoTypeCode.Guid:
                    {
                        ctx.LoadValue((Guid)defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    }
                case ProtoTypeCode.DateTime:
                    {
#if FX11 || SILVERLIGHT
                        ctx.LoadValue(((DateTime)defaultValue).ToFileTime());
                        ctx.EmitCall(typeof(DateTime).GetMethod("FromFileTime"));                      
#else
                        ctx.LoadValue(((DateTime)defaultValue).ToBinary());
                        ctx.EmitCall(ctx.MapType(typeof(DateTime)).GetMethod("FromBinary"));
#endif

                        EmitBeq(ctx, label, expected);
                        break;
                    }
                default:
                    throw new NotSupportedException("Type cannot be represented as a default value: " + expected.FullName);
            }
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Tail.EmitRead(ctx, valueFrom);
        }
#endif
    }
}
#endif