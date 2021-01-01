using ProtoBuf.Compiler;
using AqlaSerializer.Meta;
using ProtoBuf.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AqlaSerializer.Serializers
{

    internal sealed class SubTypeSerializer<TParent, TChild> : SubItemSerializer, IDirectWriteNode
        where TParent : class
        where TChild : class, TParent
    {
        public override bool IsSubType => true;

        public override Type ExpectedType => typeof(TChild);
        public override Type BaseType => typeof(TParent);

        public override void Write(ref ProtoWriter.State state, object value)
            => state.WriteSubType<TChild>((TChild)value);

        public override object Read(ref ProtoReader.State state, object value)
        {
            var ss = (SubTypeState<TParent>)value;
            ss.ReadSubType<TChild>(ref state);
            return ss;
        }

        public override void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            // => ProtoWriter.WriteSubType<TChild>(value, writer, ref state, this);
            using var tmp = ctx.GetLocalWithValue(typeof(TChild), valueFrom);
            ctx.LoadState();
            ctx.LoadValue(tmp);
            ctx.LoadSelfAsService<ISubTypeSerializer<TChild>, TChild>(default, default);
            ctx.EmitCall(s_WriteSubType[2].MakeGenericMethod(typeof(TChild)));
        }

        bool IDirectWriteNode.CanEmitDirectWrite(WireType wireType) => wireType == WireType.String;

        void IDirectWriteNode.EmitDirectWrite(int fieldNumber, WireType wireType, CompilerContext ctx, Local valueFrom)
        {
            using var tmp = ctx.GetLocalWithValue(typeof(TChild), valueFrom);
            ctx.LoadState();
            ctx.LoadValue(fieldNumber);
            ctx.LoadValue(tmp);
            ctx.LoadSelfAsService<ISubTypeSerializer<TChild>, TChild>(default, default);
            ctx.EmitCall(s_WriteSubType[3].MakeGenericMethod(typeof(TChild)));
        }

        static readonly Dictionary<int, MethodInfo> s_WriteSubType =
            (from method in typeof(ProtoWriter.State).GetMethods(BindingFlags.Instance | BindingFlags.Public)
             where method.Name == nameof(ProtoWriter.State.WriteSubType) && method.IsGenericMethod
             select new { ArgCount = method.GetParameters().Length, Method = method }).ToDictionary(x => x.ArgCount, x => x.Method);


        public override void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            // we expect the input here to be the SubTypeState<>
            // => state.ReadSubType<TActual>(ref state, serializer);
            var type = typeof(SubTypeState<TParent>);
            ctx.LoadAddress(valueFrom, type);
            ctx.LoadState();
            ctx.LoadSelfAsService<ISubTypeSerializer<TChild>, TChild>(default, default);
            ctx.EmitCall(type.GetMethod(nameof(SubTypeState<TParent>.ReadSubType)).MakeGenericMethod(typeof(TChild)));
        }
    }
    internal class SubValueSerializer<T> : SubItemSerializer, IDirectWriteNode
    {
        public override bool IsSubType => false;

        public override Type ExpectedType => typeof(T);

        private ISerializer<T> _customSerializer;
        private ISerializer<T> CustomSerializer => MetaType.SerializerType is null ? null : (_customSerializer ?? CreateExternal());

        private ISerializer<T> CreateExternal()
            => _customSerializer = (ISerializer<T>)SerializerCache.GetInstance(MetaType.SerializerType, typeof(T));

        public override void Write(ref ProtoWriter.State state, object value)
        {
            var category = GetCategory();
            switch (category)
            {
                case SerializerFeatures.CategoryMessageWrappedAtRoot:
                case SerializerFeatures.CategoryMessage:
                    state.WriteMessage<T>(default, TypeHelper<T>.FromObject(value), CustomSerializer);
                    break;
                case SerializerFeatures.CategoryScalar:
                    CustomSerializer.Write(ref state, TypeHelper<T>.FromObject(value));
                    break;
                default:
                    category.ThrowInvalidCategory();
                    break;
            }
        }

        private SerializerFeatures GetCategory()
        {
            var custom = CustomSerializer;
            return custom is null ? SerializerFeatures.CategoryMessage : custom.Features.GetCategory();
        }

        public override object Read(ref ProtoReader.State state, object value)
        {
            var category = GetCategory();
            switch (category)
            {
                case SerializerFeatures.CategoryMessageWrappedAtRoot:
                case SerializerFeatures.CategoryMessage:
                    return state.ReadMessage<T>(default, TypeHelper<T>.FromObject(value), CustomSerializer);
                case SerializerFeatures.CategoryScalar:
                    return CustomSerializer.Read(ref state, TypeHelper<T>.FromObject(value));
                default:
                    category.ThrowInvalidCategory();
                    return default;
            }
        }

        protected override WireType GetDefaultWireType(ref DataFormat dataFormat)
        {
            var ser = CustomSerializer;
            if (ser is object)
            {
                var features = ser.Features;
                if (features.GetCategory() == SerializerFeatures.CategoryScalar)
                    return features.GetWireType();
            }
            return base.GetDefaultWireType(ref dataFormat);
        }

        public override void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            var category = GetCategory();
            switch (GetCategory())
            {
                case SerializerFeatures.CategoryMessage:
                case SerializerFeatures.CategoryMessageWrappedAtRoot:
                    SubItemSerializer.EmitWriteMessage<T>(null, WireType.String, ctx, valueFrom, serializerType: MetaType.SerializerType);
                    break;
                case SerializerFeatures.CategoryScalar:
                    using (var loc = ctx.GetLocalWithValue(typeof(T), valueFrom))
                    {
                        EmitLoadCustomSerializer(ctx, MetaType.SerializerType, typeof(T));
                        ctx.LoadState();
                        ctx.LoadValue(loc);
                        ctx.EmitCall(typeof(ISerializer<T>).GetMethod(nameof(ISerializer<T>.Write), BindingFlags.Public | BindingFlags.Instance));
                    }
                    break;
                default:
                    category.ThrowInvalidCategory();
                    break;

            }
        }

        public override void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            // make sure we have a non-stack-based source
            using var loc = ctx.GetLocalWithValue(typeof(T), valueFrom);
            var category = GetCategory();
            switch (category)
            {
                case SerializerFeatures.CategoryMessage:
                case SerializerFeatures.CategoryMessageWrappedAtRoot:
                    SubItemSerializer.EmitReadMessage<T>(ctx, loc, serializerType: MetaType.SerializerType);
                    break;
                case SerializerFeatures.CategoryScalar:
                    EmitLoadCustomSerializer(ctx, MetaType.SerializerType, typeof(T));
                    ctx.LoadState();
                    ctx.LoadValue(loc);
                    ctx.EmitCall(typeof(ISerializer<T>).GetMethod(nameof(ISerializer<T>.Read), BindingFlags.Public | BindingFlags.Instance));
                    break;
                default:
                    category.ThrowInvalidCategory();
                    break;

            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "Readability")]
        bool IDirectWriteNode.CanEmitDirectWrite(WireType wireType)
        {
            switch (GetCategory())
            {
                case SerializerFeatures.CategoryMessage:
                case SerializerFeatures.CategoryMessageWrappedAtRoot:
                    return wireType switch
                       {
                           WireType.String => true,
                           WireType.StartGroup => true,
                           _ => false
                       };
                default:
                    return false;
            }
        }
            

        void IDirectWriteNode.EmitDirectWrite(int fieldNumber, WireType wireType, CompilerContext ctx, Local valueFrom)
            => SubItemSerializer.EmitWriteMessage<T>(fieldNumber, wireType, ctx, valueFrom, serializerType: MetaType.SerializerType);
    }
    internal sealed class SubItemSerializer : IProtoTypeSerializer
    {
        SerializerFeatures IProtoTypeSerializer.Features
        {
            get
            {
                ThrowHelper.ThrowNotImplementedException();
                return default;
            }
        }
        public abstract bool IsSubType { get; }

        bool IProtoTypeSerializer.HasCallbacks(TypeModel.CallbackType callbackType)
            => Proxy.Serializer is IProtoTypeSerializer pts && pts.HasCallbacks(callbackType);
        public virtual Type BaseType => ExpectedType;
        bool IProtoTypeSerializer.HasInheritance => false;

        public abstract void Write(ref ProtoWriter.State state, object value);

        public abstract object Read(ref ProtoReader.State state, object value);

        public abstract void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
        public abstract void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom);

        void IProtoTypeSerializer.EmitReadRoot(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
            => ((IRuntimeProtoSerializerNode)this).EmitRead(ctx, valueFrom);

        void IProtoTypeSerializer.EmitWriteRoot(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
            => ((IRuntimeProtoSerializerNode)this).EmitWrite(ctx, valueFrom);
        public abstract Type ExpectedType { get; }

        bool IRuntimeProtoSerializerNode.RequiresOldValue => true;

        bool IProtoTypeSerializer.ShouldEmitCreateInstance
            => Proxy.Serializer is IProtoTypeSerializer pts && pts.ShouldEmitCreateInstance;

        bool IProtoTypeSerializer.CanCreateInstance()
            => Proxy.Serializer is IProtoTypeSerializer pts && pts.CanCreateInstance();

        bool IRuntimeProtoSerializerNode.ReturnsValue => true;

        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            ((IProtoTypeSerializer)Proxy.Serializer).EmitCallback(ctx, valueFrom, callbackType);
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx, bool callNoteObject)
        {
            ((IProtoTypeSerializer)Proxy.Serializer).EmitCreateInstance(ctx, callNoteObject);
        }

        protected static void EmitLoadCustomSerializer(CompilerContext ctx, Type serializerType, Type forType)
        {
            var provider = RuntimeTypeModel.GetUnderlyingProvider(serializerType, forType);
            RuntimeTypeModel.EmitProvider(provider, ctx.IL);
        }

void IProtoTypeSerializer.Callback(object value, TypeModel.CallbackType callbackType, ISerializationContext context)
{
    ((IProtoTypeSerializer)Proxy.Serializer).Callback(value, callbackType, context);
}

        object IProtoTypeSerializer.CreateInstance(ISerializationContext source)
        {
            return ((IProtoTypeSerializer)Proxy.Serializer).CreateInstance(source);
        }

        public static void EmitWriteMessage<T>(int? fieldNumber, WireType wireType, CompilerContext ctx, Local value = null,
            FieldInfo serializer = null, bool applyRecursionCheck = true, Type serializerType = null)
        {
            using var tmp = ctx.GetLocalWithValue(typeof(T), value);
            ctx.LoadState();
            if (fieldNumber.HasValue) ctx.LoadValue(fieldNumber.Value);
            ctx.LoadValue((int)(applyRecursionCheck ? default : SerializerFeatures.OptionSkipRecursionCheck));
            ctx.LoadValue(tmp);
            LoadSerializer<T>(ctx, serializer, serializerType);
            var methodFamily = wireType switch
            {
                WireType.StartGroup => s_WriteGroup,
                _ => s_WriteMessage,
            };
            ctx.EmitCall(methodFamily[fieldNumber.HasValue ? 4 : 3].MakeGenericMethod(typeof(T)));
        }

        private static void LoadSerializer<T>(CompilerContext ctx, FieldInfo serializer, Type serializerType)
        {
            if (serializerType is object && (ctx.NonPublic || RuntimeTypeModel.IsFullyPublic(serializerType)))
            {
                EmitLoadCustomSerializer(ctx, serializerType, typeof(T));
            }
            else if (serializer is object)
            {
                ctx.LoadValue(serializer, checkAccessibility: false);
            }
            else
            {
                ctx.LoadSelfAsService<ISerializer<T>, T>(default, default);
            }
        }
        public static void EmitReadMessage<T>(CompilerContext ctx, Local value = null, FieldInfo serializer = null
            , Type serializerType = null)
        {
            // state.ReadMessage<T>(default, value, serializer);
            ctx.LoadState();
            ctx.LoadValue(0); // features
            if (value is null)
            {
                if (TypeHelper<T>.IsReferenceType)
                {
                    ctx.LoadNullRef();
                }
                else
                {
                    using var val = new Local(ctx, typeof(T));
                    ctx.InitLocal(typeof(T), val);
                    ctx.LoadValue(val);
                }
            }
            else
            {
                ctx.LoadValue(value);
            }
            LoadSerializer<T>(ctx, serializer, serializerType);
            ctx.EmitCall(s_ReadMessage.MakeGenericMethod(typeof(T)));
        }

        private static readonly Dictionary<int, MethodInfo> s_WriteMessage =
            (from method in typeof(ProtoWriter.State).GetMethods(BindingFlags.Instance | BindingFlags.Public)
             where method.Name == nameof(ProtoWriter.State.WriteMessage)
                && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1
             select new { ArgCount = method.GetParameters().Length, Method = method }).ToDictionary(x => x.ArgCount, x => x.Method);

        private static readonly Dictionary<int, MethodInfo> s_WriteGroup =
            (from method in typeof(ProtoWriter.State).GetMethods(BindingFlags.Instance | BindingFlags.Public)
             where method.Name == nameof(ProtoWriter.State.WriteGroup)
                && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1
             select new { ArgCount = method.GetParameters().Length, Method = method }).ToDictionary(x => x.ArgCount, x => x.Method);

        private static readonly MethodInfo s_ReadMessage =
            (from method in typeof(ProtoReader.State).GetMethods(BindingFlags.Instance | BindingFlags.Public)
             where method.Name == nameof(ProtoReader.State.ReadMessage)
                && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1
                && method.GetParameters().Length == 3
             select method).Single();

        protected ISerializerProxy Proxy => MetaType;
        protected MetaType MetaType { get; private set; }


        internal static IRuntimeProtoSerializerNode Create(Type type, MetaType metaType, ref DataFormat dataFormat, out WireType defaultWireType)
        {
            var obj = (SubItemSerializer)Activator.CreateInstance(typeof(SubValueSerializer<>).MakeGenericType(type), nonPublic: true);
            obj.MetaType = metaType ?? throw new ArgumentNullException(nameof(metaType));
            defaultWireType = obj.GetDefaultWireType(ref dataFormat);
            return (IRuntimeProtoSerializerNode)obj;
        }

        protected virtual WireType GetDefaultWireType(ref DataFormat dataFormat)
            => dataFormat == DataFormat.Group ? WireType.StartGroup : WireType.String;

        internal static IRuntimeProtoSerializerNode Create(Type actualType, MetaType metaType, Type parentType)
        {
            var obj = (SubItemSerializer)Activator.CreateInstance(typeof(SubTypeSerializer<,>).MakeGenericType(parentType, actualType), nonPublic: true);
            obj.MetaType = metaType ?? throw new ArgumentNullException(nameof(metaType));
            return (IRuntimeProtoSerializerNode)obj;
        }

#if FEAT_COMPILER
        private bool EmitDedicatedMethod(Compiler.CompilerContext ctx, Compiler.Local valueFrom, bool read)
        {
            MethodBuilder method = ctx.GetDedicatedMethod(key, read);
            if (method == null) return false;

            using (Compiler.Local val = ctx.GetLocalWithValue(type, valueFrom))
            using (Compiler.Local token = new ProtoBuf.Compiler.Local(ctx, typeof(SubItemToken)))
            {
                Type rwType = read ? typeof(ProtoReader) : typeof(ProtoWriter);

                if (read)
                {
                    ctx.LoadReader(true);
                }
                else
                {
                    // write requires the object for StartSubItem; read doesn't
                    // (if recursion-check is disabled [subtypes] then null is fine too)
                    if (type.IsValueType || !recursionCheck) { ctx.LoadNullRef(); }
                    else { ctx.LoadValue(val); }
                    ctx.LoadWriter(true);
                }
                ctx.EmitCall(Helpers.GetStaticMethod(rwType, "StartSubItem",
                    read ? ProtoReader.State.ReaderStateTypeArray : new Type[] { typeof(object), rwType, ProtoWriter.ByRefStateType }));
                ctx.StoreValue(token);

                if (read)
                {
                    ctx.LoadReader(true);
                    ctx.LoadValue(val);
                }
                else
                {
                    ctx.LoadWriter(true);
                    ctx.LoadValue(val);
                }
                ctx.EmitCall(method);
                // handle inheritance (we will be calling the *base* version of things,
                // but we expect Read to return the "type" type)
                if (read && type != method.ReturnType) ctx.Cast(type);
                ctx.LoadValue(token);
                if (read)
                {
                    ctx.LoadReader(true);
                    ctx.EmitCall(Helpers.GetStaticMethod(rwType, "EndSubItem",
                        new Type[] { typeof(SubItemToken), rwType, ProtoReader.State.ByRefStateType }));
                }
                else
                {
                    ctx.LoadWriter(true);
                    ctx.EmitCall(ProtoWriter.GetStaticMethod("EndSubItem"));
                }
            }
            return true;
        }
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            if (!EmitDedicatedMethod(ctx, valueFrom, false))
            {
                ctx.LoadValue(valueFrom);
                if (type.IsValueType) ctx.CastToObject(type);
                ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key)); // re-map for formality, but would expect identical, else dedicated method
                ctx.LoadWriter(true);
                ctx.EmitCall(Helpers.GetStaticMethod(typeof(ProtoWriter), recursionCheck ? "WriteObject" : "WriteRecursionSafeObject", new Type[] { typeof(object), typeof(int), typeof(ProtoWriter), ProtoWriter.ByRefStateType }));
            }
        }
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            if (!EmitDedicatedMethod(ctx, valueFrom, true))
            {
                ctx.LoadValue(valueFrom);
                if (type.IsValueType) ctx.CastToObject(type);
                ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key)); // re-map for formality, but would expect identical, else dedicated method
                ctx.LoadReader(true);
                ctx.EmitCall(Helpers.GetStaticMethod(typeof(ProtoReader), "ReadObject",
                    new[] { typeof(object), typeof(int), typeof(ProtoReader), ProtoReader.State.ByRefStateType }));
                ctx.CastFromObject(type);
            }
        }
#endif
    }
}