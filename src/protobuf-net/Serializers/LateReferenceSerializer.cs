#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AltLinq; using System.Linq;
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
    /// Should be used only inside NetObjectValueDecorator with AsReference
    /// </summary>
    sealed class LateReferenceSerializer : IProtoSerializerWithWireType
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this))
            {
                var b = builder.Contract(_model[_baseTypeKey].Serializer.ExpectedType);
                if (b != null)
                    _model[_baseTypeKey].Serializer.WriteDebugSchema(b);
            }
        }

        public bool DemandWireTypeStabilityStatus() => false;
        readonly RuntimeTypeModel _model;
        readonly int _typeKey;
        readonly int _baseTypeKey;
        readonly SubTypeHelpers _subTypeHelpers = new SubTypeHelpers();
        public Type ExpectedType { get; }
        
        public LateReferenceSerializer(Type type, int typeKey, int baseTypeKey, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (model == null) throw new ArgumentNullException(nameof(model));
            ExpectedType = type;
            _model = model;
            if (type.IsValueType)
                throw new ArgumentException("Can't create " + this.GetType().Name + " for non-reference type " + type.Name + "!");
            _typeKey = typeKey;
            _baseTypeKey = baseTypeKey;
        }

#if !FEAT_IKVM
        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
#if DEBUG
            Debug.Assert(value != null);
#endif
            _subTypeHelpers.Write(_model[_typeKey], value.GetType(), dest);
            ProtoWriter.NoteLateReference(_baseTypeKey, value, dest);
        }

        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            // TODO what may happen if old value is already existing reference? do we need to consider it?
            var v = _subTypeHelpers.TryRead(_model[_typeKey], value?.GetType(), source);
            if (v != null)
                value = v.Serializer.CreateInstance(source);
            else if (value != null)
                ProtoReader.NoteObject(value, source);
            else throw new ProtoException(CantCreateInstanceMessage);
            // each CreateInstance notes object
            ProtoReader.NoteLateReference(_baseTypeKey, value, source);
            return value;
        }

#endif

        string CantCreateInstanceMessage
            => "Can't create an instance for late reference of type " + ExpectedType.Name + ". " + NotSupportedMessage;

        public const string NotSupportedMessage = "Late references are not supported on surrogate serializers and tuples.";

        public bool RequiresOldValue => true;
        
        public bool CanCancelWriting { get; }

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => true;

        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                using (var value = ctx.GetLocalWithValue(ExpectedType, valueFrom))
                {
                    _subTypeHelpers.EmitWrite(ctx.G, _model[_typeKey], value);
                    ctx.G.Writer.NoteLateReference(ctx.MapMetaKeyToCompiledKey(_baseTypeKey), value);
                }
            }
        }

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                using (var value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                {
                    _subTypeHelpers.EmitTryRead(
                        g,
                        value,
                        _model[_typeKey],
                        r =>
                            {
                                using (ctx.StartDebugBlockAuto(this, "returnGen"))
                                {
                                    if (r == null)
                                    {
                                        g.If(value.AsOperand == null);
                                        {
                                            g.ThrowProtoException(CantCreateInstanceMessage);
                                        }
                                        g.End();
                                        g.Reader.NoteObject(value);
                                    }
                                    else
                                    {
                                        r.Serializer.EmitCreateInstance(ctx);
                                        ctx.StoreValue(value);
                                    }
                                    g.Reader.NoteLateReference(ctx.MapMetaKeyToCompiledKey(_baseTypeKey), value);
                                }
                            });

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(value);
                }
            }
        }
#endif
        }
}

#endif