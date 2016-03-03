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
        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _defaultValue?.ToString()))
                Tail.WriteDebugSchema(builder);
        }

        public bool DemandWireTypeStabilityStatus() => false;
        public override Type ExpectedType => Tail.ExpectedType;
        public override bool RequiresOldValue => Tail.RequiresOldValue;
        private readonly object _defaultValue;
        public DefaultValueDecorator(TypeModel model, object defaultValue, IProtoSerializerWithWireType tail) : base(tail)
        {
            if (defaultValue == null) throw new ArgumentNullException(nameof(defaultValue));
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
                throw new ArgumentException(string.Format("Default value is of incorrect type (expected {0}, actaul {1})", tail.ExpectedType, type), nameof(defaultValue));
            }
            this._defaultValue = defaultValue;
        }
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            if (!object.Equals(value, _defaultValue))
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
        public override bool EmitReadReturnsValue => Tail.EmitReadReturnsValue;

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
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
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                Type nullableUnderlying = Helpers.GetNullableUnderlyingType(ExpectedType);

                if (nullableUnderlying != null)
                {
                    using (var loc = ctx.Local(ExpectedType))
                    {
                        // we another for null check
                        ctx.G.Assign(loc, g.GetStackValueOperand(ExpectedType));
                        g.If(loc.AsOperand.Property("HasValue"));

                        // unwrap value
                        g.LeaveNextReturnOnStack();
                        g.Eval(loc.AsOperand.Property("Value"));
                    }
                }

                EmitBranchIfDefaultValue_Switch(ctx, label);

                if (nullableUnderlying != null)
                {
                    g.End();
                }
            }
        }

        private void EmitBranchIfDefaultValue_Switch(Compiler.CompilerContext ctx, Compiler.CodeLabel label)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                Type expected = Helpers.GetNullableUnderlyingType(ExpectedType) ?? ExpectedType;
                switch (Helpers.GetTypeCode(expected))
                {
                    case ProtoTypeCode.Boolean:
                        if ((bool)_defaultValue)
                        {
                            ctx.BranchIfTrue(label, false);
                        }
                        else
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        break;
                    case ProtoTypeCode.Byte:
                        if ((byte)_defaultValue == (byte)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(byte)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.SByte:
                        if ((sbyte)_defaultValue == (sbyte)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(sbyte)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.Int16:
                        if ((short)_defaultValue == (short)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(short)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.UInt16:
                        if ((ushort)_defaultValue == (ushort)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(ushort)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.Int32:
                        if ((int)_defaultValue == (int)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.UInt32:
                        if ((uint)_defaultValue == (uint)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(uint)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.Char:
                        if ((char)_defaultValue == (char)0)
                        {
                            ctx.BranchIfFalse(label, false);
                        }
                        else
                        {
                            ctx.LoadValue((int)(char)_defaultValue);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.Int64:
                        ctx.LoadValue((long)_defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    case ProtoTypeCode.UInt64:
                        ctx.LoadValue((long)(ulong)_defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    case ProtoTypeCode.Double:
                        ctx.LoadValue((double)_defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    case ProtoTypeCode.Single:
                        ctx.LoadValue((float)_defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    case ProtoTypeCode.String:
                        ctx.LoadValue((string)_defaultValue);
                        EmitBeq(ctx, label, expected);
                        break;
                    case ProtoTypeCode.Decimal:
                        {
                            decimal d = (decimal)_defaultValue;
                            ctx.LoadValue(d);
                            EmitBeq(ctx, label, expected);
                        }
                        break;
                    case ProtoTypeCode.TimeSpan:
                        {
                            TimeSpan ts = (TimeSpan)_defaultValue;
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
                            ctx.LoadValue((Guid)_defaultValue);
                            EmitBeq(ctx, label, expected);
                            break;
                        }
                    case ProtoTypeCode.DateTime:
                        {
#if FX11 || SILVERLIGHT
                        ctx.LoadValue(((DateTime)defaultValue).ToFileTime());
                        ctx.EmitCall(typeof(DateTime).GetMethod("FromFileTime"));                      
#else
                            ctx.LoadValue(((DateTime)_defaultValue).ToBinary());
                            ctx.EmitCall(ctx.MapType(typeof(DateTime)).GetMethod("FromBinary"));
#endif

                            EmitBeq(ctx, label, expected);
                            break;
                        }
                    default:
                        throw new NotSupportedException("Type cannot be represented as a default value: " + expected.FullName);
                }
            }
        }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                Tail.EmitRead(ctx, valueFrom);
            }
        }
#endif
    }
}
#endif