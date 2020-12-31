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
    sealed class SurrogateSerializer : IProtoTypeSerializer
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this))
                _rootTail.WriteDebugSchema(builder);
        }
        
        public bool DemandWireTypeStabilityStatus() => _rootTail.DemandWireTypeStabilityStatus();

        bool IProtoTypeSerializer.HasCallbacks(AqlaSerializer.Meta.TypeModel.CallbackType callbackType)
        {
            return false;
        }

#if FEAT_COMPILER
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, AqlaSerializer.Meta.TypeModel.CallbackType callbackType)
        {
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            throw new NotSupportedException();
        }
#endif

        bool IProtoTypeSerializer.CanCreateInstance()
        {
            return false;
        }

#if !FEAT_IKVM
        object IProtoTypeSerializer.CreateInstance(ProtoReader source)
        {
            throw new NotSupportedException();
        }

        void IProtoTypeSerializer.Callback(object value, AqlaSerializer.Meta.TypeModel.CallbackType callbackType, SerializationContext context)
        {
        }
#endif

        public bool RequiresOldValue => true;
        
        public bool CanCancelWriting => true;
        public Type ExpectedType { get; }
        private readonly Type _declaredType;
        private readonly MethodInfo _toTail, _fromTail;
        IProtoTypeSerializer _rootTail;

        public SurrogateSerializer(TypeModel model, Type forType, Type declaredType, IProtoTypeSerializer rootTail)
        {
            Helpers.DebugAssert(forType != null, "forType");
            Helpers.DebugAssert(declaredType != null, "declaredType");
            Helpers.DebugAssert(rootTail != null, "rootTail");
            Helpers.DebugAssert(rootTail.RequiresOldValue, "RequiresOldValue"); // old check, may work without!
#if FEAT_COMPILER
            Helpers.DebugAssert(!rootTail.EmitReadReturnsValue, "ReturnsValue"); // old check, may work without!
#endif
            Helpers.DebugAssert(declaredType == rootTail.ExpectedType || Helpers.IsSubclassOf(declaredType, rootTail.ExpectedType));
            this.ExpectedType = forType;
            this._declaredType = declaredType;
            this._rootTail = rootTail;
            _toTail = GetConversion(model, true);
            _fromTail = GetConversion(model, false);
        }

        private static bool HasCast(TypeModel model, Type type, Type from, Type to, out MethodInfo op)
        {
#if WINRT
            System.Collections.Generic.List<MethodInfo> list = new System.Collections.Generic.List<MethodInfo>();
            foreach (var item in type.GetRuntimeMethods())
            {
                if (item.IsStatic) list.Add(item);
            }
            MethodInfo[] found = list.ToArray();
#else
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] found = type.GetMethods(flags);
#endif
            ParameterInfo[] paramTypes;
            
            for (int i = 0; i < found.Length; i++)
            {
                MethodInfo m = found[i];
                if (m.ReturnType != to) continue;
                paramTypes = m.GetParameters();
                if (paramTypes.Length == 1 && paramTypes[0].ParameterType == from)
                {
                    if (AttributeMap.GetAttribute(AttributeMap.Create(model, m, false), "ProtoBuf.ProtoConverterAttribute") != null
                        || AttributeMap.GetAttribute(AttributeMap.Create(model, m, false), "AqlaSerializer.SurrogateConverterAttribute") != null)
                    {
                        op = m;
                        return true;
                    }
                }
            }

            for (int i = 0; i < found.Length; i++)
            {
                MethodInfo m = found[i];
                if ((m.Name != "op_Implicit" && m.Name != "op_Explicit") || m.ReturnType != to)
                {
                    continue;
                }
                paramTypes = m.GetParameters();
                if (paramTypes.Length == 1 && paramTypes[0].ParameterType == from)
                {
                    op = m;
                    return true;
                }
            }
            op = null;
            return false;
        }

        public MethodInfo GetConversion(TypeModel model, bool toTail)
        {
            Type to = toTail ? _declaredType : ExpectedType;
            Type from = toTail ? ExpectedType : _declaredType;
            MethodInfo op;
            if (HasCast(model, _declaredType, from, to, out op) || HasCast(model, ExpectedType, from, to, out op))
            {
                return op;
            }
            throw new InvalidOperationException(
                "No suitable conversion operator found for surrogate: " +
                ExpectedType.FullName + " / " + _declaredType.FullName);
        }

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter writer)
        {
            _rootTail.Write(_toTail.Invoke(null, new object[] { value }), writer);
        }

        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            int reservedTrap = -1;

            if (!ExpectedType.IsValueType)
                reservedTrap = ProtoReader.ReserveNoteObject(source);
            // convert the incoming value
            object[] args = { value };
            value = _toTail.Invoke(null, args); // don't note, references are not to surrogate but to the final object
            // invoke the tail and convert the outgoing value
            args[0] = _rootTail.Read(value, source);
            var r = _fromTail.Invoke(null, args);

            if (!ExpectedType.IsValueType)
                ProtoReader.NoteReservedTrappedObject(reservedTrap, r, source);
            
            return r;
        }
#endif

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => false;

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                using (Compiler.Local value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                using (Compiler.Local converted = new Compiler.Local(ctx, _declaredType)) // declare/re-use local
                using (Compiler.Local reservedTrap = new Compiler.Local(ctx, typeof(int)))
                {
                    var g = ctx.G;

                    if (!ExpectedType.IsValueType)
                        g.Assign(reservedTrap, g.ReaderFunc.ReserveNoteObject_int());

                    ctx.LoadValue(value); // load primary onto stack
                    ctx.EmitCall(_toTail); // static convert op, primary-to-surrogate

                    ctx.StoreValue(converted); // store into surrogate local

                    _rootTail.EmitRead(ctx, _rootTail.RequiresOldValue ? converted : null); // downstream processing against surrogate local

                    if (_rootTail.EmitReadReturnsValue)
                        ctx.StoreValue(converted);

                    ctx.LoadValue(converted); // load from surrogate local
                    ctx.EmitCall(_fromTail); // static convert op, surrogate-to-primary
                    ctx.StoreValue(value); // store back into primary

                    if (!ExpectedType.IsValueType)
                        g.Reader.NoteReservedTrappedObject(reservedTrap, value);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(value);
                }
            }
        }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.LoadValue(valueFrom);
                ctx.EmitCall(_toTail);
                _rootTail.EmitWrite(ctx, null);
            }
        }
#endif
    }
}

#endif