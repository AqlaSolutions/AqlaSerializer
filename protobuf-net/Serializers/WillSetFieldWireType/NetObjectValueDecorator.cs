// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
using TriAxis.RunSharp;
#endif
using System.Diagnostics;
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
    sealed class NetObjectValueDecorator : IProtoTypeSerializer // type here is just for wrapping
    {
        public bool DemandWireTypeStabilityStatus()
        {
            return true; // always subitem
        }

        readonly int _key = -1;
        readonly IProtoSerializerWithWireType _tail;
        readonly LateReferenceSerializer _lateReferenceTail;
        readonly IProtoSerializerWithWireType _keySerializer;

        IProtoSerializerWithWireType DelegationHandler => (_options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0 ? null : (_tail ?? _keySerializer);

        readonly Type _type;

        readonly BclHelpers.NetObjectOptions _options;
        readonly BinaryDataFormat _dataFormatForDynamicBuiltins;

        // no need for special handling of !Nullable.HasValue - when boxing they will be applied

        NetObjectValueDecorator(Type type, bool asReference, bool asLateReference, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _options = BclHelpers.NetObjectOptions.UseConstructor;
            if (asReference)
            {
                _options |= BclHelpers.NetObjectOptions.AsReference;
                if (asLateReference)
                {
                    _options |= BclHelpers.NetObjectOptions.WriteAsLateReference;
                }
            }
            else if (asLateReference) throw new ArgumentException("Can't serialize as late reference when asReference = false", "asReference");

            int key = model.GetKey(type, false, true);
            if (!Helpers.IsValueType(type) && key >= 0)
                _lateReferenceTail = new LateReferenceSerializer(type, key, model);
            else if (asLateReference) throw new ArgumentException("Can't use late reference with non-model or value type " + type.Name);

            ProtoTypeCode typeCode = Helpers.GetTypeCode(type);

            // mind that this is set not for AsReference only
            // because AsReference may be switched in another version
            if (typeCode == ProtoTypeCode.String || typeCode == ProtoTypeCode.Type || typeCode == ProtoTypeCode.Uri)
                _options |= BclHelpers.NetObjectOptions.LateSet;

            // if this type is nullable it's ok
            // we'll unwrap it
            // and for non emit it's already boxed as not nullable
            this._type = type;
        }

        public NetObjectValueDecorator(IProtoSerializerWithWireType tail, bool returnNullable, bool asReference, bool asLateReference, RuntimeTypeModel model)
            : this(type: MakeReturnNullable(tail.ExpectedType, returnNullable, model), asReference: asReference, asLateReference: asLateReference, model: model)
        {
            _tail = tail;
            RequiresOldValue = _tail.RequiresOldValue;
        }

        static Type MakeReturnNullable(Type type, bool make, TypeModel model)
        {
            if (!make || !Helpers.IsValueType(type) || Helpers.GetNullableUnderlyingType(type) != null) return type;
            return model.MapType(typeof(Nullable<>)).MakeGenericType(type);
        }

        /// <summary>
        /// Dynamic type
        /// </summary>
        public NetObjectValueDecorator(bool asReference, BinaryDataFormat dataFormatForDynamicBuiltins, RuntimeTypeModel model)
            : this(type: model.MapType(typeof(object)), asReference: asReference, asLateReference: false, model: model)
        {
            _dataFormatForDynamicBuiltins = dataFormatForDynamicBuiltins;
            _options |= BclHelpers.NetObjectOptions.DynamicType;
            // for late reference with dynamic type we need to get base type key from concrete
            // bacause dynamic types work with concrete type keys
            // but late reference - with bases
            // may be support later...
        }

        public NetObjectValueDecorator(Type type, int key, bool asReference, bool asLateReference, ISerializerProxy serializerProxy, RuntimeTypeModel model)
            : this(type: type, asReference: asReference, asLateReference: asLateReference, model: model)
        {
            if (key < 0) throw new ArgumentOutOfRangeException(nameof(key));
            _key = key;
            _keySerializer = new ModelTypeSerializer(type, key, serializerProxy);
        }

        public Type ExpectedType => _type;
        public bool RequiresOldValue { get; } = true;

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            if (!RequiresOldValue) value = null;
            bool shouldEnd;
            int newTypeRefKey;
            int newObjectKey;
            int typeKey = _key;
            var type = _type;
            bool isDynamic;
            bool isLateReference;
            BclHelpers.NetObjectOptions options = _options;
            SubItemToken token = NetObjectHelpers.ReadNetObject_Start(
                ref value,
                source,
                ref type,
                options,
                out isDynamic,
                out isLateReference,
                ref typeKey,
                out newObjectKey,
                out newTypeRefKey,
                out shouldEnd);
            if (shouldEnd)
            {
                object oldValue = value;
                if (typeKey >= 0)
                {
                    // can be only for builtins
                    options &= ~BclHelpers.NetObjectOptions.LateSet;

                    if (typeKey == _key && _keySerializer != null)
                    {
                        if (isLateReference)
                        {
                            if (_lateReferenceTail == null) throw new ProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                            value = _lateReferenceTail.Read(value, source);
                        }
                        else
                            value = _keySerializer.Read(value, source);
                    }
                    else
                    {
                        Debug.Assert(isDynamic);
                        value = ProtoReader.ReadObject(value, typeKey, source);
                    }
                }
                else
                {
                    if (isDynamic)
                    {
                        if (source.TryReadBuiltinType(ref value, Helpers.GetTypeCode(type), true))
                            options |= BclHelpers.NetObjectOptions.LateSet;
                        else
                            throw new ProtoException("Dynamic type is not a contract-type: " + type.Name);
                    }
                    else
                    {
                        if (isLateReference)
                        {
                            if (_lateReferenceTail == null) throw new ProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                            value = _lateReferenceTail.Read(value, source);
                        }
                        else
                        {
                            if (_tail == null)
                                throw new ProtoException("Dynamic type expected but no type info was read");
                            else
                            {
                                // ensure consistent behavior with emit version
                                value = _tail.Read(_tail.RequiresOldValue ? value : null, source);
                            }
                        }
                    }
                }
                NetObjectHelpers.ReadNetObject_EndWithNoteNewObject(value, source, oldValue, type, newObjectKey, newTypeRefKey, options, token);
            }
            else
            {
                if (Helpers.IsValueType(_type) && value == null)
                    value = Activator.CreateInstance(_type);
                ProtoReader.EndSubItem(token, source);
            }
            return value;
        }

        public void Write(object value, ProtoWriter dest)
        {
            bool write;
            int dynamicTypeKey;

            var options = _options;

            if ((options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0 && !ProtoWriter.CheckIsOnHalfToRecursionDepthLimit(dest))
                options &= ~BclHelpers.NetObjectOptions.WriteAsLateReference;

            SubItemToken token = NetObjectHelpers.WriteNetObject_Start(value, dest, options, out dynamicTypeKey, out write);

            if (write)
            {
                // field header written!
                if ((options & BclHelpers.NetObjectOptions.DynamicType) != 0)
                {
                    if (dynamicTypeKey < 0)
                    {
                        ProtoTypeCode typeCode = HelpersInternal.GetTypeCode(value.GetType());
                        WireType wireType = HelpersInternal.GetWireType(typeCode, _dataFormatForDynamicBuiltins);
                        if (wireType != WireType.None)
                        {
                            ProtoWriter.WriteFieldHeaderComplete(wireType, dest);
                            if (!ProtoWriter.TryWriteBuiltinTypeValue(value, typeCode, true, dest))
                                throw new ProtoException("Dynamic type is not a contract-type: " + value.GetType().Name);
                        }
                        else
                            throw new ProtoException("Dynamic type is not a contract-type: " + value.GetType().Name);
                    }
                    else ProtoWriter.WriteRecursionSafeObject(value, dynamicTypeKey, dest);
                }
                else
                {
                    if ((options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0)
                        _lateReferenceTail.Write(value, dest);
                    else if (_tail != null)
                        _tail.Write(value, dest);
                    else
                    {
                        Debug.Assert(_key >= 0);

                        if (_keySerializer != null)
                            _keySerializer.Write(value, dest);
                        else
                            ProtoWriter.WriteRecursionSafeObject(value, _key, dest);
                    }
                }
            }
            ProtoWriter.EndSubItem(token, dest);
        }

#endif

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => true;
        public void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            var g = ctx.G;
            var s = g.StaticFactory;

            //bool shouldUnwrapNullable = _serializer != null && ExpectedType != _serializer.ExpectedType && Helpers.GetNullableUnderlyingType(ExpectedType) == _serializer.ExpectedType;

            using (Local value = RequiresOldValue ? ctx.GetLocalWithValueForEmitRead(this, valueFrom) : ctx.Local(_type))
            using (Local shouldEnd = ctx.Local(typeof(bool)))
            using (Local isLateReference = ctx.Local(typeof(bool)))
            using (Local newTypeRefKey = ctx.Local(typeof(int)))
            using (Local typeKey = ctx.Local(typeof(int)))
            using (Local type = ctx.Local(typeof(System.Type)))
            using (Local newObjectKey = ctx.Local(typeof(int)))
            using (Local isDynamic = ctx.Local(typeof(bool)))
            using (Local options = ctx.Local(typeof(BclHelpers.NetObjectOptions)))
            using (Local token = ctx.Local(typeof(SubItemToken)))
            using (Local oldValueBoxed = ctx.Local(typeof(object)))
            using (Local valueBoxed = ctx.Local(typeof(object)))
            {
                g.Assign(options, _options);
                if (!RequiresOldValue)
                    g.Assign(valueBoxed, null);
                else
                    g.Assign(valueBoxed, value); // box

                g.Assign(typeKey, ctx.MapMetaKeyToCompiledKey(_key));
                g.Assign(type, _type);
                g.Assign(
                    token,
                    s.Invoke(
                        typeof(NetObjectHelpers),
                        nameof(NetObjectHelpers.ReadNetObject_Start),
                        valueBoxed,
                        g.ArgReaderWriter(),
                        type,
                        options,
                        isDynamic,
                        isLateReference,
                        typeKey,
                        newObjectKey,
                        newTypeRefKey,
                        shouldEnd));

                g.If(shouldEnd);
                {
                    g.Assign(oldValueBoxed, valueBoxed);

                    // now valueBoxed is not null otherwise it would go to else

                    g.If(typeKey.AsOperand >= 0);
                    {
                        var optionsWithoutLateSet = _options & ~BclHelpers.NetObjectOptions.LateSet;
                        if (optionsWithoutLateSet != _options)
                            g.Assign(options, optionsWithoutLateSet);
                        if (_keySerializer != null)
                        {
                            g.If(typeKey.AsOperand == ctx.MapMetaKeyToCompiledKey(_key));
                            {
                                g.If(isLateReference);
                                {
                                    if (_lateReferenceTail == null)
                                        g.ThrowProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                                    else
                                        EmitReadTail(g, ctx, _lateReferenceTail, value, valueBoxed);
                                }
                                g.Else();
                                {
                                    EmitReadTail(g, ctx, _keySerializer, value, valueBoxed);
                                }
                                g.End();
                            }
                            g.Else();
                            {
                                g.Assign(valueBoxed, g.ReaderFunc.ReadObject(valueBoxed, typeKey));
                                g.Assign(value, valueBoxed.AsOperand.Cast(_type));
                            }
                            g.End();
                        }
                        else
                        {
                            g.Assign(valueBoxed, g.ReaderFunc.ReadObject(valueBoxed, typeKey));
                            g.Assign(value, valueBoxed.AsOperand.Cast(_type));
                        }
                    }
                    g.Else();
                    {
                        g.If(isDynamic);
                        {
                            g.If(g.ReaderFunc.TryReadBuiltinType_bool(valueBoxed, g.HelpersFunc.GetTypeCode(type), true));
                            {
                                g.Assign(options, _options | BclHelpers.NetObjectOptions.LateSet);
                                g.Assign(value, valueBoxed.AsOperand.Cast(_type));
                            }
                            g.Else();
                            {
                                g.ThrowProtoException("Dynamic type is not a contract-type: " + type.AsOperand.Property(nameof(Type.Name)));
                            }
                            g.End();
                        }
                        g.Else();
                        {
                            g.If(isLateReference);
                            {
                                if (_lateReferenceTail == null)
                                    g.ThrowProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                                else
                                    EmitReadTail(g, ctx, _lateReferenceTail, value, valueBoxed);
                            }
                            g.Else();
                            {
                                if (_tail == null)
                                    g.ThrowProtoException("Dynamic type expected but no type info was read");
                                else
                                {
                                    EmitReadTail(g, ctx, _tail, value, valueBoxed);
                                }
                            }
                            g.End();
                        }
                        g.End();
                    }
                    g.End();

                    g.Invoke(
                        typeof(NetObjectHelpers),
                        nameof(NetObjectHelpers.ReadNetObject_EndWithNoteNewObject),
                        valueBoxed,
                        g.ArgReaderWriter(),
                        oldValueBoxed,
                        type,
                        newObjectKey,
                        newTypeRefKey,
                        options,
                        token);
                }
                g.Else();
                {
                    if (Helpers.IsValueType(_type) && (EmitReadReturnsValue || RequiresOldValue))
                    {
                        g.If(valueBoxed.AsOperand == null);
                        {
                            // also nullable can just unbox from null or value
                            // but anyway
                            g.InitObj(value);
                        }
                        g.Else();
                        {
                            g.Assign(value, valueBoxed.AsOperand.Cast(_type));
                        }
                        g.End();
                    }
                    else
                        g.Assign(value, valueBoxed.AsOperand.Cast(_type));

                    g.Reader.EndSubItem(token);
                }
                g.End();

                if (EmitReadReturnsValue)
                    ctx.LoadValue(value);
            }
        }

        static void EmitReadTail(SerializerCodeGen g, CompilerContext ctx, IProtoSerializerWithWireType ser, Local value, Local valueBoxed)
        {
            ser.EmitRead(ctx, ser.RequiresOldValue ? value : null);
            if (ser.EmitReadReturnsValue)
                g.Assign(value, g.GetStackValueOperand(ser.ExpectedType));
            g.Assign(valueBoxed, value);
        }

        public void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            bool canBeLateRef = (_options & BclHelpers.NetObjectOptions.WriteAsLateReference) == 0;
            using (Local value = ctx.GetLocalWithValue(_type, valueFrom))
            {
                var g = ctx.G;
                using (Local write = ctx.Local(typeof(bool)))
                using (Local dynamicTypeKey = ctx.Local(typeof(int)))
                using (Local optionsLocal = canBeLateRef ? ctx.Local(typeof(BclHelpers.NetObjectOptions)):null)
                using (Local token = ctx.Local(typeof(SubItemToken)))
                {
                    var s = g.StaticFactory;
                    // nullables: if null - will be boxed as null, if value - will be boxed as value, so don't worry about it

                    Operand options;
                    if (canBeLateRef)
                    {
                        g.If(!g.WriterFunc.CheckIsOnHalfToRecursionDepthLimit_bool());
                        {
                            g.Assign(optionsLocal, _options & ~BclHelpers.NetObjectOptions.WriteAsLateReference);
                        }
                        g.Else();
                        {
                            g.Assign(optionsLocal, _options);
                        }
                        g.End();
                        options = optionsLocal;
                    }
                    else options = _options;

                    g.Assign(
                        token,
                        s.Invoke(ctx.MapType(typeof(NetObjectHelpers)), nameof(NetObjectHelpers.WriteNetObject_Start), value, g.ArgReaderWriter(), options, dynamicTypeKey, write));
                    g.If(write);
                    {
                        // field header written!
                        if ((_options & BclHelpers.NetObjectOptions.DynamicType) != 0)
                        {
                            g.If(dynamicTypeKey.AsOperand < 0);
                            {
                                using (Local typeCode = ctx.Local(typeof(ProtoTypeCode)))
                                using (Local wireType = ctx.Local(typeof(WireType)))
                                {
                                    g.Assign(typeCode, g.HelpersFunc.GetTypeCode(value.AsOperand.InvokeGetType()));
                                    g.Assign(wireType, g.HelpersFunc.GetWireType(typeCode, _dataFormatForDynamicBuiltins));
                                    g.If(wireType.AsOperand != WireType.None);
                                    {
                                        g.Writer.WriteFieldHeaderComplete(wireType);
                                        g.If(!g.WriterFunc.TryWriteBuiltinTypeValue_bool(value, typeCode, true));
                                        {
                                            g.ThrowProtoException("Dynamic type is not a contract-type: " + value.AsOperand.InvokeGetType().Property("Name"));
                                        }
                                        g.End();
                                    }
                                    g.Else();
                                    {
                                        g.ThrowProtoException("Dynamic type is not a contract-type: " + value.AsOperand.InvokeGetType().Property("Name"));
                                    }
                                    g.End();
                                }
                            }
                            g.Else();
                            {
                                g.Writer.WriteRecursionSafeObject(value, dynamicTypeKey);
                            }
                            g.End();
                        }
                        g.Else();
                        {
                            if (canBeLateRef)
                            {
                                g.If(optionsLocal.AsOperand == _options); // if not changed (now only one place) - write as ref
                                {
                                    _lateReferenceTail.EmitWrite(ctx, value);
                                }
                                g.Else();
                            }
                            if (_tail != null)
                                _tail.EmitWrite(ctx, value);
                            else if (_keySerializer != null)
                            {
                                Debug.Assert(_key >= 0);
                                _keySerializer.EmitWrite(ctx, value);
                            }
                            else
                            {
                                Debug.Assert(_key >= 0);
                                g.Writer.WriteRecursionSafeObject(value, ctx.MapMetaKeyToCompiledKey(_key));
                            }
                            if (canBeLateRef)
                            {
                                g.End();
                            }
                        }
                        g.End();
                    }
                    g.End();
                    g.Writer.EndSubItem(token);
                }
            }
        }
#endif

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            IProtoTypeSerializer pts = DelegationHandler as IProtoTypeSerializer;
            return pts != null && pts.HasCallbacks(callbackType);
        }

        public bool CanCreateInstance()
        {
            IProtoTypeSerializer pts = DelegationHandler as IProtoTypeSerializer;
            return pts != null && pts.CanCreateInstance();
        }

#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)DelegationHandler).CreateInstance(source);
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            IProtoTypeSerializer pts = DelegationHandler as IProtoTypeSerializer;
            if (pts != null) pts.Callback(value, callbackType, context);
        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            // we only expect this to be invoked if HasCallbacks returned true, so implicitly _serializer
            // **must** be of the correct type
            ((IProtoTypeSerializer)DelegationHandler).EmitCallback(ctx, valueFrom, callbackType);
        }

        public void EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)DelegationHandler).EmitCreateInstance(ctx);
        }
#endif
    }
}

#endif