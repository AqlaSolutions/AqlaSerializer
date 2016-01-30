#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AltLinq;
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
        public bool DemandWireTypeStabilityStatus() => false;
        readonly RuntimeTypeModel _model;
        readonly int _typeKey;
        readonly SubTypeHelpers _subTypeHelpers = new SubTypeHelpers();
        public Type ExpectedType { get; }

        public static NetObjectValueDecorator CreateInsideNetObject(Type type, RuntimeTypeModel model)
        {
            return new NetObjectValueDecorator(new LateReferenceSerializer(type, model), false, true, model);
        }

        LateReferenceSerializer(Type type, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (model == null) throw new ArgumentNullException(nameof(model));
            ExpectedType = type;
            _model = model;
            if (Helpers.IsValueType(type))
                throw new ArgumentException("Can't create " + this.GetType().Name + " for non-reference type " + type.Name + "!");
            _typeKey = _model.GetKey(type, true, true);
        }

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
#if DEBUG
            Debug.Assert(value != null);
#endif
            _subTypeHelpers.Write(_model[_typeKey], value.GetType(), dest);
            ProtoWriter.NoteLateReference(_typeKey, value, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            value = _subTypeHelpers.TryRead(_model[_typeKey], value?.GetType(), source)?.Serializer.CreateInstance(source) ?? value;
            if (value == null) throw new ProtoException(CantCreateInstanceMessage);
            // each CreateInstance notes object
            ProtoReader.NoteLateReference(_typeKey, value, source);
            return value;
        }

#endif

        string CantCreateInstanceMessage
            => "Can't create an instance for late reference of type " + ExpectedType.Name + "; late references are not supported on surrogate serializers and tuples.";
        bool IProtoSerializer.RequiresOldValue => true;
        bool IProtoSerializer.ReturnsValue => true;
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (var loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
                _subTypeHelpers.EmitWrite(ctx.G, _model[_typeKey], loc);
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (var loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            {
                _subTypeHelpers.EmitTryRead(
                    g,
                    loc,
                    _model[_typeKey],
                    r =>
                        {
                            if (r == null)
                            {
                                g.If(loc.AsOperand==null);
                                {
                                    g.ThrowProtoException(CantCreateInstanceMessage);
                                }
                                g.End();
                                ctx.LoadValue(loc);
                            }
                            else
                                r.Serializer.EmitCreateInstance(ctx);
                        });
            }
        }
#endif
    }
}

#endif