// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
#if FEAT_COMPILER
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
    sealed class ArrayDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            _listHelpers.Write(value, null, ((IList)value)?.Count, null, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            Array result = null;
            BasicList list = null;
            int reservedTrap = -1;
            int index;
            Action<object> addMethod = null;
            
            _listHelpers.Read(
                null,
                length =>
                    {
                        if (length >= 0)
                        {
                            int oldLen;
                            result = Read_CreateInstance(value, length.Value, null, out oldLen, source);
                            index = oldLen;
                            addMethod = v => result.SetValue(v, index++);
                        }
                        else
                        {
                            reservedTrap = ProtoReader.ReserveNoteObject(source);
                            list = new BasicList();
                            addMethod = v => list.Add(v);
                        }
                    },
                el => addMethod(el),
                source);

            if (result == null)
            {
                int oldLen;
                result = Read_CreateInstance(value, list.Count, reservedTrap, out oldLen, source);
                list.CopyTo(result, oldLen);
            }
            return result;
        }

        Array Read_CreateInstance(object value, int appendCount, int? reservedTrap, out int oldLen, ProtoReader source)
        {
            oldLen = AppendToCollection ? (((Array)value)?.Length ?? 0) : 0;
            Array result = Array.CreateInstance(_itemType, oldLen + appendCount);
            if (reservedTrap.HasValue)
                ProtoReader.NoteReservedTrappedObject(reservedTrap.Value, result, source);
            else
                ProtoReader.NoteObject(result, source);
            if (oldLen != 0) ((Array)value).CopyTo(result, 0);
            return result;
        }
#endif

        readonly ListHelpers _listHelpers;
        readonly bool _writePacked;
        readonly WireType _packedWireTypeForRead;
        readonly Type _arrayType; // this is, for example, typeof(int[])
        readonly bool _overwriteList;
        readonly Type _itemType; // this is, for example, typeof(int[])
        readonly bool _protoCompatibility;


        bool AppendToCollection => !_overwriteList;
        
        public ArrayDecorator(TypeModel model, IProtoSerializerWithWireType tail, bool writePacked, WireType packedWireTypeForRead, Type arrayType, bool overwriteList, bool protoCompatibility)
            : base(tail)
        {
            Helpers.DebugAssert(arrayType != null, "arrayType should be non-null");
            Helpers.DebugAssert(arrayType.IsArray && arrayType.GetArrayRank() == 1, "should be single-dimension array; " + arrayType.FullName);
            _itemType = tail.ExpectedType;
            
            Helpers.DebugAssert(Tail.ExpectedType != model.MapType(typeof(byte)), "Should have used BlobSerializer");
            if (!ListDecorator.CanPack(packedWireTypeForRead))
            {
                if (writePacked) throw new InvalidOperationException("Only simple data-types can use packed encoding");
                _packedWireTypeForRead = WireType.None;
            }
            else
                _packedWireTypeForRead = packedWireTypeForRead;
            _writePacked = writePacked;
            _arrayType = arrayType;
            _overwriteList = overwriteList;
            _protoCompatibility = protoCompatibility;
            _listHelpers = new ListHelpers(_writePacked, _packedWireTypeForRead, _protoCompatibility, Tail);
        }

        public override Type ExpectedType { get { return _arrayType; } }
        public override bool RequiresOldValue { get { return AppendToCollection; } }
        public override bool ReturnsValue { get { return true; } }
#if FEAT_COMPILER
        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
#if false
// int i and T[] arr
            using (Compiler.Local arr = ctx.GetLocalWithValue(_arrayType, valueFrom))
            using (Compiler.Local i = new AqlaSerializer.Compiler.Local(ctx, ctx.MapType(typeof(int))))
            {
                bool writePacked = (_writePacked) != 0;
                using (Compiler.Local token = writePacked ? new Compiler.Local(ctx, ctx.MapType(typeof(SubItemToken))) : null)
                {
                    Type mappedWriter = ctx.MapType(typeof(ProtoWriter));
                    if (writePacked)
                    {
                        ctx.LoadValue(fieldNumber);
                        ctx.LoadValue((int)WireType.String);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(mappedWriter.GetMethod("WriteFieldHeader"));

                        ctx.LoadValue(arr);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(mappedWriter.GetMethod("StartSubItem"));
                        ctx.StoreValue(token);

                        ctx.LoadValue(fieldNumber);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(mappedWriter.GetMethod("SetPackedField"));
                    }
                    ListDecorator.EmitWriteEmptyElement(ctx, _itemType, Tail, false);
                    
                    EmitWriteArrayLoop(ctx, i, arr);

                    if (writePacked)
                    {
                        ctx.LoadValue(token);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(mappedWriter.GetMethod("EndSubItem"));
                    }
                }
            }
#endif

        }

        private void EmitWriteArrayLoop(Compiler.CompilerContext ctx, Compiler.Local i, Compiler.Local arr)
        {
#if false
// i = 0
            ctx.LoadValue(0);
            ctx.StoreValue(i);

            // range test is last (to minimise branches)
            Compiler.CodeLabel loopTest = ctx.DefineLabel(), processItem = ctx.DefineLabel();
            ctx.Branch(loopTest, false);
            ctx.MarkLabel(processItem);

            // {...}
            ctx.LoadArrayValue(arr, i);
            if (SupportNull)
            {
                Tail.EmitWrite(ctx, null);
            }
            else
            {
                ctx.WriteNullCheckedTail(_itemType, Tail, null);
            }

            // i++
            ctx.LoadValue(i);
            ctx.LoadValue(1);
            ctx.Add();
            ctx.StoreValue(i);

            // i < arr.Length
            ctx.MarkLabel(loopTest);
            ctx.LoadValue(i);
            ctx.LoadLength(arr, false);
            ctx.BranchIfLess(processItem, false);
#endif

        }
#endif

#if FEAT_COMPILER
        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
#if false
Type listType;
#if NO_GENERICS
            listType = typeof(BasicList);
#else
            listType = ctx.MapType(typeof(System.Collections.Generic.List<>)).MakeGenericType(_itemType);
#endif
            Type expected = ExpectedType;
            using (Compiler.Local oldArr = AppendToCollection ? ctx.GetLocalWithValue(expected, valueFrom) : null)
            using (Compiler.Local newArr = new Compiler.Local(ctx, expected))
            using (Compiler.Local list = new Compiler.Local(ctx, listType))
            using (Compiler.Local reservedTrap = new Local(ctx, ctx.MapType(typeof(int)))) 
            using (Compiler.Local refLocalToNoteObject = new Local(ctx, ctx.MapType(typeof(object))))
            {
                ctx.EmitCallReserveNoteObject();
                ctx.StoreValue(reservedTrap);

                ctx.EmitCtor(listType);
                ctx.StoreValue(list);
                
                ListDecorator.EmitReadList(ctx, list, Tail, listType.GetMethod("Add"), packedWireType, false);

                // leave this "using" here, as it can share the "FieldNumber" local with EmitReadList
                using(Compiler.Local oldLen = AppendToCollection ? new AqlaSerializer.Compiler.Local(ctx, ctx.MapType(typeof(int))) : null) {
                    Type[] copyToArrayInt32Args = new Type[] { ctx.MapType(typeof(Array)), ctx.MapType(typeof(int)) };

                    if (AppendToCollection)
                    {
                        ctx.LoadLength(oldArr, true);
                        ctx.CopyValue();
                        ctx.StoreValue(oldLen);

                        ctx.LoadAddress(list, listType);
                        ctx.LoadValue(listType.GetProperty("Count"));
                        ctx.Add();
                        ctx.CreateArray(_itemType, null); // length is on the stack
                        ctx.StoreValue(newArr);

                        ctx.LoadValue(oldLen);
                        Compiler.CodeLabel nothingToCopy = ctx.DefineLabel();
                        ctx.BranchIfFalse(nothingToCopy, true);
                        ctx.LoadValue(oldArr);
                        ctx.LoadValue(newArr);
                        ctx.LoadValue(0); // index in target

                        ctx.EmitCall(expected.GetMethod("CopyTo", copyToArrayInt32Args));

                        ctx.MarkLabel(nothingToCopy);

                        ctx.LoadValue(list);
                        ctx.LoadValue(newArr);
                        ctx.LoadValue(oldLen);
                        
                    }
                    else
                    {
                        ctx.LoadAddress(list, listType);
                        ctx.LoadValue(listType.GetProperty("Count"));
                        ctx.CreateArray(_itemType, null);
                        ctx.StoreValue(newArr);

                        ctx.LoadAddress(list, listType);
                        ctx.LoadValue(newArr);
                        ctx.LoadValue(0);
                    }

                    copyToArrayInt32Args[0] = expected; // // prefer: CopyTo(T[], int)
                    MethodInfo copyTo = listType.GetMethod("CopyTo", copyToArrayInt32Args);
                    if (copyTo == null)
                    { // fallback: CopyTo(Array, int)
                        copyToArrayInt32Args[1] = ctx.MapType(typeof(Array));
                        copyTo = listType.GetMethod("CopyTo", copyToArrayInt32Args);
                    }
                    ctx.EmitCall(copyTo);
                }

                ctx.LoadValue(newArr);
                ctx.CastToObject(ctx.MapType(expected));
                ctx.StoreValue(refLocalToNoteObject);
                
                ctx.LoadValue(reservedTrap);
                ctx.LoadAddress(refLocalToNoteObject, refLocalToNoteObject.Type);
                ctx.EmitCallNoteReservedTrappedObject();
                
                
                ctx.LoadValue(newArr);
            }
#endif

        }
        
#endif

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return false;
        }

        public bool CanCreateInstance()
        {
            return true;
        }

#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            Array r = Array.CreateInstance(_itemType, 0);
            ProtoReader.NoteObject(r, source);
            return r;
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {

        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        {

        }

        public void EmitCreateInstance(CompilerContext ctx)
        {
#if NO_GENERICS
            var listType = typeof(BasicList);
#else
            var listType = ctx.MapType(typeof(System.Collections.Generic.List<>)).MakeGenericType(_itemType);
#endif
            ctx.EmitCtor(listType);
        }
#endif

    }
}
#endif