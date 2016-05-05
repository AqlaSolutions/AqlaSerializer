// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AltLinq; using System.Linq;
#if FEAT_COMPILER
using TriAxis.RunSharp;
using AqlaSerializer.Compiler;
#endif
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    internal class ListDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
        // will be always group or string and won't change between group and string in same session
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility || WritePacked;
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            Action metaWriter =
                () =>
                    {
                        // we still write length in case it will be read as array
                        int length = (value as ICollection)?.Count ?? 0;
                        if (length > 0)
                        {
                            ProtoWriter.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant, dest);
                            ProtoWriter.WriteInt32(length, dest);
                        }
                        if (_writeSubType)
                        {
                            Type t = value.GetType();
                            if (_concreteTypeDefault != t)
                            {
                                ProtoWriter.WriteFieldHeaderBegin(ListHelpers.FieldSubtype, dest);
                                _subTypeHelpers.Write(_metaType, t, dest);
                            }
                        }
                    };
            ListHelpers.Write(value, metaWriter, null, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            IList list = null;
            object[] args = null;
            bool createdNew = false;
            bool asList = IsList && !SuppressIList;

            // can't call clear? => create new!
            bool forceNewInstance = !AppendToCollection && !asList;
            
            ListHelpers.Read(
                () =>
                    {
                        if (_metaType != null && source.TryReadFieldHeader(ListHelpers.FieldSubtype))
                        {
                            MetaType mt = _subTypeHelpers.TryRead(_metaType, forceNewInstance ? null : value?.GetType(), source);
                            if (mt != null)
                            {
                                value = mt.Serializer.CreateInstance(source);
                                createdNew = true;
                            }
                            return true;
                        }
                        return false;
                    },
                () =>
                    {
                        if (value == null || (forceNewInstance && !createdNew))
                        {
                            createdNew = true;
                            value = Activator.CreateInstance(_concreteTypeDefault);
                            ProtoReader.NoteObject(value, source);
                            if (asList)
                            {
                                list = (IList)value;
                                Debug.Assert(list != null);
                            }
                        }
                        else
                        {
                            if (!createdNew)
                                ProtoReader.NoteObject(value, source);

                            if (asList)
                            {
                                list = (IList)value;
                                Debug.Assert(list != null);
                                if (!AppendToCollection && !createdNew)
                                    list.Clear();
                            }
                        }

                        if (!asList)
                            args = new object[1];
                    },
                v =>
                    {
                        if (asList)
                            list.Add(v);
                        else
                        {
                            args[0] = v;
                            this._add.Invoke(value, args);
                        }
                    },
                source);

            return value;
        }

#endif

        internal static bool CanPack(WireType wireType)
        {
            switch (wireType)
            {
                case WireType.Fixed32:
                case WireType.Fixed64:
                case WireType.SignedVariant:
                case WireType.Variant:
                    return true;
                default:
                    return false;
            }
        }

        readonly byte _options;

        const byte OPTIONS_IsList = 1;
        const byte OPTIONS_SuppressIList = 2;
        const byte OPTIONS_WritePacked = 4;
        const byte OPTIONS_OverwriteList = 16;

        readonly Type _concreteTypeDefault;
        readonly MethodInfo _add;

        bool IsList => (_options & OPTIONS_IsList) != 0;
        bool SuppressIList => (_options & OPTIONS_SuppressIList) != 0;
        protected bool WritePacked => (_options & OPTIONS_WritePacked) != 0;
        protected readonly WireType PackedWireTypeForRead;
        
        readonly bool _protoCompatibility;
        readonly bool _writeSubType;

        protected readonly ListHelpers ListHelpers;
        readonly SubTypeHelpers _subTypeHelpers = new SubTypeHelpers();
        readonly MetaType _metaType;

        internal static ListDecorator Create(
            RuntimeTypeModel model, Type declaredType, Type concreteTypeDefault, IProtoSerializerWithWireType tail, bool writePacked, WireType packedWireType,
            bool overwriteList, bool protoCompatibility, bool writeSubType)
        {
#if !NO_GENERICS
            MethodInfo builderFactory, add, addRange, finish;
            if (ImmutableCollectionDecorator.IdentifyImmutable(model, declaredType, out builderFactory, out add, out addRange, out finish))
            {
                return new ImmutableCollectionDecorator(
                    model,
                    declaredType,
                    concreteTypeDefault,
                    tail,
                    writePacked,
                    packedWireType,
                    overwriteList,
                    builderFactory,
                    add,
                    addRange,
                    finish,
                    protoCompatibility);
            }
#endif
            return new ListDecorator(model, declaredType, concreteTypeDefault, tail, writePacked, packedWireType, overwriteList, protoCompatibility, writeSubType);
        }

        protected ListDecorator(
            RuntimeTypeModel model, Type declaredType, Type concreteTypeDefault, IProtoSerializerWithWireType tail, bool writePacked, WireType packedWireType,
            bool overwriteList, bool protoCompatibility, bool writeSubType)
            : base(tail)
        {
            if (overwriteList) _options |= OPTIONS_OverwriteList;

            PackedWireTypeForRead = packedWireType;
            _protoCompatibility = protoCompatibility;
            _writeSubType = writeSubType && !protoCompatibility;

            if (writePacked) _options |= OPTIONS_WritePacked;
            if (declaredType == null) throw new ArgumentNullException(nameof(declaredType));
            if (declaredType.IsArray) throw new ArgumentException("Cannot treat arrays as lists", nameof(declaredType));
            this.ExpectedType = declaredType;
            this._concreteTypeDefault = concreteTypeDefault ?? declaredType;

            // look for a public list.Add(typedObject) method
            if (RequireAdd)
            {
                bool isList;
                _add = TypeModel.ResolveListAdd(model, declaredType, tail.ExpectedType, out isList);
                if (isList)
                {
                    _options |= OPTIONS_IsList;
                    string fullName = declaredType.FullName;
                    if (fullName != null && fullName.StartsWith("System.Data.Linq.EntitySet`1[[", StringComparison.Ordinal))
                    { // see http://stackoverflow.com/questions/6194639/entityset-is-there-a-sane-reason-that-ilist-add-doesnt-set-assigned
                        _options |= OPTIONS_SuppressIList;
                    }
                }
                if (_add == null) throw new InvalidOperationException("Unable to resolve a suitable Add method for " + declaredType.FullName);
            }

            ListHelpers = new ListHelpers(WritePacked, PackedWireTypeForRead, _protoCompatibility, tail, false);

            if (!protoCompatibility)
            {
                int key = model.GetKey(declaredType, false, false);
                if (key >= 0)
                    _metaType = model[key];
                else
                    _writeSubType = false; // warn?
            }
        }

        protected virtual bool RequireAdd => true;

        public override Type ExpectedType { get; }

        public override bool RequiresOldValue => true;

        protected bool AppendToCollection => (_options & OPTIONS_OverwriteList) == 0;

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => true;

        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
            using (Compiler.Local oldValueForSubTypeHelpers = ctx.Local(value.Type))
            using (Compiler.Local createdNew = ctx.Local(typeof(bool), true))
            {
                bool asList = IsList && !SuppressIList;

                // can't call clear? => create new!
                bool forceNewInstance = !AppendToCollection && !asList;
                
                ListHelpers.EmitRead(
                    ctx.G,
                    (onSuccess, onFail) =>
                        {
                            using (ctx.StartDebugBlockAuto(this, "readNextMeta"))
                            {
                                if (_metaType != null)
                                {
                                    g.If(g.ReaderFunc.TryReadFieldHeader_bool(ListHelpers.FieldSubtype));
                                    {
                                        using (ctx.StartDebugBlockAuto(this, "subtype handler"))
                                        {
                                            g.Assign(oldValueForSubTypeHelpers, forceNewInstance ? null : value.AsOperand);
                                            _subTypeHelpers.EmitTryRead(
                                                g,
                                                oldValueForSubTypeHelpers,
                                                _metaType,
                                                r =>
                                                    {
                                                        using (ctx.StartDebugBlockAuto(this, "subtype handler - read"))
                                                        {
                                                            if (r != null)
                                                            {
                                                                ctx.MarkDebug("// creating list subtype");
                                                                r.Serializer.EmitCreateInstance(ctx);
                                                                ctx.StoreValue(value);
                                                                g.Assign(createdNew, true);
                                                            }
                                                        }
                                                    });
                                        }
                                        onSuccess();
                                    }
                                    g.Else();
                                    {
                                        onFail();
                                    }
                                    g.End();
                                }
                                else onFail();
                            }
                        }
                    ,
                    () =>
                        {
                            using (ctx.StartDebugBlockAuto(this, "prepareInstance"))
                            {
                                var createInstanceCondition = value.AsOperand == null;

                                // also create new if should clear existing instance on not lists
                                if (forceNewInstance)
                                    createInstanceCondition = createInstanceCondition || !createdNew.AsOperand;

                                g.If(createInstanceCondition);
                                {
                                    ctx.MarkDebug("// creating new list");
                                    EmitCreateInstance(ctx);
                                    ctx.StoreValue(value);
                                    g.Reader.NoteObject(value);
                                }
                                g.Else();
                                {
                                    g.If(!createdNew.AsOperand);
                                    {
                                        g.Reader.NoteObject(value);

                                        if (asList && !AppendToCollection)
                                        {
                                            ctx.MarkDebug("// clearing existing list");
                                            // ReSharper disable once PossibleNullReferenceException
                                            g.Invoke(value, "Clear");
                                        }
                                    }
                                    g.End();
                                }
                                g.End();
                            }
                        },
                    v =>
                        {
                            // TODO do null checks without allowing user == operators!
                            using (ctx.StartDebugBlockAuto(this, "add"))
                            {
#if DEBUG_COMPILE_2
                                g.If(v.AsOperand != null);
                                {
                                    g.ctx.MarkDebug("adding " + v.AsOperand.InvokeToString());
                                }
                                g.End();
#endif
                                if (asList)
                                {
                                    ctx.MarkDebug("// using Add method");
                                    Operand instance = value;
                                    if (_add != null && !Helpers.IsAssignableFrom(_add.DeclaringType, ExpectedType))
                                        instance = instance.Cast(_add.DeclaringType); // TODO optimize to local
                                    g.Invoke(instance, "Add", v);
                                }
                                else
                                {
                                    ctx.MarkDebug("// using add delegate");
                                    ctx.LoadAddress(value, ExpectedType);
                                    if (!Helpers.IsAssignableFrom(_add.DeclaringType, ExpectedType))
                                        ctx.Cast(_add.DeclaringType);
                                    ctx.LoadValue(v);
                                    ctx.EmitCall(this._add);
                                    if (this._add.ReturnType != null && this._add.ReturnType != ctx.MapType(typeof(void)))
                                        ctx.DiscardValue();
                                }
                            }
                        }
                    );
                if (EmitReadReturnsValue)
                {
                    ctx.MarkDebug("returning list");
                    ctx.LoadValue(value);
                }
            }
        }

        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            using (Compiler.Local t = ctx.Local(typeof(System.Type)))
            using (Compiler.Local length = ctx.Local(typeof(int)))
            using (var icol = !_protoCompatibility ? ctx.Local(typeof(ICollection)) : null)
            {
                ListHelpers.EmitWrite(
                    ctx.G,
                    value,
                    () =>
                        {
                            g.Assign(icol, value.AsOperand.As(icol.Type));
                            g.Assign(length, (icol.AsOperand != null).Conditional(icol.AsOperand.Property("Count"), -1));
                            g.If(length.AsOperand > 0);
                            {
                                g.Writer.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant);
                                g.Writer.WriteInt32(length);
                            }
                            g.End();

                            if (_writeSubType)
                            {
                                g.Assign(t, value.AsOperand.InvokeGetType());
                                g.If(_concreteTypeDefault != t.AsOperand);
                                {
                                    g.Writer.WriteFieldHeaderBegin(ListHelpers.FieldSubtype);
                                    _subTypeHelpers.EmitWrite(g, _metaType, value);
                                }
                                g.End();
                            }
                        },
                    null);
            }
        }



#if WINRT
        private static readonly TypeInfo ienumeratorType = typeof(IEnumerator).GetTypeInfo(), ienumerableType = typeof (IEnumerable).GetTypeInfo();
#else
        static readonly System.Type ienumeratorType = typeof(IEnumerator);
        static readonly System.Type ienumerableType = typeof(IEnumerable);
#endif

        protected MethodInfo GetEnumeratorInfo(TypeModel model, out MethodInfo moveNext, out MethodInfo current)
        {

#if WINRT
            TypeInfo enumeratorType = null, iteratorType, expectedType = ExpectedType.GetTypeInfo();
#else
            Type enumeratorType = null, iteratorType, expectedType = ExpectedType;
#endif

            // try a custom enumerator
            MethodInfo getEnumerator = Helpers.GetInstanceMethod(expectedType, "GetEnumerator", null);
            Type itemType = Tail.ExpectedType;

            Type getReturnType = null;
            if (getEnumerator != null)
            {
                getReturnType = getEnumerator.ReturnType;
                iteratorType = getReturnType
#if WINRT
                    .GetTypeInfo()
#endif
                    ;
                moveNext = Helpers.GetInstanceMethod(iteratorType, "MoveNext", null);
                PropertyInfo prop = Helpers.GetProperty(iteratorType, "Current", false);
                current = prop == null ? null : Helpers.GetGetMethod(prop, false, false);
                if (moveNext == null && (model.MapType(ienumeratorType).IsAssignableFrom(iteratorType)))
                {
                    moveNext = Helpers.GetInstanceMethod(model.MapType(ienumeratorType), "MoveNext", null);
                }
                // fully typed
                if (moveNext != null && moveNext.ReturnType == model.MapType(typeof(bool))
                    && current != null && current.ReturnType == itemType)
                {
                    return getEnumerator;
                }
                moveNext = current = getEnumerator = null;
            }

#if !NO_GENERICS
            // try IEnumerable<T>
            Type tmp = model.MapType(typeof(System.Collections.Generic.IEnumerable<>), false);

            if (tmp != null)
            {
                tmp = tmp.MakeGenericType(itemType);

#if WINRT
                enumeratorType = tmp.GetTypeInfo();
#else
                enumeratorType = tmp;
#endif
            }
            
            if (enumeratorType != null && enumeratorType.IsAssignableFrom(expectedType))
            {
                getEnumerator = Helpers.GetInstanceMethod(enumeratorType, "GetEnumerator");
                getReturnType = getEnumerator.ReturnType;

#if WINRT
                iteratorType = getReturnType.GetTypeInfo();
#else
                iteratorType = getReturnType;
#endif

                moveNext = Helpers.GetInstanceMethod(model.MapType(ienumeratorType), "MoveNext");
                current = Helpers.GetGetMethod(Helpers.GetProperty(iteratorType, "Current", false), false, false);
                return getEnumerator;
            }
#endif
            // give up and fall-back to non-generic IEnumerable
            enumeratorType = model.MapType(ienumerableType);
            getEnumerator = Helpers.GetInstanceMethod(enumeratorType, "GetEnumerator");
            getReturnType = getEnumerator.ReturnType;
            iteratorType = getReturnType
#if WINRT
                .GetTypeInfo()
#endif
                ;
            moveNext = Helpers.GetInstanceMethod(iteratorType, "MoveNext");
            current = Helpers.GetGetMethod(Helpers.GetProperty(iteratorType, "Current", false), false, false);
            return getEnumerator;
        }

#endif

        public virtual bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return false;
        }

        public virtual bool CanCreateInstance()
        {
            return true;
        }

#if !FEAT_IKVM
        public virtual object CreateInstance(ProtoReader source)
        {
            var r = Activator.CreateInstance(_concreteTypeDefault);
            ProtoReader.NoteObject(r, source);
            return r;
        }

        public virtual void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {

        }
#endif
#if FEAT_COMPILER
        public virtual void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        {

        }

        public virtual void EmitCreateInstance(CompilerContext ctx)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitCtor(_concreteTypeDefault);
                ctx.CopyValue();
                // we can use stack value here because note object on reader is static (backwards API)
                ctx.G.Reader.NoteObject(ctx.G.GetStackValueOperand(ExpectedType));
            }
        }
#endif

        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, ListHelpers.MakeDebugSchemaDescription(AppendToCollection)))
                Tail.WriteDebugSchema(builder);
        }

    }
}

#endif