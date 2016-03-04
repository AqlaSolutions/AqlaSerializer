﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    class ListDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
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
        private readonly byte options;

        private const byte OPTIONS_IsList = 1,
                           OPTIONS_SuppressIList = 2,
                           OPTIONS_WritePacked = 4,
                           OPTIONS_ReturnList = 8,
                           OPTIONS_OverwriteList = 16,
                           OPTIONS_SupportNull = 32;

        private readonly Type declaredType, concreteType;

        private readonly MethodInfo add;

        private readonly int fieldNumber;

        private bool IsList { get { return (options & OPTIONS_IsList) != 0; } }
        private bool SuppressIList { get { return (options & OPTIONS_SuppressIList) != 0; } }
        private bool WritePacked { get { return (options & OPTIONS_WritePacked) != 0; } }
        private bool SupportNull { get { return (options & OPTIONS_SupportNull) != 0; } }
        private bool ReturnList { get { return (options & OPTIONS_ReturnList) != 0; } }
        protected readonly WireType packedWireType;

        readonly Type itemType;

        internal static ListDecorator Create(TypeModel model, Type declaredType, Type concreteType, IProtoSerializer tail, int fieldNumber, bool writePacked, WireType packedWireType, bool returnList, bool overwriteList, bool supportNull)
        {
#if !NO_GENERICS
            MethodInfo builderFactory, add, addRange, finish;
            if (returnList && ImmutableCollectionDecorator.IdentifyImmutable(model, declaredType, out builderFactory, out add, out addRange, out finish))
            {
                return new ImmutableCollectionDecorator(
                    model, declaredType, concreteType, tail, fieldNumber, writePacked, packedWireType, returnList, overwriteList, supportNull,
                    builderFactory, add, addRange, finish);
            }

#endif
            return new ListDecorator(model, declaredType, concreteType, tail, fieldNumber, writePacked, packedWireType, returnList, overwriteList, supportNull);
        }

        protected ListDecorator(TypeModel model, Type declaredType, Type concreteType, IProtoSerializer tail, int fieldNumber, bool writePacked, WireType packedWireType, bool returnList, bool overwriteList, bool supportNull)
            : base(tail)
        {
            this.itemType = tail.ExpectedType;
            if (returnList) options |= OPTIONS_ReturnList;
            if (overwriteList) options |= OPTIONS_OverwriteList;
            if (supportNull) options |= OPTIONS_SupportNull;
            if ((writePacked || packedWireType != WireType.None) && fieldNumber <= 0) throw new ArgumentOutOfRangeException("fieldNumber");
            if (!CanPack(packedWireType))
            {
                if (writePacked) throw new InvalidOperationException("Only simple data-types can use packed encoding");
                packedWireType = WireType.None;
            }            

            this.fieldNumber = fieldNumber;
            if (writePacked) options |= OPTIONS_WritePacked;
            this.packedWireType = packedWireType;
            if (declaredType == null) throw new ArgumentNullException("declaredType");
            if (declaredType.IsArray) throw new ArgumentException("Cannot treat arrays as lists", "declaredType");
            this.declaredType = declaredType;
            this.concreteType = concreteType;
            
            // look for a public list.Add(typedObject) method
            if (RequireAdd)
            {
                bool isList;
                add = TypeModel.ResolveListAdd(model, declaredType, tail.ExpectedType, out isList);
                if (isList)
                {
                    options |= OPTIONS_IsList;
                    string fullName = declaredType.FullName;
                    if (fullName != null && fullName.StartsWith("System.Data.Linq.EntitySet`1[["))
                    { // see http://stackoverflow.com/questions/6194639/entityset-is-there-a-sane-reason-that-ilist-add-doesnt-set-assigned
                        options |= OPTIONS_SuppressIList;
                    }
                }
                if (add == null) throw new InvalidOperationException("Unable to resolve a suitable Add method for " + declaredType.FullName);
            }

        }
        protected virtual bool RequireAdd { get { return true; } }

        public override Type ExpectedType { get { return declaredType;  } }
        public override bool RequiresOldValue { get { return AppendToCollection; } }
        public override bool ReturnsValue { get { return ReturnList; } }

        protected bool AppendToCollection
        {
            get { return (options & OPTIONS_OverwriteList) == 0; }
        }

#if FEAT_COMPILER
        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            /* This looks more complex than it is. Look at the non-compiled Read to
             * see what it is trying to do, but note that it needs to cope with a
             * few more scenarios. Note that it picks the **most specific** Add,
             * unlike the runtime version that uses IList when possible. The core
             * is just a "do {list.Add(readValue())} while {thereIsMore}"
             * 
             * The complexity is due to:
             *  - value types vs reference types (boxing etc)
             *  - initialization if we need to pass in a value to the tail
             *  - handling whether or not the tail *returns* the value vs updates the input
             */
            bool returnList = ReturnList;
            
            using (Compiler.Local list = AppendToCollection ? ctx.GetLocalWithValue(ExpectedType, valueFrom) : new Compiler.Local(ctx, declaredType))
            using (Compiler.Local origlist = (returnList && AppendToCollection) ? new Compiler.Local(ctx, ExpectedType) : null)
            {
                if (!AppendToCollection)
                { // always new
                    ctx.LoadNullRef();
                    ctx.StoreValue(list);
                }
                else if (returnList)
                { // need a copy
                    ctx.LoadValue(list);
                    ctx.StoreValue(origlist);
                }
                if (concreteType != null)
                {
                    ctx.LoadValue(list);
                    Compiler.CodeLabel notNull = ctx.DefineLabel();
                    ctx.BranchIfTrue(notNull, true);
                    ctx.EmitCtor(concreteType);
                    ctx.CopyValue();
                    ctx.CastToObject(concreteType);
                    ctx.EmitCallNoteObject();
                    ctx.StoreValue(list);
                    ctx.MarkLabel(notNull);
                }

                bool castListForAdd = !add.DeclaringType.IsAssignableFrom(declaredType);
                EmitReadList(ctx, list, Tail, add, packedWireType, castListForAdd);

                if (returnList)
                {
                    if (AppendToCollection)
                    {
                        // remember ^^^^ we had a spare copy of the list on the stack; now we'll compare
                        ctx.LoadValue(origlist);
                        ctx.LoadValue(list); // [orig] [new-value]
                        Compiler.CodeLabel sameList = ctx.DefineLabel(), allDone = ctx.DefineLabel();
                        ctx.BranchIfEqual(sameList, true);
                        ctx.LoadValue(list);
                        ctx.Branch(allDone, true);
                        ctx.MarkLabel(sameList);
                        ctx.LoadNullRef();
                        ctx.MarkLabel(allDone);
                    }
                    else
                    {
                        ctx.LoadValue(list);
                    }
                }
            }
        }

        internal static void EmitReadList(AqlaSerializer.Compiler.CompilerContext ctx, Compiler.Local list, IProtoSerializer tail, MethodInfo add, WireType packedWireType, bool castListForAdd)
        {
            using (Compiler.Local fieldNumber = new Compiler.Local(ctx, ctx.MapType(typeof(int))))
            {
                Compiler.CodeLabel readPacked = packedWireType == WireType.None ? new Compiler.CodeLabel() : ctx.DefineLabel();                                   
                if (packedWireType != WireType.None)
                {
                    ctx.LoadReaderWriter();
                    ctx.LoadValue(typeof(ProtoReader).GetProperty("WireType"));
                    ctx.LoadValue((int)WireType.String);
                    ctx.BranchIfEqual(readPacked, false);
                }

                ctx.LoadReaderWriter();
                ctx.LoadValue(typeof(ProtoReader).GetProperty("FieldNumber"));
                ctx.StoreValue(fieldNumber);

                EmitReadAndAddItem(ctx, list, tail, add, castListForAdd, true);

                { // while TryReadFieldHeader
                    Compiler.CodeLabel @continue = ctx.DefineLabel();
                    Compiler.CodeLabel @end = ctx.DefineLabel();
                    ctx.MarkLabel(@continue);

                    ctx.LoadReaderWriter();
                    ctx.LoadValue(fieldNumber);
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("TryReadFieldHeader"));
                    ctx.BranchIfFalse(@end, false);

                    EmitReadAndAddItem(ctx, list, tail, add, castListForAdd);
                    
                    ctx.Branch(@continue, false);

                    ctx.MarkLabel(@end);
                }

                if (packedWireType != WireType.None)
                {
                    Compiler.CodeLabel allDone = ctx.DefineLabel();
                    ctx.Branch(allDone, false);
                    ctx.MarkLabel(readPacked);

                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("StartSubItem"));

                    ctx.LoadValue((int)packedWireType);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("HasSubValue"));
                    ctx.DiscardValue();
                    // no check for performance
                    EmitReadAndAddItem(ctx, list, tail, add, castListForAdd, true);

                    Compiler.CodeLabel testForData = ctx.DefineLabel(), noMoreData = ctx.DefineLabel();
                    ctx.MarkLabel(testForData);
                    ctx.LoadValue((int)packedWireType);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("HasSubValue"));
                    ctx.BranchIfFalse(noMoreData, false);

                    EmitReadAndAddItem(ctx, list, tail, add, castListForAdd);
                    ctx.Branch(testForData, false);

                    ctx.MarkLabel(noMoreData);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("EndSubItem"));
                    ctx.MarkLabel(allDone);
                }


                
            }
        }

        static void EmitReadAndAddItem(Compiler.CompilerContext ctx, Compiler.Local list, IProtoSerializer tail, MethodInfo add, bool castListForAdd)
        {
            EmitReadAndAddItem(ctx, list, tail, add, castListForAdd, false);
        }

        private static void EmitReadAndAddItem(Compiler.CompilerContext ctx, Compiler.Local list, IProtoSerializer tail, MethodInfo add, bool castListForAdd, bool isFake)
        {
            ctx.LoadAddress(list, list.Type); // needs to be the reference in case the list is value-type (static-call)
            if (castListForAdd) ctx.Cast(add.DeclaringType);
            Type itemType = tail.ExpectedType;
            bool tailReturnsValue = tail.ReturnsValue;
            if (tail.RequiresOldValue)
            {
                if (itemType.IsValueType || !tailReturnsValue)
                {
                    // going to need a variable
                    using (Compiler.Local item = new Compiler.Local(ctx, itemType))
                    {
                        if (itemType.IsValueType)
                        {   // initialise the struct
                            ctx.LoadAddress(item, itemType);
                            ctx.EmitCtor(itemType);
                        }
                        else
                        {   // assign null
                            ctx.LoadNullRef();
                            ctx.StoreValue(item);
                        }
                        tail.EmitRead(ctx, item);
                        if (!tailReturnsValue) { ctx.LoadValue(item); }
                    }
                }
                else
                {    // no variable; pass the null on the stack and take the value *off* the stack
                    ctx.LoadNullRef();
                    tail.EmitRead(ctx, null);
                }
            }
            else
            {
                if (tailReturnsValue)
                {   // out only (on the stack); just emit it
                    tail.EmitRead(ctx, null);
                }
                else
                {   // doesn't take anything in nor return anything! WTF?
                    throw new InvalidOperationException();
                }
            }
            // our "Add" is chosen either to take the correct type, or to take "object";
            // we may need to box the value
                
            Type addParamType = add.GetParameters()[0].ParameterType;
            if(addParamType != itemType) {
                if (addParamType == ctx.MapType(typeof(object)))
                {
                    ctx.CastToObject(itemType);
                }
#if !NO_GENERICS
                else if(Helpers.GetNullableUnderlyingType( addParamType) == itemType)
                { // list is nullable
                    ConstructorInfo ctor = Helpers.GetConstructor(addParamType, new Type[] {itemType}, false);
                    ctx.EmitCtor(ctor); // the itemType on the stack is now a Nullable<ItemType>
                }
#endif
                else
                {
                    throw new InvalidOperationException("Conflicting item/add type");
                }
            }
            if (isFake)
            {
                ctx.DiscardValue();
                ctx.DiscardValue();
            }
            else
            {
                ctx.EmitCall(add);
                if (add.ReturnType != ctx.MapType(typeof(void)))
                {
                    ctx.DiscardValue();
                }
            }
        }
#endif

#if WINRT
        private static readonly TypeInfo ienumeratorType = typeof(IEnumerator).GetTypeInfo(), ienumerableType = typeof (IEnumerable).GetTypeInfo();
#else
        private static readonly System.Type ienumeratorType = typeof (IEnumerator), ienumerableType = typeof (IEnumerable);
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
;
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
            current = Helpers.GetGetMethod(Helpers.GetProperty(iteratorType,"Current", false), false, false);
            return getEnumerator;
        }
#if FEAT_COMPILER
        static internal void EmitWriteEmptyElement(AqlaSerializer.Compiler.CompilerContext ctx, Type itemType, IProtoSerializer tail, bool writeNullChecked)
        {
            if (itemType.IsValueType)
            {
                using (Compiler.Local temp = new Compiler.Local(ctx, ctx.MapType(itemType)))
                {
                    ctx.LoadAddress(temp, ctx.MapType(itemType));
                    ctx.EmitCtor(ctx.MapType(itemType));
                    ctx.LoadValue(temp);
                    EmitWriteEmptyElement_Write(ctx, itemType, tail, false);
                }
            }
            else
            {
                ctx.LoadNullRef();
                EmitWriteEmptyElement_Write(ctx, itemType, tail, writeNullChecked);
            }

        }

        static void EmitWriteEmptyElement_Write(AqlaSerializer.Compiler.CompilerContext ctx, Type itemType, IProtoSerializer tail, bool writeNullChecked)
        {
            if (writeNullChecked)
                ctx.WriteNullCheckedTail(itemType, tail, null);
            else
                tail.EmitWrite(ctx, null);
        }

        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            using (Compiler.Local list = ctx.GetLocalWithValue(ExpectedType, valueFrom))
            {
                MethodInfo moveNext, current, getEnumerator = GetEnumeratorInfo(ctx.Model, out moveNext, out current);
                Helpers.DebugAssert(moveNext != null);
                Helpers.DebugAssert(current != null);
                Helpers.DebugAssert(getEnumerator != null);
                Type enumeratorType = getEnumerator.ReturnType;
                bool writePacked = WritePacked;
                using (Compiler.Local iter = new Compiler.Local(ctx, enumeratorType))
                using (Compiler.Local token = writePacked ? new Compiler.Local(ctx, ctx.MapType(typeof(SubItemToken))) : null)
                {
                    if (writePacked)
                    {
                        ctx.LoadValue(fieldNumber);
                        ctx.LoadValue((int)WireType.String);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("WriteFieldHeader"));

                        ctx.LoadValue(list);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("StartSubItem"));
                        ctx.StoreValue(token);

                        ctx.LoadValue(fieldNumber);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("SetPackedField"));
                    }

                    EmitWriteEmptyElement(ctx, itemType, Tail, false);
                    
                    ctx.LoadAddress(list, ExpectedType);
                    ctx.EmitCall(getEnumerator);
                    ctx.StoreValue(iter);
                    using (ctx.Using(iter))
                    {
                        Compiler.CodeLabel body = ctx.DefineLabel(), next = ctx.DefineLabel();
                        ctx.Branch(next, false);
                        
                        ctx.MarkLabel(body);

                        ctx.LoadAddress(iter, enumeratorType);
                        ctx.EmitCall(current);
                        Type expectedType = Tail.ExpectedType;
                        if (expectedType != ctx.MapType(typeof(object)) && current.ReturnType == ctx.MapType(typeof(object)))
                        {
                            ctx.CastFromObject(expectedType);
                        }
                        Tail.EmitWrite(ctx, null);

                        ctx.MarkLabel(@next);
                        ctx.LoadAddress(iter, enumeratorType);
                        ctx.EmitCall(moveNext);
                        ctx.BranchIfTrue(body, false);
                    }

                    if (writePacked)
                    {
                        ctx.LoadValue(token);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("EndSubItem"));
                    }                    
                }
            }
        }
#endif

#if !FEAT_IKVM

        internal static void WriteEmptyElement(IProtoSerializer tail, ProtoWriter dest, Type itemType)
        {
            tail.Write(
                Helpers.IsValueType(itemType) && Helpers.GetNullableUnderlyingType(itemType) == null
                    ? Activator.CreateInstance(itemType)
                    : null,
                dest);
        }


        public override void Write(object value, ProtoWriter dest)
        {
            SubItemToken token;
            bool writePacked = WritePacked;
            if (writePacked)
            {
                ProtoWriter.WriteFieldHeader(fieldNumber, WireType.String, dest);
                token = ProtoWriter.StartSubItem(value, dest);
                ProtoWriter.SetPackedField(fieldNumber, dest);
            }
            else
            {
                token = new SubItemToken(); // default
            }
            bool checkForNull = !SupportNull;
            WriteEmptyElement(Tail, dest, itemType);
            foreach (object subItem in (IEnumerable)value)
            {
                if (checkForNull && subItem == null) { throw new NullReferenceException(); }
                Tail.Write(subItem, dest);
            }
            if (writePacked)
            {
                ProtoWriter.EndSubItem(token, dest);
            }
        }
        public override object Read(object value, ProtoReader source)
        {
            int field = source.FieldNumber;
            object origValue = value;
            if (value == null)
            {
                value = Activator.CreateInstance(concreteType);
                ProtoReader.NoteObject(value, source);
            }
            bool isList = IsList && !SuppressIList;
            if (packedWireType != WireType.None && source.WireType == WireType.String)
            {
                SubItemToken token = ProtoReader.StartSubItem(source);

                if (!ProtoReader.HasSubValue(packedWireType, source))
                    throw new InvalidOperationException("Empty element problem");
                Tail.Read(null, source);

                if (isList)
                {
                    IList list = (IList)value;
                    while (ProtoReader.HasSubValue(packedWireType, source))
                    {
                        list.Add(Tail.Read(null, source));
                    }
                }
                else {
                    object[] args = new object[1];
                    while (ProtoReader.HasSubValue(packedWireType, source))
                    {
                        args[0] = Tail.Read(null, source);
                        add.Invoke(value, args);
                    }
                }
                ProtoReader.EndSubItem(token, source);
            }
            else
            {
                Tail.Read(null, source);

                if (isList)
                {
                    IList list = (IList)value;
                    while (source.TryReadFieldHeader(field))
                    {
                        list.Add(Tail.Read(null, source));
                    }
                }
                else
                {
                    object[] args = new object[1];
                    while (source.TryReadFieldHeader(field))
                    {
                        args[0] = Tail.Read(null, source);
                        add.Invoke(value, args);
                    }
                }
            }
            return origValue == value ? null : value;
        }
#endif

    }
}
#endif