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
using System.Runtime.CompilerServices;

#endif

namespace AqlaSerializer.Serializers
{
    internal class ListDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
        public override bool CanCancelWriting => ListHelpers.CanCancelWriting;

        // will be always group or string and won't change between group and string in same session
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility || WritePacked;
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            int? count = (value as ICollection)?.Count;
            Action metaWriter =
                () =>
                    {
                        // we still write length in case it will be read as array
                        int length = count ?? 0;
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
            ListHelpers.Write(value, count, metaWriter, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            IList list = null;
            object[] args = null;
            bool createdNew = false;
            bool asList = IsList && !SuppressIList;
            int length = 0;
            int oldLength = 0;

            // can't call clear? => create new!
            bool forceNewInstance = !AppendToCollection && !asList;

            ListHelpers.Read(
                () =>
                {
                    if (source.FieldNumber == ListHelpers.FieldLength)
                    {
                        // we write length to construct an array before deserializing
                        // so we can handle references to array from inside it

                        length = source.ReadInt32();
                        return true;
                    }
                    if (_metaType != null && source.FieldNumber == ListHelpers.FieldSubtype)
                    {
                        MetaType mt = _subTypeHelpers.TryRead(_metaType, forceNewInstance ? null : value?.GetType(), source);
                        if (mt != null)
                        {
                            if (mt.Type.IsArray)
                                Read_CheckLength(length);
                            value = mt.Type.IsArray
                                ? Read_CreateInstance(mt.Type, length, source)
                                : mt.Serializer.CreateInstance(source);
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
                            if (_concreteTypeDefault.IsArray)
                                Read_CheckLength(length);
                            value = Read_CreateInstance(_concreteTypeDefault, length, source);

                            if (_concreteTypeDefault.IsArray)
                                list = new List<object>();
                            else if (asList)
                            {
                                list = (IList)value;
                                Debug.Assert(list != null);
                            }
                        }
                        else
                        {
                            if (value is Array existingArray)
                            {
                                list = new List<object>();

                                if (!createdNew)
                                {
                                    if (AppendToCollection)
                                    {
                                        oldLength = existingArray.Length;
                                        foreach (var el in existingArray)
                                            list.Add(el);
                                    }

                                    if (existingArray.Length != length + oldLength)
                                    {
                                        createdNew = true;
                                        Read_CheckLength(length); // check only new elements length
                                        value = Read_CreateInstance(_concreteTypeDefault, length + oldLength, source);
                                    }
                                    else ProtoReader.NoteObject(value, source);
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
                        }

                        if (list == null)
                            args = new object[1];
                    },
                v =>
                    {
                        if (list != null)
                            list.Add(v);
                        else
                        {
                            args[0] = v;
                            this._add.Invoke(value, args);
                        }
                    },
                source);

            if (value is Array array)
                list.CopyTo(array, oldLength);

            return value;
        }

        private void Read_CheckLength(int length)
        {
            if (length > _arrayReadLengthLimit)
                ArrayDecorator.ThrowExceededLengthLimit(length, _arrayReadLengthLimit);
        }

        private object Read_CreateInstance(Type type, int arrayLength, ProtoReader source)
        {
            var r = type.IsArray
                ? Array.CreateInstance(Tail.ExpectedType, arrayLength)
                : Activator.CreateInstance(type);

            ProtoReader.NoteObject(r, source);
            return r;
        }

#endif

        internal static bool CanPackProtoCompatible(WireType wireType)
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
        protected readonly WireType ExpectedTailWireType;

        readonly bool _protoCompatibility;
        readonly bool _writeSubType;

        protected readonly ListHelpers ListHelpers;
        readonly SubTypeHelpers _subTypeHelpers = new SubTypeHelpers();
        readonly MetaType _metaType;
        readonly int _arrayReadLengthLimit;

        internal static ListDecorator Create(
            RuntimeTypeModel model, Type declaredType, Type concreteTypeDefault, IProtoSerializerWithWireType tail, bool writeProtoPacked, WireType expectedTailWireType,
            bool overwriteList, bool protoCompatibility, bool writeSubType, int arrayReadLengthLimit)
        {
            MethodInfo builderFactory, add, addRange, finish;
            if (ImmutableCollectionDecorator.IdentifyImmutable(model, declaredType, out builderFactory, out add, out addRange, out finish))
            {
                return new ImmutableCollectionDecorator(
                    model,
                    declaredType,
                    concreteTypeDefault,
                    tail,
                    writeProtoPacked,
                    expectedTailWireType,
                    overwriteList,
                    builderFactory,
                    add,
                    addRange,
                    finish,
                    protoCompatibility);
            }
            return new ListDecorator(model, declaredType, concreteTypeDefault, tail, writeProtoPacked, expectedTailWireType, overwriteList, protoCompatibility, writeSubType, arrayReadLengthLimit);
        }

        protected ListDecorator(
            RuntimeTypeModel model, Type declaredType, Type concreteTypeDefault, IProtoSerializerWithWireType tail, bool writePacked, WireType expectedTailWireType,
            bool overwriteList, bool protoCompatibility, bool writeSubType, int arrayReadLengthLimit)
            : base(tail)
        {
            if (overwriteList) _options |= OPTIONS_OverwriteList;

            ExpectedTailWireType = expectedTailWireType;
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

            ListHelpers = new ListHelpers(WritePacked, ExpectedTailWireType, _protoCompatibility, tail, false);

            if (!protoCompatibility)
            {
                int key = model.GetKey(declaredType, false, false);
                if (key >= 0)
                    _metaType = model[key];
                else
                    _writeSubType = false; // warn?
            }
            _arrayReadLengthLimit = arrayReadLengthLimit;
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
            using (Compiler.Local tempList = ctx.Local(ctx.MapType(typeof(List<>)).MakeGenericType(Tail.ExpectedType)))
            using (Compiler.Local value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
            using (Compiler.Local oldValueForSubTypeHelpers = ctx.Local(value.Type))
            using (Compiler.Local createdNew = ctx.Local(typeof(bool), true))
            using (Compiler.Local length = ctx.Local(typeof(int), true))
            using (Compiler.Local oldLen = ctx.Local(typeof(int)))
            using (Compiler.Local asArray = ctx.Local(Tail.ExpectedType.MakeArrayType()))
            {
                bool asList = IsList && !SuppressIList;

                bool canBeArray = ExpectedType.IsAssignableFrom(ctx.MapType(asArray.Type));
                if (canBeArray)
                    g.InitObj(asArray);

                // can't call clear? => create new!
                bool forceNewInstance = !AppendToCollection && !asList;

                ListHelpers.EmitRead(
                    ctx.G,
                    (fieldNumber, onSuccess, onFail) => {
                        using (ctx.StartDebugBlockAuto(this, "readNextMeta"))
                        {
                            if (_metaType != null)
                            {
                                g.If(fieldNumber == ListHelpers.FieldLength);
                                {
                                    g.Assign(length, g.ReaderFunc.ReadInt32());
                                    onSuccess();
                                }
                                g.Else();
                                {
                                    g.If(fieldNumber == ListHelpers.FieldSubtype);
                                    {
                                        using (ctx.StartDebugBlockAuto(this, "subtype handler"))
                                        {
                                            g.Assign(oldValueForSubTypeHelpers, forceNewInstance ? null : value.AsOperand);
                                            _subTypeHelpers.EmitTryRead(
                                                g,
                                                oldValueForSubTypeHelpers,
                                                _metaType,
                                                r => {
                                                    using (ctx.StartDebugBlockAuto(this, "subtype handler - read"))
                                                    {
                                                        if (r != null)
                                                        {
                                                            ctx.MarkDebug("// creating list subtype");
                                                            if (r.Type.IsArray)
                                                            {
                                                                EmitRead_CheckLength(g, length);
                                                                EmitRead_CreateInstance(g, r.Type, length);
                                                            }
                                                            else
                                                            {
                                                                r.Serializer.EmitCreateInstance(ctx);
                                                            }
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
                                g.End();
                            }
                            else onFail();
                        }
                    }
                    ,
                    () => {
                        using (ctx.StartDebugBlockAuto(this, "prepareInstance"))
                        {
                            var createInstanceCondition = value.AsOperand == null;

                            // also create new if should clear existing instance on not lists
                            if (forceNewInstance)
                                createInstanceCondition = createInstanceCondition || !createdNew.AsOperand;

                            g.If(createInstanceCondition);
                            {
                                ctx.MarkDebug("// creating new list");
                                if (_concreteTypeDefault.IsArray)
                                    EmitRead_CheckLength(g, length);
                                EmitRead_CreateInstance(g, _concreteTypeDefault, length);
                                if (_concreteTypeDefault.IsArray)
                                {
                                    ctx.CopyValue();
                                    g.Assign(asArray, g.GetStackValueOperand(_concreteTypeDefault));
                                    g.Assign(tempList, g.ExpressionFactory.New(tempList.Type));
                                }
                                ctx.StoreValue(value);
                            }
                            g.Else();
                            {
                                if (canBeArray)
                                {
                                    g.Assign(asArray, value.AsOperand.As(asArray.Type));
                                    g.If(asArray.AsOperand != null);
                                    {
                                        g.Assign(tempList, g.ExpressionFactory.New(tempList.Type));

                                        g.If(!createdNew.AsOperand);
                                        {
                                            if (AppendToCollection)
                                            {
                                                g.Assign(oldLen, asArray.AsOperand.ArrayLength());
                                                using (var i = ctx.Local(typeof(int)))
                                                {
                                                    g.For(i.AsOperand.Assign(0), i < oldLen.AsOperand, i.AsOperand.Increment());
                                                    {
                                                        g.Invoke(tempList, "Add", asArray.AsOperand[i]);
                                                    }
                                                    g.End();
                                                }
                                            }
                                            else g.Assign(oldLen, 0);
                                            g.If(asArray.AsOperand.ArrayLength() != length + oldLen.AsOperand);
                                            {
                                                // createdNew = true
                                                EmitRead_CheckLength(g, length);
                                                EmitRead_CreateInstance(g, asArray.Type, length + oldLen.AsOperand);
                                                ctx.CopyValue();
                                                g.Assign(asArray, g.GetStackValueOperand(asArray.Type));
                                                ctx.StoreValue(value);
                                            }
                                            g.Else();
                                            {
                                                g.Reader.NoteObject(value);
                                            }
                                            g.End();
                                        }
                                        g.Else();
                                        {
                                            g.Assign(oldLen, 0);
                                        }
                                        g.End();
                                    }
                                    g.Else();
                                }
                                // if not array
                                {

                                    g.If(!createdNew.AsOperand);
                                    {
                                        g.Reader.NoteObject(value);
                                    }
                                    g.End();

                                    if (asList && !AppendToCollection)
                                    {
                                        ctx.MarkDebug("// clearing existing list");
                                        // ReSharper disable once PossibleNullReferenceException
                                        g.Invoke(value, "Clear");
                                    }
                                }
                                if (canBeArray)
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
                                if (canBeArray)
                                {
                                    g.If(asArray.AsOperand != null);
                                    {
                                        ctx.MarkDebug("// using Add method on tempList");
                                        g.Invoke(tempList, "Add", v);
                                    }
                                    g.Else();
                                }
                                // if not array
                                {
                                    if (asList)
                                    {
                                        ctx.MarkDebug("// using Add method");
                                        Operand instance = value;
                                        // call using a type where _add in declared
                                        if (_add != null && !Helpers.IsAssignableFrom(_add.DeclaringType, ExpectedType))
                                            instance = instance.Cast(_add.DeclaringType); // TODO optimize to local
                                        // don't use delegate here even if it's set
                                        // there is a bug in RunSharp that doesn't box v when calling with MethodInfo
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
                                if (canBeArray)
                                    g.End();
                            }
                        }
                    );

                if (canBeArray)
                {
                    g.If(asArray.AsOperand != null);
                    {
                        g.Invoke(tempList.AsOperand, g.TypeMapper.MapType(typeof(ICollection)).GetMethod("CopyTo"), asArray, oldLen);
                    }
                    g.End();
                }

                if (EmitReadReturnsValue)
                {
                    ctx.MarkDebug("returning list");
                    ctx.LoadValue(value);
                }
            }
        }

        private void EmitRead_CheckLength(SerializerCodeGen g, Local length)
        {
            g.If(length.AsOperand > _arrayReadLengthLimit);
            {
                ArrayDecorator.EmitThrowExceededLengthLimit(g, length, _arrayReadLengthLimit);
            }
            g.End();
        }

        void EmitRead_CreateInstance(SerializerCodeGen g, Type type, Operand length)
        {
            var ctx = g.ctx;
            using (ctx.StartDebugBlockAuto("EmitRead_CreateInstance"))
            {
                if (type.IsArray)
                {
                    ctx.G.LeaveNextReturnOnStack();
                    ctx.G.Eval(ctx.G.ExpressionFactory.NewArray(Tail.ExpectedType, length));
                }
                else
                    ctx.EmitCtor(_concreteTypeDefault);

                ctx.CopyValue();
                // we can use stack value here because note object on reader is static (backwards API)
                ctx.G.Reader.NoteObject(ctx.G.GetStackValueOperand(ExpectedType));
            }
        }

        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            Local icol = null;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            using (Compiler.Local t = ctx.Local(typeof(System.Type)))
            using (Compiler.Local length = ctx.Local(typeof(int)))
            using (Compiler.Local countNullable = ctx.Local(typeof(int?), false))
            {
                bool countSet = false;

                ContextualOperand GetCachedCountNullable()
                {
                    if (countSet) return countNullable.AsOperand;
                    countSet = true;
                    icol = ctx.Local(typeof(ICollection));
                    g.Assign(icol, value.AsOperand.As(icol.Type));
                    g.If(icol.AsOperand != null);
                    {
                        g.Assign(countNullable, (icol.AsOperand.Property("Count")));
                    }
                    g.Else();
                    g.Assign(countNullable, null);
                    g.End();
                    return countNullable.AsOperand;
                }


                try
                {
                    ListHelpers.EmitWrite(
                        ctx.G,
                        value,
                        GetCachedCountNullable,
                        () => {

                            icol = ctx.Local(typeof(ICollection));
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
                finally
                {
                    icol?.Dispose();
                }
            }
        }



        static readonly System.Type ienumeratorType = typeof(IEnumerator);
        static readonly System.Type ienumerableType = typeof(IEnumerable);

        protected MethodInfo GetEnumeratorInfo(TypeModel model, out MethodInfo moveNext, out MethodInfo current)
        {

            Type enumeratorType = null, iteratorType, expectedType = ExpectedType;

            // try a custom enumerator
            MethodInfo getEnumerator = Helpers.GetInstanceMethod(expectedType, "GetEnumerator", null);
            Type itemType = Tail.ExpectedType;

            Type getReturnType = null;
            if (getEnumerator != null)
            {
                getReturnType = getEnumerator.ReturnType;
                iteratorType = getReturnType;
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

            // try IEnumerable<T>
            Type tmp = model.MapType(typeof(System.Collections.Generic.IEnumerable<>), false);

            if (tmp != null)
            {
                tmp = tmp.MakeGenericType(itemType);

                enumeratorType = tmp;
            }

            if (enumeratorType != null && enumeratorType.IsAssignableFrom(expectedType))
            {
                getEnumerator = Helpers.GetInstanceMethod(enumeratorType, "GetEnumerator");
                getReturnType = getEnumerator.ReturnType;

                iteratorType = getReturnType;

                moveNext = Helpers.GetInstanceMethod(model.MapType(ienumeratorType), "MoveNext");
                current = Helpers.GetGetMethod(Helpers.GetProperty(iteratorType, "Current", false), false, false);
                return getEnumerator;
            }
            // give up and fall-back to non-generic IEnumerable
            enumeratorType = model.MapType(ienumerableType);
            getEnumerator = Helpers.GetInstanceMethod(enumeratorType, "GetEnumerator");
            getReturnType = getEnumerator.ReturnType;
            iteratorType = getReturnType;
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
            return Read_CreateInstance(_concreteTypeDefault, 0, source);
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
                if (_concreteTypeDefault.IsArray)
                {
                    ctx.G.LeaveNextReturnOnStack();
                    ctx.G.Eval(ctx.G.ExpressionFactory.NewArray(Tail.ExpectedType, 0));
                }
                else
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