#if FEAT_COMPILER
//#define DEBUG_COMPILE
using System;
using System.Diagnostics.CodeAnalysis;
using AltLinq;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;
using TriAxis.RunSharp;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
using IKVM.Reflection.Emit;
#else
using System.Reflection;
using System.Reflection.Emit;

#endif

namespace AqlaSerializer.Compiler
{
    class SerializerCodeGen : CodeGen
    {
        public CompilerContext ctx { get; }

        public SerializerCodeGen(CompilerContext ctx, ICodeGenContext context, bool isOwner = true)
            : base(context, isOwner)
        {
            this.ctx = ctx;
            TypeOf = new SerializerTypeOfHelpers(ctx);
            HelpersFunc = new HelpersFuncFactory(this);
            WriterFunc = new WriterFuncFactory(this);
            ReaderFunc = new ReaderFuncFactory(this);
            Writer = new WriterGen(this);
            Reader = new ReaderGen(this);
        }

        public HelpersFuncFactory HelpersFunc { get; }
        public WriterFuncFactory WriterFunc { get; }
        public ReaderFuncFactory ReaderFunc { get; }
        public WriterGen Writer { get; }
        public ReaderGen Reader { get; }

        public SerializerTypeOfHelpers TypeOf { get; }

        public void ThrowProtoException(string message)
        {
            Throw(ExpressionFactory.New(typeof(ProtoException), message));
        }

        public void ThrowNullReferenceException()
        {
            Throw(ExpressionFactory.New(typeof(NullReferenceException)));
        }

        public ContextualOperand ArgReaderWriter()
        {
            return Arg(ctx.ArgIndexReadWriter);
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    class HelpersFuncFactory
    {
        readonly SerializerCodeGen g;

        public ContextualOperand GetWireType(Operand protoTypeCode, Operand binaryDataFormat)
        {
            return g.StaticFactory.Invoke(typeof(HelpersInternal), nameof(HelpersInternal.GetWireType), protoTypeCode, binaryDataFormat);
        }

        public ContextualOperand GetTypeCode(Operand type)
        {
            return g.StaticFactory.Invoke(typeof(HelpersInternal), nameof(HelpersInternal.GetTypeCode), type);
        }

        public ContextualOperand IsAssignableFrom_bool(Operand targetType, Operand type)
        {
#if WINRT
            targetType = targetType.Invoke("GetTypeInfo");
#endif
            return targetType.Invoke("IsAssignableFrom", g.TypeMapper, type);
        }

        public HelpersFuncFactory(SerializerCodeGen g)
        {
            this.g = g;
        }
    }

    class WriterGen
    {
        readonly SerializerCodeGen g;

        public void NoteLateReference(Operand intTypeKey, Operand outObjValue)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.NoteLateReference), intTypeKey, outObjValue, g.ArgReaderWriter());
        }

        public void WriteFieldHeaderBegin(Operand intField)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeaderBegin), intField, g.ArgReaderWriter());
        }

        public void WriteFieldHeaderComplete(Operand wireType)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeaderComplete), wireType, g.ArgReaderWriter());
        }

        public void WriteFieldHeaderBeginIgnored()
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeaderBeginIgnored), g.ArgReaderWriter());
        }

        public void WriteFieldHeaderCancelBegin()
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeaderCancelBegin), g.ArgReaderWriter());
        }

        public void WriteFieldHeaderIgnored(Operand wireType)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeaderIgnored), wireType, g.ArgReaderWriter());
        }

        public void WriteFieldHeader(Operand intField, Operand wireType)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteFieldHeader), intField, wireType, g.ArgReaderWriter());
        }

        public void EndSubItem(Operand subItemToken)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.EndSubItem), subItemToken, g.ArgReaderWriter());
        }

        public void Write(string name, Operand value)
        {
            g.Invoke(typeof(ProtoWriter), "Write" + name, value, g.ArgReaderWriter());
        }

        public void WriteInt32(Operand value)
        {
            Write("Int32", value);
        }

        public void ExpectRoot()
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.ExpectRoot), g.ArgReaderWriter());
        }

        public void WriteRecursionSafeObject(Operand objValue, Operand intTypeKey)
        {
            g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.WriteRecursionSafeObject), objValue, intTypeKey, g.ArgReaderWriter());
        }

        public WriterGen(SerializerCodeGen g)
        {
            this.g = g;
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    class WriterFuncFactory
    {
        readonly SerializerCodeGen g;

        public ContextualOperand TryGetNextLateReference_bool(Operand outIntTypeKey, Operand outObjValue, Operand outIntReferenceKey)
        {
            return g.StaticFactory.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.TryGetNextLateReference), outIntTypeKey, outObjValue, outIntReferenceKey, g.ArgReaderWriter());
        }

        public ContextualOperand TryWriteBuiltinTypeValue_bool(Operand objValue, Operand protoTypeCode, Operand boolAllowSystemType)
        {
            return g.StaticFactory.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.TryWriteBuiltinTypeValue), objValue, protoTypeCode, boolAllowSystemType, g.ArgReaderWriter());
        }

        public ContextualOperand StartSubItem(Operand objInstance, Operand boolPrefixLength)
        {
            if (Helpers.IsValueType(objInstance.GetReturnType(g.TypeMapper))) objInstance = null; // no need to box value types here
            return g.StaticFactory.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.StartSubItem), objInstance, boolPrefixLength, g.ArgReaderWriter());
        }

        public ContextualOperand WireType()
        {
            return g.ArgReaderWriter().Property(nameof(ProtoWriter.WireType));
        }

        public ContextualOperand FieldNumber()
        {
            return g.ArgReaderWriter().Property(nameof(ProtoWriter.FieldNumber));
        }

        public WriterFuncFactory(SerializerCodeGen g)
        {
            this.g = g;
        }
    }

    class ReaderGen
    {
        readonly SerializerCodeGen g;

        public void NoteObject(Operand objValue)
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.NoteObject), objValue, g.ArgReaderWriter());
        }

        public void NoteReservedTrappedObject(Operand intTrapKey, Operand objValue)
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.NoteReservedTrappedObject), intTrapKey, objValue, g.ArgReaderWriter());
        }

        public void NoteLateReference(Operand intTypeKey, Operand outObjValue)
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.NoteLateReference), intTypeKey, outObjValue, g.ArgReaderWriter());
        }

        public void EndSubItem(Operand subItemToken)
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.EndSubItem), subItemToken, g.ArgReaderWriter());
        }

        public void EndSubItem(Operand subItemToken, Operand boolSkipToEnd)
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.EndSubItem), subItemToken, boolSkipToEnd, g.ArgReaderWriter());
        }

        public void SkipField()
        {
            g.Invoke(g.ArgReaderWriter(), nameof(ProtoReader.SkipField));
        }

        public void ExpectRoot()
        {
            g.Invoke(typeof(ProtoReader), nameof(ProtoReader.ExpectRoot), g.ArgReaderWriter());
        }

        public ReaderGen(SerializerCodeGen g)
        {
            this.g = g;
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    class ReaderFuncFactory
    {
        readonly SerializerCodeGen g;

        public ContextualOperand ReserveNoteObject_int()
        {
            return g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.ReserveNoteObject));
        }

        public ContextualOperand TryGetNextLateReference_bool(Operand outIntTypeKey, Operand outObjValue, Operand outIntReferenceKey)
        {
            return g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.TryGetNextLateReference), outIntTypeKey, outObjValue, outIntReferenceKey, g.ArgReaderWriter());
        }

        public ContextualOperand ReadObject(Operand objValue, Operand intTypeKey)
        {
            return g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.ReadObject), objValue, intTypeKey, g.ArgReaderWriter());
        }

        public ContextualOperand ReadTypedObject(Operand objValue, Operand intTypeKey, Operand type)
        {
            return g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.ReadTypedObject), objValue, intTypeKey, type, g.ArgReaderWriter());
        }

        public ContextualOperand TryReadBuiltinType_bool(Operand refObjValue, Operand protoTypeCode, Operand boolAllowSystemType)
        {
            return g.ArgReaderWriter().Invoke(nameof(ProtoReader.TryReadBuiltinType), refObjValue, protoTypeCode, boolAllowSystemType);
        }

        /// <summary>
        /// Returns SubItemToken
        /// </summary>
        public ContextualOperand StartSubItem()
        {
            return g.ArgReaderWriter().Invoke(nameof(ProtoReader.StartSubItem));
        }

        public ContextualOperand ReadFieldHeader_int()
        {
            return g.ArgReaderWriter().Invoke(nameof(ProtoReader.ReadFieldHeader));
        }

        public ContextualOperand TryReadFieldHeader_bool(Operand intField)
        {
            return g.ArgReaderWriter().Invoke(nameof(ProtoReader.TryReadFieldHeader), intField);
        }

        public ContextualOperand HasSubValue_bool(Operand wireType)
        {
            return g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.HasSubValue), wireType, g.ArgReaderWriter());
        }

        public ContextualOperand Read(string name)
        {
            return g.ArgReaderWriter().Invoke("Read" + name);
        }

        public ContextualOperand ReadInt32()
        {
            return Read("Int32");
        }

        public ContextualOperand WireType()
        {
            return g.ArgReaderWriter().Property(nameof(ProtoReader.WireType));
        }

        public ContextualOperand FieldNumber()
        {
            return g.ArgReaderWriter().Property(nameof(ProtoReader.FieldNumber));
        }

        public ReaderFuncFactory(SerializerCodeGen g)
        {
            this.g = g;
        }
    }

    class SerializerTypeOfHelpers
    {
        public SerializerTypeOfHelpers(CompilerContext ctx)
        {
            TypeOfReferenceKey = ctx.MapType(typeof(int));
            TypeOfTypeKey = ctx.MapType(typeof(int));
            TypeOfTrapKey = ctx.MapType(typeof(int));
            TypeOfSubItemToken = ctx.MapType(typeof(SubItemToken));
        }

        public Type TypeOfReferenceKey { get; }
        public Type TypeOfTypeKey { get; }
        public Type TypeOfTrapKey { get; }
        public Type TypeOfSubItemToken { get; }
    }
}

#endif