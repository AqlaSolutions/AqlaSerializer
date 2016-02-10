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
        readonly IProtoSerializerWithWireType _keySerializer;

        IProtoSerializerWithWireType DelegationHandler => _tail ?? _keySerializer;

        readonly Type _type;

        readonly BclHelpers.NetObjectOptions _options;
        readonly BinaryDataFormat _dataFormatForDynamicBuiltins;

        // no need for special handling of !Nullable.HasValue - when boxing they will be applied

        NetObjectValueDecorator(Type type, bool asReference)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _options = BclHelpers.NetObjectOptions.UseConstructor;
            if (asReference)
                _options |= BclHelpers.NetObjectOptions.AsReference;

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

        public NetObjectValueDecorator(IProtoSerializerWithWireType tail, bool returnNullable, bool asReference, TypeModel model)
            : this(type: MakeReturnNullable(tail.ExpectedType, returnNullable, model), asReference: asReference)
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
        /// <param name="asReference"></param>
        /// <param name="dataFormatForDynamicBuiltins"></param>
        public NetObjectValueDecorator(bool asReference, BinaryDataFormat dataFormatForDynamicBuiltins, TypeModel model)
            : this(type: model.MapType(typeof(object)), asReference: asReference)
        {
            _dataFormatForDynamicBuiltins = dataFormatForDynamicBuiltins;
            _options |= BclHelpers.NetObjectOptions.DynamicType;
        }

        public NetObjectValueDecorator(Type type, int key, bool asReference, ISerializerProxy serializerProxy)
            : this(type: type, asReference: asReference)
        {
            if (key < 0) throw new ArgumentOutOfRangeException(nameof(key));
            _key = key;
            _keySerializer = new ModelTypeSerializer(type, key, serializerProxy);
        }

        public Type ExpectedType => _type;
        public bool ReturnsValue => _tail?.ReturnsValue ?? true;
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
            bool isDynamicLateSet = false;
            BclHelpers.NetObjectOptions options = _options;
            SubItemToken token = NetObjectHelpers.ReadNetObject_Start(
                ref value,
                source,
                ref type,
                options,
                out isDynamic,
                ref typeKey,
                out newObjectKey,
                out newTypeRefKey,
                out shouldEnd);
            if (shouldEnd)
            {
                object oldValue = value;
                if (typeKey >= 0)
                {
                    if (typeKey == _key && _keySerializer != null)
                        value = _keySerializer.Read(value, source);
                    else
                        value = ProtoReader.ReadObject(value, typeKey, source);
                }
                else
                {
                    if (isDynamic)
                    {
                        if (source.TryReadBuiltinType(ref value, Helpers.GetTypeCode(type), true))
                            isDynamicLateSet = true;
                        else
                            throw new ProtoException("Dynamic type is not a contract-type: " + type.Name);
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
                if (isDynamicLateSet) options |= BclHelpers.NetObjectOptions.LateSet;
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
            SubItemToken token = NetObjectHelpers.WriteNetObject_Start(value, dest, _options, out dynamicTypeKey, out write);

            if (write)
            {
                // field header written!
                if ((_options & BclHelpers.NetObjectOptions.DynamicType) != 0)
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
                    if (_tail != null)
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
        public void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            var g = ctx.G;
            var s = g.StaticFactory;

            //bool shouldUnwrapNullable = _serializer != null && ExpectedType != _serializer.ExpectedType && Helpers.GetNullableUnderlyingType(ExpectedType) == _serializer.ExpectedType;

            using (Local value = RequiresOldValue ? ctx.GetLocalWithValueForEmitRead(this, valueFrom) : ctx.Local(_type))
            using (Local shouldEnd = ctx.Local(typeof(bool)))
            using (Local newTypeRefKey = ctx.Local(typeof(int)))
            using (Local typeKey = ctx.Local(typeof(int)))
            using (Local type = ctx.Local(typeof(System.Type)))
            using (Local newObjectKey = ctx.Local(typeof(int)))
            using (Local isDynamic = ctx.Local(typeof(bool)))
            using (Local isDynamicLateSet = ctx.Local(typeof(bool)))
            using (Local token = ctx.Local(typeof(SubItemToken)))
            using (Local oldValueBoxed = ctx.Local(typeof(object)))
            using (Local valueBoxed = ctx.Local(typeof(object)))
            {
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
                        _options,
                        isDynamic,
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
                        if (_keySerializer != null)
                        {
                            g.If(typeKey.AsOperand == _key);
                            {
                                _keySerializer.EmitRead(ctx, _keySerializer.RequiresOldValue ? value : null);
                                if (_keySerializer.ReturnsValue)
                                    g.Assign(value, g.GetStackValueOperand(_keySerializer.ExpectedType));
                                g.Assign(valueBoxed, value);
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
                                g.Assign(isDynamicLateSet, true);
                                g.Assign(value, valueBoxed.AsOperand.Cast(_type));
                            }
                            g.Else();
                            {
                                g.Assign(isDynamicLateSet, false);
                                g.ThrowProtoException("Dynamic type is not a contract-type: " + type.AsOperand.Property(nameof(Type.Name)));
                            }
                            g.End();
                        }
                        g.Else();
                        {
                            if (_tail == null)
                                g.ThrowProtoException("Dynamic type expected but no type info was read");
                            else
                            {
                                _tail.EmitRead(ctx, _tail.RequiresOldValue ? value : null);
                                if (_tail.ReturnsValue)
                                    g.Assign(value, g.GetStackValueOperand(_tail.ExpectedType));
                                g.Assign(valueBoxed, value);
                            }
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
                        (isDynamic.AsOperand && isDynamicLateSet.AsOperand).Conditional(_options | BclHelpers.NetObjectOptions.LateSet, _options),
                        token);
                }
                g.Else();
                {
                    if (Helpers.IsValueType(_type) && (ReturnsValue || RequiresOldValue))
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

                if (ReturnsValue)
                    ctx.LoadValue(value);
            }
        }

        public void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            using (Local value = ctx.GetLocalWithValue(_type, valueFrom))
            {
                var g = ctx.G;
                using (Local write = ctx.Local(typeof(bool)))
                using (Local dynamicTypeKey = ctx.Local(typeof(int)))
                using (Local token = ctx.Local(typeof(SubItemToken)))
                {
                    var s = g.StaticFactory;
                    // nullables: if null - will be boxed as null, if value - will be boxed as value, so don't worry about it
                    g.Assign(
                        token,
                        s.Invoke(ctx.MapType(typeof(NetObjectHelpers)), nameof(NetObjectHelpers.WriteNetObject_Start), value, g.ArgReaderWriter(), _options, dynamicTypeKey, write));
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