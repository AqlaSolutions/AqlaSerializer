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
            _allowNullWireType = false;
            return true; // always subitem
        }

        readonly int _baseKey = -1;
        readonly IProtoSerializerWithWireType _tail;
        readonly LateReferenceSerializer _lateReferenceTail;
        readonly IProtoSerializerWithWireType _baseKeySerializer;

        IProtoSerializerWithWireType DelegationHandler => (_options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0 ? null : (_tail ?? _baseKeySerializer);

        readonly BclHelpers.NetObjectOptions _options;
        readonly BinaryDataFormat _dataFormatForDynamicBuiltins;

        bool _allowNullWireType;

        // no need for special handling of !Nullable.HasValue - when boxing they will be applied

        NetObjectValueDecorator(Type type, bool asReference, bool asLateReference, bool allowNullWireType, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _allowNullWireType = allowNullWireType;
            _options = BclHelpers.NetObjectOptions.UseConstructor;
            if (asReference)
            {
                _options |= BclHelpers.NetObjectOptions.AsReference;
                if (asLateReference)
                {
                    _options |= BclHelpers.NetObjectOptions.WriteAsLateReference;
                }
            }
            else if (asLateReference) throw new ArgumentException("Can't serialize as late reference when asReference = false", nameof(asReference));

            int baseKey = model.GetKey(type, false, true);
            int key = model.GetKey(type, false, false);
            if (!Helpers.IsValueType(type) && key >= 0 && baseKey >= 0 && ValueSerializerBuilder.CanTypeBeAsLateReferenceOnBuildStage(key, model, true))
                _lateReferenceTail = new LateReferenceSerializer(type, key, baseKey, model);
            else if (asLateReference) throw new ArgumentException("Can't use late reference with non-model or value type " + type.Name);

            ProtoTypeCode typeCode = Helpers.GetTypeCode(type);

            // mind that this is set not for AsReference only
            // because AsReference may be switched in another version
            if (typeCode == ProtoTypeCode.String || typeCode == ProtoTypeCode.Type || typeCode == ProtoTypeCode.Uri)
                _options |= BclHelpers.NetObjectOptions.LateSet;

            // if this type is nullable it's ok
            // we'll unwrap it
            // and for non emit it's already boxed as not nullable
            this.ExpectedType = type;
        }

        public NetObjectValueDecorator(IProtoSerializerWithWireType tail, bool returnNullable, bool asReference, bool asLateReference, bool allowNullWireType, RuntimeTypeModel model)
            : this(type: MakeReturnNullable(tail.ExpectedType, returnNullable, model), asReference: asReference, asLateReference: asLateReference, allowNullWireType: allowNullWireType, model: model)
        {
            _tail = tail;
            RequiresOldValue = _tail.RequiresOldValue || (_lateReferenceTail?.RequiresOldValue ?? false);
        }

        static Type MakeReturnNullable(Type type, bool make, TypeModel model)
        {
            if (!make || !Helpers.IsValueType(type) || Helpers.GetNullableUnderlyingType(type) != null) return type;
            return model.MapType(typeof(Nullable<>)).MakeGenericType(type);
        }

        /// <summary>
        /// Dynamic type
        /// </summary>
        public NetObjectValueDecorator(Type dynamicBase, bool asReference, BinaryDataFormat dataFormatForDynamicBuiltins, bool allowNullWireType, RuntimeTypeModel model)
            : this(type: dynamicBase, asReference: asReference, asLateReference: false, allowNullWireType: allowNullWireType, model: model)
        {
            _dataFormatForDynamicBuiltins = dataFormatForDynamicBuiltins;
            _options |= BclHelpers.NetObjectOptions.DynamicType;
            // for late reference with dynamic type we need to get base type key from concrete
            // bacause dynamic types work with concrete type keys
            // but late reference - with bases
            // may be support later...
        }

        public NetObjectValueDecorator(Type type, int baseKey, bool asReference, bool asLateReference, ISerializerProxy concreteSerializerProxy, bool allowNullWireType, RuntimeTypeModel model)
            : this(type: type, asReference: asReference, asLateReference: asLateReference, allowNullWireType: allowNullWireType, model: model)
        {
            if (baseKey < 0) throw new ArgumentOutOfRangeException(nameof(baseKey));
            _baseKey = baseKey;
            _baseKeySerializer = new ModelTypeSerializer(Helpers.GetNullableUnderlyingType(type) ?? type, baseKey, concreteSerializerProxy, model);
        }

        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            string description = _options.ToString();
            if (_allowNullWireType)
            {
                if (!string.IsNullOrEmpty(description)) description += ", ";
                description += "WithNullWireType";
            }
            var s = (_tail ?? _baseKeySerializer);
            if (s != null)
            {
                using (builder.SingleTailDecorator(this, description))
                    s.WriteDebugSchema(builder);
            }
            else builder.SingleValueSerializer(this, description);
        }

        public Type ExpectedType { get; }

        public bool RequiresOldValue { get; } = true;

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            var type = ExpectedType;
            if (source.WireType == WireType.Null) return Helpers.IsValueType(type) ? Activator.CreateInstance(type) : null;

            if (!RequiresOldValue) value = null;

            int typeKey = _baseKey;
            BclHelpers.NetObjectOptions options = _options;
            var r = NetObjectHelpers.ReadNetObject_Start(
                ref value,
                source,
                ref type,
                options,
                ref typeKey,
                true);

            object oldValue = value;
            if (r.ShouldRead)
            {
                if (typeKey >= 0)
                {
                    // can be only for builtins
                    options &= ~BclHelpers.NetObjectOptions.LateSet;

                    if (typeKey == _baseKey && _baseKeySerializer != null)
                    {
                        if (r.IsLateReference)
                        {
                            if (_lateReferenceTail == null) throw new ProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                            value = _lateReferenceTail.Read(value, source);
                        }
                        else
                            value = _baseKeySerializer.Read(value, source);
                    }
                    else
                    {
                        Debug.Assert(r.IsDynamic);
                        value = ProtoReader.ReadObject(value, typeKey, source);
                    }
                }
                else
                {
                    if (r.IsDynamic)
                    {
                        if (source.TryReadBuiltinType(ref value, Helpers.GetTypeCode(type), true))
                            options |= BclHelpers.NetObjectOptions.LateSet;
                        else
                            throw new ProtoException("Dynamic type is not a contract-type: " + type.Name);
                    }
                    else
                    {
                        if (r.IsLateReference)
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
            }
            NetObjectHelpers.ReadNetObject_End(value, r, source, oldValue, type, options);
            if (!r.ShouldRead)
            {
                if (Helpers.IsValueType(ExpectedType) && value == null)
                    value = Activator.CreateInstance(ExpectedType);
            }
            return value;
        }

        public void Write(object value, ProtoWriter dest)
        {
            if (_allowNullWireType && value == null)
            {
                ProtoWriter.WriteFieldHeaderComplete(WireType.Null, dest);
                return;
            }

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
                        Debug.Assert(_baseKey >= 0);

                        if (_baseKeySerializer != null)
                            _baseKeySerializer.Write(value, dest);
                        else
                            ProtoWriter.WriteRecursionSafeObject(value, _baseKey, dest);
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

            Type nullableUnderlying = Helpers.GetNullableUnderlyingType(ExpectedType);
            using (ctx.StartDebugBlockAuto(this))
            using (Local nullableValue = RequiresOldValue ? ctx.GetLocalWithValueForEmitRead(this, valueFrom) : ctx.Local(ExpectedType))
            {
                g.If(g.ReaderFunc.WireType() == WireType.Null);
                {
                    if (Helpers.IsValueType(ExpectedType))
                        g.InitObj(nullableValue);
                    else
                        g.Assign(nullableValue, null);
                }
                g.Else();

                using (Local innerValue = nullableUnderlying == null ? nullableValue.AsCopy() : ctx.Local(nullableUnderlying))
                using (Local readReturnValue = ctx.Local(typeof(NetObjectHelpers.ReadReturnValue)))
                using (Local typeKey = ctx.Local(typeof(int)))
                using (Local type = ctx.Local(typeof(System.Type)))
                using (Local options = ctx.Local(typeof(BclHelpers.NetObjectOptions)))
                using (Local oldValueBoxed = ctx.Local(typeof(object)))
                using (Local inputValueBoxed = ctx.Local(typeof(object)))
                {
                    var shouldRead = readReturnValue.AsOperand.Field(nameof(NetObjectHelpers.ReadReturnValue.ShouldRead)).SetNotLeaked();
                    var isLateReference = readReturnValue.AsOperand.Field(nameof(NetObjectHelpers.ReadReturnValue.IsLateReference)).SetNotLeaked();
                    var isDynamic = readReturnValue.AsOperand.Field(nameof(NetObjectHelpers.ReadReturnValue.IsDynamic)).SetNotLeaked();
                    
                    g.Assign(options, _options);
                    if (!RequiresOldValue)
                        g.Assign(inputValueBoxed, null);
                    else
                        g.Assign(inputValueBoxed, nullableValue); // box

                    g.Assign(typeKey, ctx.MapMetaKeyToCompiledKey(_baseKey));
                    g.Assign(type, ExpectedType);
                    g.Assign(
                        readReturnValue,
                        s.Invoke(
                            typeof(NetObjectHelpers),
                            nameof(NetObjectHelpers.ReadNetObject_Start),
                            inputValueBoxed,
                            g.ArgReaderWriter(),
                            type,
                            options,
                            typeKey,
                            true));

                    g.Assign(oldValueBoxed, inputValueBoxed);

                    g.If(shouldRead);
                    {
                        using (ctx.StartDebugBlockAuto(this, "ShouldEnd=True"))
                        {
                            // now valueBoxed is not null otherwise it would go to else

                            // unwrap nullable
                            if (!innerValue.IsSame(nullableValue))
                            {
                                // old value may be null
                                g.If(nullableValue.AsOperand != null);
                                {
                                    g.Assign(innerValue, nullableValue.AsOperand.Property("Value"));
                                }
                                g.Else();
                                {
                                    g.InitObj(innerValue);
                                }
                                g.End();
                            }


                            g.If(typeKey.AsOperand >= 0);
                            {
                                g.ctx.MarkDebug("typeKey >= 0");
                                var optionsWithoutLateSet = _options & ~BclHelpers.NetObjectOptions.LateSet;
                                if (optionsWithoutLateSet != _options)
                                    g.Assign(options, optionsWithoutLateSet);
                                if (_baseKeySerializer != null)
                                {
                                    g.If(typeKey.AsOperand == ctx.MapMetaKeyToCompiledKey(_baseKey));
                                    {
                                        g.If(isLateReference);
                                        {
                                            if (_lateReferenceTail == null)
                                                g.ThrowProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                                            else
                                                EmitReadTail(g, ctx, _lateReferenceTail, innerValue, inputValueBoxed);
                                        }
                                        g.Else();
                                        {
                                            EmitReadTail(g, ctx, _baseKeySerializer, innerValue, inputValueBoxed);
                                        }
                                        g.End();
                                    }
                                    g.Else();
                                    {
                                        g.Assign(inputValueBoxed, g.ReaderFunc.ReadObject(inputValueBoxed, typeKey));
                                        g.Assign(innerValue, inputValueBoxed.AsOperand.Cast(nullableUnderlying ?? ExpectedType));
                                    }
                                    g.End();
                                }
                                else
                                {
                                    g.Assign(inputValueBoxed, g.ReaderFunc.ReadObject(inputValueBoxed, typeKey));
                                    g.Assign(innerValue, inputValueBoxed.AsOperand.Cast(nullableUnderlying ?? ExpectedType));
                                }
                            }
                            g.Else();
                            {
                                g.ctx.MarkDebug("typeKey < 0");
                                g.If(isDynamic);
                                {
                                    g.ctx.MarkDebug("dynamic");
                                    g.If(g.ReaderFunc.TryReadBuiltinType_bool(inputValueBoxed, g.HelpersFunc.GetTypeCode(type), true));
                                    {
                                        g.Assign(options, _options | BclHelpers.NetObjectOptions.LateSet);
                                        g.Assign(innerValue, inputValueBoxed.AsOperand.Cast(nullableUnderlying ?? ExpectedType));
                                    }
                                    g.Else();
                                    {
                                        g.ThrowProtoException("Dynamic type is not a contract-type: " + type.AsOperand.Property(nameof(Type.Name)));
                                    }
                                    g.End();
                                }
                                g.Else();
                                {
                                    g.ctx.MarkDebug("nondynamic");
                                    g.If(isLateReference);
                                    {
                                        if (_lateReferenceTail == null)
                                            g.ThrowProtoException("Late reference can't be deserialized for type " + ExpectedType.Name);
                                        else
                                            EmitReadTail(g, ctx, _lateReferenceTail, innerValue, inputValueBoxed);
                                    }
                                    g.Else();
                                    {
                                        if (_tail == null)
                                            g.ThrowProtoException("Dynamic type expected but no type info was read");
                                        else
                                        {
                                            EmitReadTail(g, ctx, _tail, innerValue, inputValueBoxed);
                                        }
                                    }
                                    g.End();
                                }
                                g.End();
                            }
                            g.End();

                            if (!innerValue.IsSame(nullableValue))
                                g.Assign(nullableValue, innerValue); // nullable~T it back
                        }
                    }
                    g.End();

                    g.Invoke(
                        typeof(NetObjectHelpers),
                        nameof(NetObjectHelpers.ReadNetObject_End),
                        inputValueBoxed,
                        readReturnValue,
                        g.ArgReaderWriter(),
                        oldValueBoxed,
                        type,
                        options);

                    g.If(!shouldRead);
                    {
                        if (Helpers.IsValueType(ExpectedType) && (EmitReadReturnsValue || RequiresOldValue))
                        {
                            g.If(inputValueBoxed.AsOperand == null);
                            {
                                // also nullable can just unbox from null or value
                                // but anyway
                                g.InitObj(nullableValue);
                            }
                            g.Else();
                            {
                                g.Assign(nullableValue, inputValueBoxed.AsOperand.Cast(ExpectedType));
                            }
                            g.End();
                        }
                        else
                            g.Assign(nullableValue, inputValueBoxed.AsOperand.Cast(ExpectedType));
                    }
                    g.End();
                }
                
                g.End(); // null check

                if (EmitReadReturnsValue)
                    ctx.LoadValue(nullableValue);
            }
        }

        void EmitReadTail(SerializerCodeGen g, CompilerContext ctx, IProtoSerializerWithWireType ser, Local value, Local outValueBoxed)
        {
            // inputValue may be nullable
            using (ctx.StartDebugBlockAuto(this))
            {
                ser.EmitRead(ctx, ser.RequiresOldValue ? value : null);
                if (ser.EmitReadReturnsValue)
                    g.Assign(value, g.GetStackValueOperand(ser.ExpectedType));
                
                g.Assign(outValueBoxed, value);
            }
        }

        public void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                Type nullableUnderlying = Helpers.GetNullableUnderlyingType(ExpectedType);
                bool canBeLateRef = (_options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0;
                using (Local inputValue = ctx.GetLocalWithValue(ExpectedType, valueFrom))
                using (Local value = nullableUnderlying == null ? inputValue.AsCopy() : ctx.Local(nullableUnderlying))
                {
                    var g = ctx.G;
                    bool canBeNull = nullableUnderlying != null || !Helpers.IsValueType(ExpectedType);
                    bool allowNullWireType = _allowNullWireType;
                    if (canBeNull && allowNullWireType)
                    {
                        g.If(inputValue.AsOperand == null);
                        {
                            g.Writer.WriteFieldHeaderComplete(WireType.Null);
                        }
                        g.Else();
                    }

                    using (Local write = ctx.Local(typeof(bool)))
                    using (Local dynamicTypeKey = ctx.Local(typeof(int)))
                    using (Local optionsLocal = canBeLateRef ? ctx.Local(typeof(BclHelpers.NetObjectOptions)) : null)
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
                            s.Invoke(
                                ctx.MapType(typeof(NetObjectHelpers)),
                                nameof(NetObjectHelpers.WriteNetObject_Start),
                                inputValue,
                                g.ArgReaderWriter(),
                                options,
                                dynamicTypeKey,
                                write));
                        g.If(write);
                        {
                            using (ctx.StartDebugBlockAuto(this, "Write=True"))
                            {
                                if (!value.IsSame(inputValue))
                                    g.Assign(value, inputValue.AsOperand.Property("Value")); // write = true so not null
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
                                else
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
                                    else if (_baseKeySerializer != null)
                                    {
                                        Debug.Assert(_baseKey >= 0);
                                        _baseKeySerializer.EmitWrite(ctx, value);
                                    }
                                    else
                                    {
                                        Debug.Assert(_baseKey >= 0);
                                        g.Writer.WriteRecursionSafeObject(value, ctx.MapMetaKeyToCompiledKey(_baseKey));
                                    }
                                    if (canBeLateRef)
                                    {
                                        g.End();
                                    }
                                }
                            }
                        }
                        g.End();
                        g.Writer.EndSubItem(token);
                    }

                    if (canBeNull && allowNullWireType)
                        g.End();
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