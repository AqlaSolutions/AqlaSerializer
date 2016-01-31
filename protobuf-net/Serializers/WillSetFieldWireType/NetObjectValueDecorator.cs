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
        readonly IProtoSerializerWithWireType _serializer;
        readonly Type _type;
        readonly RuntimeTypeModel _model;

        readonly BclHelpers.NetObjectOptions _options;
        readonly BinaryDataFormat _dataFormatForDynamicBuiltins;

        // no need for special handling of !Nullable.HasValue - when boxing they will be applied

        NetObjectValueDecorator(Type type, bool asReference, RuntimeTypeModel model)
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
            // TODO unwrap nullable in emit
            this._type = type;
            _model = model;
        }

        public NetObjectValueDecorator(IProtoSerializerWithWireType serializer, bool returnNullable, bool asReference, RuntimeTypeModel model)
            : this(type: MakeReturnNullable(serializer.ExpectedType, returnNullable, model), asReference: asReference, model: model)
        {
            _serializer = serializer;

            RequiresOldValue = _serializer.RequiresOldValue;
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
        public NetObjectValueDecorator(bool asReference, BinaryDataFormat dataFormatForDynamicBuiltins, RuntimeTypeModel model)
            : this(type: model.MapType(typeof(object)), asReference: asReference, model: model)
        {
            _dataFormatForDynamicBuiltins = dataFormatForDynamicBuiltins;
            _options |= BclHelpers.NetObjectOptions.DynamicType;
        }

        public NetObjectValueDecorator(Type type, int key, bool asReference, RuntimeTypeModel model)
            : this(type: type, asReference: asReference, model: model)
        {
            if (key < 0) throw new ArgumentOutOfRangeException(nameof(key));
            _key = key;
        }

        public Type ExpectedType => _type;
        public bool ReturnsValue => _serializer?.ReturnsValue ?? true;
        public bool RequiresOldValue { get; } = true;

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            if (!RequiresOldValue) value = null;
            bool shouldEnd;
            int newTypeRefKey;
            int newObjectKey;
            int typeKey = _key;
            var t = _type;
            bool isDynamic;
            BclHelpers.NetObjectOptions options = _options;
            SubItemToken token = NetObjectHelpers.ReadNetObject_Start(
                ref value,
                source,
                ref t,
                options,
                out isDynamic,
                ref typeKey,
                out newObjectKey,
                out newTypeRefKey,
                out shouldEnd);
            if (shouldEnd)
            {
                object oldValue = value;
                if (typeKey > 0)
                {
                    value = ProtoReader.ReadObject(value, typeKey, source);
                }
                else
                {
                    if (isDynamic)
                    {
                        if (source.TryReadBuiltinType(ref value, Helpers.GetTypeCode(t), true))
                            options |= BclHelpers.NetObjectOptions.LateSet;
                        else
                            throw new InvalidOperationException("Dynamic type is not a contract-type: " + value.GetType().Name);
                    }
                    else if (_serializer == null)
                    {
                        throw new InvalidOperationException("Dynamic type expected but no type info was read");
                    }
                    else
                    {
                        value = _serializer.Read(_serializer.RequiresOldValue ? value : null, source);
                    }
                }
                NetObjectHelpers.ReadNetObject_EndWithNoteNewObject(value, source, oldValue, t, newObjectKey, newTypeRefKey, options, token);
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
            SubItemToken t = NetObjectHelpers.WriteNetObject_Start(value, dest, _options, out dynamicTypeKey, out write);

            // TODO emit
            if (write)
            {
                // field header written!
                if ((_options & BclHelpers.NetObjectOptions.DynamicType) != 0)
                {
                    if (dynamicTypeKey < 0)
                    {
                        var typeCode = HelpersInternal.GetTypeCode(value.GetType());
                        var wireType = HelpersInternal.GetWireType(typeCode, _dataFormatForDynamicBuiltins);
                        if (wireType != WireType.None)
                        {
                            ProtoWriter.WriteFieldHeaderComplete(wireType, dest);
                            if (ProtoWriter.TryWriteBuiltinTypeValue(value, typeCode, true, dest))
                                write = false;
                        }
                        if (write)
                            throw new InvalidOperationException("Dynamic type is not a contract-type: " + _type.Name);
                    }
                    else ProtoWriter.WriteRecursionSafeObject(value, dynamicTypeKey, dest);
                }
                else if (_serializer != null)
                {
                    _serializer.Write(value, dest);
                }
                else
                    ProtoWriter.WriteRecursionSafeObject(value, _key, dest);
            }
            ProtoWriter.EndSubItem(t, dest);
        }

#endif

#if FEAT_COMPILER
        public void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            var g = new CodeGen(ctx.RunSharpContext, false);
            var s = ctx.RunSharpContext.StaticFactory;

            using (Local value = RequiresOldValue ? ctx.GetLocalWithValue(_type, valueFrom) : new Local(ctx, _type))
            {
                using (Local token = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
                using (Local shouldEnd = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local isType = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local newTypeKey = new Local(ctx, ctx.MapType(typeof(int))))
                using (Local newObjectKey = new Local(ctx, ctx.MapType(typeof(int))))
                using (Local t = new Local(ctx, ctx.MapType(typeof(System.Type))))
                using (Local newValue = new Local(ctx, ctx.MapType(typeof(object))))
                using (Local oldValue = new Local(ctx, ctx.MapType(typeof(object))))
                using (Local resultCasted = new Local(ctx, ctx.MapType(_type)))
                {
                    if (!RequiresOldValue)
                    {
                        if (_type.IsValueType)
                            g.InitObj(value);
                        else
                        {
                            ctx.LoadNullRef();
                            ctx.StoreValue(value);
                        }
                    }
                    g.Assign(t, ctx.MapType(_type));
                    g.Assign(
                        newValue,
                        s.Invoke(
                            typeof(NetObjectHelpers),
                            nameof(NetObjectHelpers.ReadNetObject_Start),
                            value,
                            g.Arg(ctx.ArgIndexReadWriter),
                            t,
                            _options,
                            token,
                            shouldEnd,
                            newObjectKey,
                            newTypeKey,
                            isType));

                    g.If(shouldEnd);
                    {
                        g.Assign(oldValue, newValue);
                        // valuetype: will never be null otherwise it would go to else
                        g.Assign(resultCasted, newValue.AsOperand.Cast(_type));
                        EmitDoRead(g, resultCasted, ctx);
                        g.Invoke(
                            typeof(NetObjectHelpers),
                            nameof(NetObjectHelpers.ReadNetObject_EndWithNoteNewObject),
                            newValue,
                            g.Arg(ctx.ArgIndexReadWriter),
                            oldValue,
                            t,
                            newObjectKey,
                            newTypeKey,
                            isType,
                            _options,
                            token);
                    }
                    g.Else();
                    {
                        // nullable will just unbox how it should be from null or value
                        if (_type.IsValueType && ReturnsValue && (Helpers.GetNullableUnderlyingType(_type) == null))
                        {
                            // TODO do we need to ensure this? versioning - change between reference type/nullable and value type
                            g.If(newValue.AsOperand == null);
                            {
                                g.InitObj(resultCasted);
                            }
                            g.Else();
                            {
                                g.Assign(resultCasted, newValue.AsOperand.Cast(_type));
                            }
                            g.End();
                        }
                        else
                            g.Assign(resultCasted, newValue.AsOperand.Cast(_type));

                        g.Invoke(typeof(ProtoReader), nameof(ProtoReader.EndSubItem), token, g.Arg(ctx.ArgIndexReadWriter));
                    }
                    g.End();

                    if (ReturnsValue)
                    {
                        ctx.LoadValue(resultCasted);
                    }
                }
            }
        }

        void EmitDoRead(CodeGen g, Local value, CompilerContext ctx)
        {
            _serializer.EmitRead(ctx, value);
            ctx.StoreValue(value);
        }

        public void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            using (Local value = ctx.GetLocalWithValue(_type, valueFrom))
            {
                var g = new CodeGen(ctx.RunSharpContext, false);
                using (Local write = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local t = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
                {
                    var s = ctx.RunSharpContext.StaticFactory;
                    // nullables: if null - will be boxed as null, if value - will be boxed as value, so don't worry about it
                    g.Assign(
                        t,
                        s.Invoke(ctx.MapType(typeof(NetObjectHelpers)), nameof(NetObjectHelpers.WriteNetObject_Start), value, g.Arg(ctx.ArgIndexReadWriter), _options, write));
                    g.If(write);
                    {
                        // field header written!
                        EmitDoWrite(g, value, ctx);
                    }
                    g.End();
                    g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.EndSubItem), t, g.Arg(ctx.ArgIndexReadWriter));
                }
            }

        }

        void EmitDoWrite(CodeGen g, Local value, CompilerContext ctx)
        {
            var s = ctx.RunSharpContext.StaticFactory;
            using (Local t2 = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                g.Assign(t2, s.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.StartSubItem), null, g.Arg(ctx.ArgIndexReadWriter)));
                _serializer.EmitWrite(ctx, value);
                g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.EndSubItem), t2, g.Arg(ctx.ArgIndexReadWriter));
            }
        }
#endif

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            return pts != null && pts.HasCallbacks(callbackType);
        }

        public bool CanCreateInstance()
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            return pts != null && pts.CanCreateInstance();
        }

#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)_serializer).CreateInstance(source);
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            if (pts != null) pts.Callback(value, callbackType, context);
        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            // we only expect this to be invoked if HasCallbacks returned true, so implicitly _serializer
            // **must** be of the correct type
            ((IProtoTypeSerializer)_serializer).EmitCallback(ctx, valueFrom, callbackType);
        }

        public void EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)_serializer).EmitCreateInstance(ctx);
        }
#endif
    }
}

#endif