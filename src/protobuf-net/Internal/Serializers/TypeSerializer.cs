using ProtoBuf.Compiler;
using System.Text;
using AltLinq; using System.Linq;
using AqlaSerializer.Meta;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
using TriAxis.RunSharp;
#endif

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
using System.Collections.Generic;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Diagnostics;
using ProtoBuf.Serializers;
using System.Runtime.Serialization;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class TypeSerializer : IProtoTypeSerializer
    {
        public static IProtoTypeSerializer Create(Type forType, int[] fieldNumbers, IRuntimeProtoSerializerNode[] serializers, MethodInfo[] baseCtorCallbacks, bool isRootType, bool useConstructor, bool assertKnownType, CallbackSet callbacks, Type constructType, MethodInfo factory, Type rootType, SerializerFeatures features)
        {
            var obj = (TypeSerializer)(rootType is object
                ? Activator.CreateInstance(typeof(InheritanceTypeSerializer<,>).MakeGenericType(rootType, forType), nonPublic: true)
                : Activator.CreateInstance(typeof(TypeSerializer<>).MakeGenericType(forType), nonPublic: true));
            
            obj.Init(fieldNumbers, serializers, baseCtorCallbacks, isRootType, useConstructor, assertKnownType, callbacks, constructType, factory, features);
            return (IProtoTypeSerializer)obj;
        }
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.GroupSerializer(this))
            {
                for (int i = 0; i < _serializers.Length; i++)
                {
                    IProtoSerializerWithWireType ser = _serializers[i];
                    if (ser.ExpectedType != ExpectedType)
                    {
                        using (builder.Field(_fieldNumbers[i], "SubType"))
                            ser.WriteDebugSchema(builder);
                    }
                }
                for (int i = 0; i < _serializers.Length; i++)
                {
                    IProtoSerializerWithWireType ser = _serializers[i];
                    if (ser.ExpectedType == ExpectedType)
                    {
                        using (builder.Field(_fieldNumbers[i]))
                            ser.WriteDebugSchema(builder);
                    }
                }
            }
        }
        
        public bool DemandWireTypeStabilityStatus() => true;
        abstract internal void Init(int[] fieldNumbers, IRuntimeProtoSerializerNode[] serializers, MethodInfo[] baseCtorCallbacks, bool isRootType, bool useConstructor, bool assertKnownType, CallbackSet callbacks, Type constructType, MethodInfo factory, SerializerFeatures features);

        public IProtoTypeSerializer GetSubTypeSerializer(int number)
        {
            return (IProtoTypeSerializer)_serializers[Array.IndexOf(_fieldNumbers, number)];
        }

        private readonly IProtoSerializerWithWireType[] _serializers;
        private bool _isRootType, _useConstructor, _isExtensible, _hasConstructor;
        public bool CanCreateInstance { get; set; } = true;
        public bool AllowInheritance { get; set; } = true;
        readonly bool _prefixLength;

        public TypeSerializer(TypeModel model, Type forType, int[] fieldNumbers, IProtoSerializerWithWireType[] serializers, MethodInfo[] baseCtorCallbacks, bool isRootType, bool useConstructor, CallbackSet callbacks, Type constructType, MethodInfo factory, bool prefixLength)
        {
            Helpers.DebugAssert(forType != null);
            Helpers.DebugAssert(fieldNumbers != null);
            Helpers.DebugAssert(serializers != null);
            Helpers.DebugAssert(fieldNumbers.Length == serializers.Length);
            
            Helpers.Sort(fieldNumbers, serializers);
            bool hasSubTypes = false;
            for (int i = 1; i < fieldNumbers.Length; i++)
            {
                if (fieldNumbers[i] == fieldNumbers[i - 1]) throw new InvalidOperationException("Duplicate field-number detected; " +
                           fieldNumbers[i].ToString() + " on: " + forType.FullName+", forgot to specify SerializableType.ImplicitFirstTag?");
                if (!hasSubTypes && serializers[i].ExpectedType != forType)
                {
                    hasSubTypes = true;
                }
            }
            this.ExpectedType = forType;
            this._factory = factory;
            _prefixLength = prefixLength;
#if WINRT
            this.typeInfo = forType.GetTypeInfo();
#endif
            if (constructType == null)
            {
                constructType = forType;
            }
            else
            {
#if WINRT
                if (!typeInfo.IsAssignableFrom(constructType.GetTypeInfo()))
#else
                if (!forType.IsAssignableFrom(constructType))
#endif
                {
                    throw new InvalidOperationException(forType.FullName + " cannot be assigned from " + constructType.FullName);
                }
            }
            this._constructType = constructType;
            this._serializers = serializers;
            this._fieldNumbers = fieldNumbers;
            this._callbacks = callbacks;
            this._isRootType = isRootType;
            this._useConstructor = useConstructor;

            if (baseCtorCallbacks != null && baseCtorCallbacks.Length == 0) baseCtorCallbacks = null;
            this._baseCtorCallbacks = baseCtorCallbacks;
#if !NO_GENERICS
            if (Helpers.GetNullableUnderlyingType(forType) != null)
            {
                throw new ArgumentException("Cannot create a TypeSerializer for nullable types", nameof(forType));
            }
#endif

#if WINRT
            if (iextensible.IsAssignableFrom(typeInfo))
            {
                if (typeInfo.IsValueType || !isRootType || hasSubTypes)
#else
            if (model.MapType(iextensible).IsAssignableFrom(forType))
            {
                if (forType.IsValueType || !isRootType || hasSubTypes)
#endif
                {
                    throw new NotSupportedException("IExtensible is not supported in structs or classes with inheritance");
                }
                _isExtensible = true;
            }
#if WINRT
            TypeInfo constructTypeInfo = constructType.GetTypeInfo();
            _hasConstructor = !constructTypeInfo.IsAbstract && Helpers.GetConstructor(constructTypeInfo, Helpers.EmptyTypes, true) != null;
#else
            _hasConstructor = !constructType.IsAbstract && Helpers.GetConstructor(constructType, Helpers.EmptyTypes, true) != null;
#endif
            if (constructType != forType && useConstructor && !_hasConstructor)
            {
                throw new ArgumentException("The supplied default implementation cannot be created: " + constructType.FullName, nameof(constructType));
            }
        }

        public IProtoSerializerWithWireType GetMoreSpecificSerializer(object value)
        {
            int fieldNumber;
            IProtoSerializerWithWireType serializer;
            GetMoreSpecificSerializer(value, out serializer, out fieldNumber);
            return serializer;
        }
        
        public bool CanCancelWriting { get; }


#if FEAT_COMPILER
        public bool EmitReadReturnsValue { get; } = false; // updates field directly        

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                Type expected = ExpectedType;
                using (Compiler.Local loc = ctx.GetLocalWithValue(expected, valueFrom))
                using (Compiler.Local token = ctx.Local(typeof(SubItemToken)))
                {
                    Operand prefixLength = _prefixLength;
                    if (_isRootType) prefixLength = g.WriterFunc.TakeIsExpectingRootType_bool() || _prefixLength;
                    g.Assign(token, g.WriterFunc.StartSubItem(loc, prefixLength));
                    // pre-callbacks
                    EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeSerialize);

                    Compiler.CodeLabel startFields = ctx.DefineLabel();
                    // inheritance
                    if (CanHaveInheritance)
                    {
                        for (int i = 0; i < _serializers.Length; i++)
                        {
                            IProtoSerializer ser = _serializers[i];
                            Type serType = ser.ExpectedType;
                            if (serType != ExpectedType)
                            {
                                Compiler.CodeLabel ifMatch = ctx.DefineLabel(), nextTest = ctx.DefineLabel();
                                ctx.LoadValue(loc);
                                ctx.TryCast(serType);
                                ctx.CopyValue();
                                ctx.BranchIfTrue(ifMatch, true);
                                ctx.DiscardValue();
                                ctx.Branch(nextTest, true);
                                ctx.MarkLabel(ifMatch);
                                ctx.G.Writer.WriteFieldHeaderBegin(_fieldNumbers[i]);
                                ser.EmitWrite(ctx, null);
                                ctx.Branch(startFields, false);
                                ctx.MarkLabel(nextTest);
                            }
                        }


                        if (_constructType != null && _constructType != ExpectedType)
                        {
                            using (Compiler.Local actualType = new Compiler.Local(ctx, ctx.MapType(typeof(System.Type))))
                            {
                                // would have jumped to "fields" if an expected sub-type, so two options:
                                // a: *exactly* that type, b: an *unexpected* type
                                ctx.LoadValue(loc);
                                ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                                ctx.CopyValue();
                                ctx.StoreValue(actualType);
                                ctx.LoadValue(ExpectedType);
                                ctx.BranchIfEqual(startFields, true);

                                ctx.LoadValue(actualType);
                                ctx.LoadValue(_constructType);
                                ctx.BranchIfEqual(startFields, true);
                            }
                        }
                        else
                        {
                            // would have jumped to "fields" if an expected sub-type, so two options:
                            // a: *exactly* that type, b: an *unexpected* type
                            ctx.LoadValue(loc);
                            ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                            ctx.LoadValue(ExpectedType);
                            ctx.BranchIfEqual(startFields, true);
                        }
                        // unexpected, then... note that this *might* be a proxy, which
                        // is handled by ThrowUnexpectedSubtype
                        ctx.LoadValue(ExpectedType);
                        ctx.LoadValue(loc);
                        ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                        ctx.EmitCall(
                            ctx.MapType(typeof(TypeModel)).GetMethod(
                                "ThrowUnexpectedSubtype",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));

                    }
                    // fields

                    ctx.MarkLabel(startFields);
                    for (int i = 0; i < _serializers.Length; i++)
                    {
                        IProtoSerializer ser = _serializers[i];
                        if (ser.ExpectedType == ExpectedType)
                        {
                            ctx.G.Writer.WriteFieldHeaderBegin(_fieldNumbers[i]);
                            ser.EmitWrite(ctx, loc);
                        }
                    }

                    // extension data
                    if (_isExtensible)
                    {
                        ctx.LoadValue(loc);
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("AppendExtensionData"));
                    }
                    // post-callbacks
                    EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.AfterSerialize);
                    g.Writer.EndSubItem(token);
                }
            }
        }

        static void EmitInvokeCallback(Compiler.CompilerContext ctx, MethodInfo method, bool copyValue, Type constructType, Type type)
        {
            if (method != null)
            {
                if (copyValue) ctx.CopyValue(); // assumes the target is on the stack, and that we want to *retain* it on the stack
                ParameterInfo[] parameters = method.GetParameters();
                bool handled = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == ctx.MapType(typeof(SerializationContext)))
                    {
                        ctx.LoadSerializationContext();
                    }
                    else if (parameterType == ctx.MapType(typeof(System.Type)))
                    {
                        ctx.LoadValue(constructType ?? type);
                    }
#if PLAT_BINARYFORMATTER
                    else if (parameterType == ctx.MapType(typeof(System.Runtime.Serialization.StreamingContext)))
                    {
                        ctx.LoadSerializationContext();
                        MethodInfo op = ctx.MapType(typeof(SerializationContext)).GetMethod("op_Implicit", new Type[] { ctx.MapType(typeof(SerializationContext)) });
                        if (op != null)
                        { // it isn't always! (framework versions, etc)
                            ctx.EmitCall(op);
                            handled = true;
                        }
                    }
#endif
                    else
                    {
                        handled = false;
                    }
                }
                if (handled)
                {
                    ctx.EmitCall(method);
                    if (constructType != null)
                    {
                        if (method.ReturnType == ctx.MapType(typeof(object)))
                        {
                            ctx.CastFromObject(type);
                        }
                    }
                }
                else
                {
                    throw Meta.CallbackSet.CreateInvalidCallbackSignature(method);
                }
            }
        }
        private void EmitCallbackIfNeeded(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            Helpers.DebugAssert(!valueFrom.IsNullRef());
            if (_isRootType && ((IProtoTypeSerializer)this).HasCallbacks(callbackType))
            {
                ((IProtoTypeSerializer)this).EmitCallback(ctx, valueFrom, callbackType);
            }
        }
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                bool actuallyHasInheritance = false;
                if (CanHaveInheritance)
                {

                    for (int i = 0; i < _serializers.Length; i++)
                    {
                        IProtoSerializer ser = _serializers[i];
                        if (ser.ExpectedType != ExpectedType && ((ser as IProtoTypeSerializer)?.HasCallbacks(callbackType) ?? false))
                        {
                            actuallyHasInheritance = true;
                        }
                    }
                }

                Helpers.DebugAssert(((IProtoTypeSerializer)this).HasCallbacks(callbackType), "Shouldn't be calling this if there is nothing to do");
                MethodInfo method = _callbacks?[callbackType];
                if (method == null && !actuallyHasInheritance)
                {
                    return;
                }
                ctx.LoadAddress(valueFrom, ExpectedType);
                EmitInvokeCallback(ctx, method, actuallyHasInheritance, null, ExpectedType);

                if (actuallyHasInheritance)
                {
                    Compiler.CodeLabel @break = ctx.DefineLabel();
                    for (int i = 0; i < _serializers.Length; i++)
                    {
                        IProtoSerializer ser = _serializers[i];
                        IProtoTypeSerializer typeser;
                        Type serType = ser.ExpectedType;
                        if (serType != ExpectedType &&
                            (typeser = (IProtoTypeSerializer)ser).HasCallbacks(callbackType))
                        {
                            Compiler.CodeLabel ifMatch = ctx.DefineLabel(), nextTest = ctx.DefineLabel();
                            ctx.CopyValue();
                            ctx.TryCast(serType);
                            ctx.CopyValue();
                            ctx.BranchIfTrue(ifMatch, true);
                            ctx.DiscardValue();
                            ctx.Branch(nextTest, false);
                            ctx.MarkLabel(ifMatch);
                            typeser.EmitCallback(ctx, null, callbackType);
                            ctx.Branch(@break, false);
                            ctx.MarkLabel(nextTest);
                        }
                    }
                    ctx.MarkLabel(@break);
                    ctx.DiscardValue();
                }
            }
        }
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                Helpers.DebugAssert(!valueFrom.IsNullRef());

                using (Compiler.Local loc = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                using (Compiler.Local token = ctx.Local(typeof(SubItemToken)))
                using (Compiler.Local fieldNumber = new Compiler.Local(ctx, ctx.MapType(typeof(int))))
                {
                    g.Assign(token, g.ReaderFunc.StartSubItem());
                    // pre-callbacks
                    if (HasCallbacks(TypeModel.CallbackType.BeforeDeserialize))
                    {
                        if (ExpectedType.IsValueType)
                        {
                            EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeDeserialize);
                        }
                        else
                        { // could be null
                            Compiler.CodeLabel callbacksDone = ctx.DefineLabel();
                            ctx.LoadValue(loc);
                            ctx.BranchIfFalse(callbacksDone, false);
                            EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeDeserialize);
                            ctx.MarkLabel(callbacksDone);
                        }
                    }

                    Compiler.CodeLabel @continue = ctx.DefineLabel(), processField = ctx.DefineLabel();
                    ctx.Branch(@continue, false);

                    ctx.MarkLabel(processField);
                    foreach (BasicList.Group group in BasicList.GetContiguousGroups(_fieldNumbers, _serializers))
                    {
                        Compiler.CodeLabel tryNextField = ctx.DefineLabel();
                        int groupItemCount = group.Items.Count;
                        if (groupItemCount == 1)
                        {
                            // discreet group; use an equality test
                            ctx.LoadValue(fieldNumber);
                            ctx.LoadValue(group.First);
                            Compiler.CodeLabel processThisField = ctx.DefineLabel();
                            ctx.BranchIfEqual(processThisField, true);
                            ctx.Branch(tryNextField, false);
                            WriteFieldHandler(ctx, loc, processThisField, @continue, (IProtoSerializer)group.Items[0]);
                        }
                        else
                        { // implement as a jump-table-based switch
                            ctx.LoadValue(fieldNumber);
                            ctx.LoadValue(group.First);
                            ctx.Subtract(); // jump-tables are zero-based
                            Compiler.CodeLabel[] jmp = new Compiler.CodeLabel[groupItemCount];
                            for (int i = 0; i < groupItemCount; i++)
                            {
                                jmp[i] = ctx.DefineLabel();
                            }
                            ctx.Switch(jmp);
                            // write the default...
                            ctx.Branch(tryNextField, false);
                            for (int i = 0; i < groupItemCount; i++)
                            {
                                WriteFieldHandler(ctx, loc, jmp[i], @continue, (IProtoSerializer)group.Items[i]);
                            }
                        }
                        ctx.MarkLabel(tryNextField);
                    }

                    EmitCreateIfNull(ctx, loc);
                    ctx.LoadReaderWriter();
                    if (_isExtensible)
                    {
                        ctx.LoadValue(loc);
                        ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("AppendExtensionData"));
                    }
                    else
                    {
                        ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("SkipField"));
                    }

                    ctx.MarkLabel(@continue);
                    ctx.EmitBasicRead("ReadFieldHeader", ctx.MapType(typeof(int)));
                    ctx.CopyValue();
                    ctx.StoreValue(fieldNumber);
                    ctx.LoadValue(0);
                    ctx.BranchIfGreater(processField, false);

                    EmitCreateIfNull(ctx, loc);
                    // post-callbacks
                    EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.AfterDeserialize);

                    g.Reader.EndSubItem(token);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(loc);
                }
            }
        }

        private void WriteFieldHandler(
            Compiler.CompilerContext ctx, Compiler.Local loc,
            Compiler.CodeLabel handler, Compiler.CodeLabel @continue, IProtoSerializer serializer)
        {
            ctx.MarkLabel(handler);
            Type serType = serializer.ExpectedType;
            if (serType == ExpectedType)
            {
                // emit create if null
                Helpers.DebugAssert(!loc.IsNullRef());
                if (!ExpectedType.IsValueType && CanCreateInstance)
                {
                    Compiler.CodeLabel afterIf = ctx.DefineLabel();
                    Compiler.CodeLabel ifContent = ctx.DefineLabel();

                    // if != null && of correct type
                    ctx.LoadValue(loc);
                    ctx.BranchIfFalse(ifContent, false);
                    ctx.LoadValue(loc);
                    ctx.TryCast(ExpectedType);
                    ctx.BranchIfFalse(ifContent, false);
                    ctx.Branch(afterIf, false);
                    {
                        ctx.MarkLabel(ifContent);

                        ((IProtoTypeSerializer)this).EmitCreateInstance(ctx);

                        if (_callbacks != null) EmitInvokeCallback(ctx, _callbacks.BeforeDeserialize, true, null, ExpectedType);
                        ctx.StoreValue(loc);
                    }
                    ctx.MarkLabel(afterIf);
                }

                serializer.EmitRead(ctx, loc);
            }
            else
            {
                ctx.LoadValue(loc);
                if (ExpectedType.IsValueType || !serializer.EmitReadReturnsValue)
                    ctx.Cast(serType);
                else
                    ctx.TryCast(serType); // default value can be another inheritance branch
                serializer.EmitRead(ctx, null);
            }

            if (serializer.EmitReadReturnsValue)
            {   // update the variable
                ctx.StoreValue(loc);
            }
            ctx.Branch(@continue, false); // "continue"
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                // different ways of creating a new instance
                bool callNoteObject = true;
                if (_factory != null)
                {
                    EmitInvokeCallback(ctx, _factory, false, _constructType, ExpectedType);
                }
                else if (!_useConstructor || (_useConstructor && !_hasConstructor))
                { // DataContractSerializer style
                    ctx.LoadValue(_constructType);
                    ctx.EmitCall(ctx.MapType(typeof(BclHelpers)).GetMethod("GetUninitializedObject"));
                    ctx.Cast(ExpectedType);
                }
                else if (_constructType.IsClass && _hasConstructor)
                { // XmlSerializer style
                    ctx.EmitCtor(_constructType);
                }
                else
                {
                    ctx.LoadValue(ExpectedType);
                    ctx.EmitCall(
                        ctx.MapType(typeof(TypeModel)).GetMethod(
                            "ThrowCannotCreateInstance",
                            BindingFlags.Static | BindingFlags.Public));
                    ctx.LoadNullRef();
                    callNoteObject = false;
                }
                if (callNoteObject)
                {
                    // track root object creation
                    ctx.CopyValue();
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(
                        ctx.MapType(typeof(ProtoReader)).GetMethod(
                            "NoteObject",
                            BindingFlags.Static | BindingFlags.Public));
                }
                if (_baseCtorCallbacks != null)
                {
                    for (int i = 0; i < _baseCtorCallbacks.Length; i++)
                    {
                        EmitInvokeCallback(ctx, _baseCtorCallbacks[i], true, null, ExpectedType);
                    }
                }
            }
        }
        private void EmitCreateIfNull(Compiler.CompilerContext ctx, Compiler.Local storage)
        {
            Helpers.DebugAssert(!storage.IsNullRef());
            if (!ExpectedType.IsValueType && CanCreateInstance)
            {
                Compiler.CodeLabel afterNullCheck = ctx.DefineLabel();
                ctx.LoadValue(storage);
                ctx.BranchIfTrue(afterNullCheck, false);

                ((IProtoTypeSerializer)this).EmitCreateInstance(ctx);

                if (_callbacks != null) EmitInvokeCallback(ctx, _callbacks.BeforeDeserialize, true, null, ExpectedType);
                ctx.StoreValue(storage);
                ctx.MarkLabel(afterNullCheck);
            }
        }
#endif
    }

    internal sealed class InheritanceTypeSerializer<TBase, T> : TypeSerializer<T>, ISubTypeSerializer<T>
        where TBase : class
        where T : class, TBase
    {
        public override bool HasInheritance => true;

        internal override Type BaseType => typeof(TBase);

        public override void Write(ref ProtoWriter.State state, T value)
            => state.WriteBaseType<TBase>(value);
        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            var token = ProtoReader.StartSubItem(source);
            if (_isRootType && value != null) { Callback(value, TypeModel.CallbackType.BeforeDeserialize, source.Context); }
            int fieldNumber, lastFieldNumber = 0, lastFieldIndex = 0;

            //Helpers.DebugWriteLine(">> Reading fields for " + forType.FullName);
            while ((fieldNumber = source.ReadFieldHeader()) > 0)
            {
                bool fieldHandled = false;
                if (fieldNumber < lastFieldNumber)
                {
                    lastFieldNumber = lastFieldIndex = 0;
                }
                for (int i = lastFieldIndex; i < _fieldNumbers.Length; i++)
                {
                    if (_fieldNumbers[i] == fieldNumber)
                    {
                        IProtoSerializer ser = _serializers[i];
                        //Helpers.DebugWriteLine(": " + ser.ToString());
                        Type serType = ser.ExpectedType;
                        if (value == null ||  !Helpers.IsInstanceOfType(ExpectedType, value))
                        {
                            if (serType == ExpectedType && CanCreateInstance) value = CreateInstance(source, true);
                        }
                        value = ser.Read(value, source);
                        
                        lastFieldIndex = i;
                        lastFieldNumber = fieldNumber;
                        fieldHandled = true;
                        break;
                    }
                }
                if (!fieldHandled)
                {
                    //Helpers.DebugWriteLine(": [" + fieldNumber + "] (unknown)");
                    if (value == null) value = CreateInstance(source, true);
                    if (_isExtensible)
                    {
                        source.AppendExtensionData((IExtensible)value);
                    }
                    else
                    {
                        source.SkipField();
                    }
                }
            }
            //Helpers.DebugWriteLine("<< Reading fields for " + forType.FullName);
            if (value == null) value = CreateInstance(source, true);
            if (_isRootType) { Callback(value, TypeModel.CallbackType.AfterDeserialize, source.Context); }
            ProtoReader.EndSubItem(token, source);
            return value;
        }

        T ISubTypeSerializer<T>.ReadSubType(ref ProtoReader.State state, SubTypeState<T> value)
        {
            value.OnBeforeDeserialize(_subTypeOnBeforeDeserialize);
            DeserializeBody(ref state, ref value, (ref SubTypeState<T> s) => s.Value, (ref SubTypeState<T> s, T v) => s.Value = v);
            var val = value.Value;
            Callback(ref val, TypeModel.CallbackType.AfterDeserialize, state.Context);
            return val;
        }

        void ISubTypeSerializer<T>.WriteSubType(ref ProtoWriter.State state, T value)
            => SerializeImpl(ref state, value);

        public override void EmitReadRoot(CompilerContext context, Local valueFrom)
        {   // => (T)((IProtoSubTypeSerializer<TBase>)this).ReadSubType(reader, ref state, SubTypeState<TBase>.Create<T>(state.Context, value));
            // or
            // => state.ReadBaseType<TBase, T>(value, this);
            if (context.IsService)
            {
                using var tmp = context.GetLocalWithValue(typeof(T), valueFrom);
                context.LoadSelfAsService<ISubTypeSerializer<TBase>, TBase>(default, default);
                context.LoadState();

                // sub-state
                context.LoadSerializationContext(typeof(ISerializationContext));
                context.LoadValue(tmp);
                context.EmitCall(typeof(SubTypeState<TBase>)
                    .GetMethod(nameof(SubTypeState<string>.Create), BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(typeof(T)));
                context.EmitCall(typeof(ISubTypeSerializer<TBase>)
                    .GetMethod(nameof(ISubTypeSerializer<string>.ReadSubType), BindingFlags.Public | BindingFlags.Instance));
                if (typeof(T) != typeof(TBase)) context.Cast(typeof(T));
            }
            else
            {
                context.LoadState();
                context.LoadValue(valueFrom);
                context.LoadSelfAsService<ISubTypeSerializer<TBase>, TBase>(default, default);
                context.EmitCall(typeof(ProtoReader.State).GetMethod(nameof(ProtoReader.State.ReadBaseType), BindingFlags.Public | BindingFlags.Instance)
                    .MakeGenericMethod(typeof(TBase), typeof(T)));
            }
        }
        public override void EmitWriteRoot(CompilerContext context, Local valueFrom)
        {   // => ((IProtoSubTypeSerializer<TBase>)this).WriteSubType(writer, ref state, value);
            // or
            // => ProtoWriter.WriteBaseType<TBase>(value, writer, ref state, this);

            using var tmp = context.GetLocalWithValue(typeof(T), valueFrom);
            if (context.IsService)
            {
                context.LoadSelfAsService<ISubTypeSerializer<TBase>, TBase>(default, default);
                context.LoadState();
                context.LoadValue(tmp);
                context.EmitCall(typeof(ISubTypeSerializer<TBase>)
                    .GetMethod(nameof(ISubTypeSerializer<string>.WriteSubType), BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                context.LoadState();
                context.LoadValue(tmp);
                context.LoadSelfAsService<ISubTypeSerializer<TBase>, TBase>(default, default);
                context.EmitCall(typeof(ProtoWriter).GetMethod(nameof(ProtoWriter.State.WriteBaseType), BindingFlags.Public | BindingFlags.Instance)
                    .MakeGenericMethod(typeof(TBase)));
            }
        }

        public override bool IsSubType => true;
    }
    internal class TypeSerializer<T> : TypeSerializer, ISerializer<T>, IFactory<T>, IProtoTypeSerializer
    {
        public virtual bool HasInheritance => false;
        public virtual void EmitReadRoot(CompilerContext context, Local valueFrom)
            => ((IRuntimeProtoSerializerNode)this).EmitRead(context, valueFrom);
        public virtual void EmitWriteRoot(CompilerContext context, Local valueFrom)
            => ((IRuntimeProtoSerializerNode)this).EmitWrite(context, valueFrom);

        T IFactory<T>.Create(ISerializationContext context) => (T)CreateInstance(context);

        public virtual void Write(ref ProtoWriter.State state, T value)
            => SerializeImpl(ref state, value);

        public virtual T Read(ref ProtoReader.State state, T value)
        {
            if (value is null) value = (T)CreateInstance(state.Context);

            Callback(ref value, TypeModel.CallbackType.BeforeDeserialize, state.Context);
            DeserializeBody(ref state, ref value, (ref T o) => o, (ref T o, T v) => o = v);
            Callback(ref value, TypeModel.CallbackType.AfterDeserialize, state.Context);
            return value;
        }
        public virtual bool IsSubType => false;

        void IRuntimeProtoSerializerNode.Write(ref ProtoWriter.State state, object value)
            => Write(ref state, TypeHelper<T>.FromObject(value));

        object IRuntimeProtoSerializerNode.Read(ref ProtoReader.State state, object value)
            => Read(ref state, TypeHelper<T>.FromObject(value));

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            if (_callbacks?[callbackType] != null) return true;
            for (int i = 0; i < _serializers.Length; i++)
            {
                if (_serializers[i].ExpectedType != ExpectedType && ((_serializers[i] as IProtoTypeSerializer)?.HasCallbacks(callbackType) ?? false)) return true;
            }
            return false;
        }

        private Type _constructType;
#if WINRT
        private readonly TypeInfo typeInfo;
#endif
        public Type ExpectedType { get; }
#if !FEAT_IKVM
object IProtoTypeSerializer.CreateInstance(ISerializationContext context) => CreateInstance(context);

        internal virtual Type BaseType => typeof(T);
        Type IProtoTypeSerializer.BaseType => BaseType;
        private int[] _fieldNumbers;
        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            if (_callbacks != null) InvokeCallback(_callbacks[callbackType], value, _constructType, context);
            IProtoTypeSerializer ser = (IProtoTypeSerializer)GetMoreSpecificSerializer(value);
            ser?.Callback(value, callbackType, context);
        }
        private CallbackSet _callbacks;
        private MethodInfo[] _baseCtorCallbacks;
        private MethodInfo _factory;

        public SerializerFeatures Features { get; private set; }
#if WINRT
        private static readonly TypeInfo iextensible = typeof(IExtensible).GetTypeInfo();
#else
        private static readonly System.Type iextensible = typeof(IExtensible);
#endif

        private bool CanHaveInheritance
        {
            get
            {
#if WINRT
                return (typeInfo.IsClass || typeInfo.IsInterface) && !typeInfo.IsSealed && AllowInheritance;
#else
                return (ExpectedType.IsClass || ExpectedType.IsInterface) && !ExpectedType.IsSealed && AllowInheritance;
#endif
            }
        }
        bool IProtoTypeSerializer.CanCreateInstance() { return true; }

        internal override void Init(int[] fieldNumbers, IRuntimeProtoSerializerNode[] serializers, MethodInfo[] baseCtorCallbacks,
            bool isRootType, bool useConstructor, bool assertKnownType,
            CallbackSet callbacks, Type constructType, MethodInfo factory, SerializerFeatures features)
        {
            Debug.Assert(fieldNumbers is object);
            Debug.Assert(serializers is object);
            Debug.Assert(fieldNumbers.Length == serializers.Length);

            Array.Sort(fieldNumbers, serializers);
            Features = features;
            bool hasSubTypes = false;
            var forType = ExpectedType;
            for (int i = 0; i < fieldNumbers.Length; i++)
            {
                if (i != 0 && fieldNumbers[i] == fieldNumbers[i - 1])
                {
                    throw new InvalidOperationException("Duplicate field-number detected; " +
                              fieldNumbers[i].ToString() + " on: " + forType.FullName);
                }
                if (!hasSubTypes && serializers[i].ExpectedType != forType)
                {
                    hasSubTypes = true;
                }
            }
            this.factory = factory;

            if (constructType is null)
            {
                constructType = forType;
            }
            else
            {
                if (!forType.IsAssignableFrom(constructType))
                {
                    throw new InvalidOperationException(forType.FullName + " cannot be assigned from " + constructType.FullName);
                }
            }
            this.constructType = constructType;
            this.serializers = serializers;
            this.fieldNumbers = fieldNumbers;
            this.callbacks = callbacks;
            this.isRootType = isRootType;
            this.useConstructor = useConstructor;
            this.assertKnownType = assertKnownType;

            if (baseCtorCallbacks is object)
            {
                foreach (var cb in baseCtorCallbacks)
                {
                    if (!cb.ReflectedType.IsAssignableFrom(forType))
                        throw new InvalidOperationException("Trying to assign incompatible callback to " + forType.FullName);
                }
                if (baseCtorCallbacks.Length == 0)
                    baseCtorCallbacks = null;
            }

            this.baseCtorCallbacks = baseCtorCallbacks;

            if (Nullable.GetUnderlyingType(forType) is object)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly - this is contextually fine
                throw new ArgumentException("Cannot create a TypeSerializer for nullable types", nameof(forType));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }
            if (iextensible.IsAssignableFrom(forType))
            {
                if (forType.IsValueType || !isRootType || hasSubTypes)
                {
                    throw new NotSupportedException("IExtensible is not supported in structs or classes with inheritance");
                }
                isExtensible = true;
            }
            hasConstructor = !constructType.IsAbstract && Helpers.GetConstructor(constructType, Type.EmptyTypes, true) is object;
            if (constructType != forType && useConstructor && !hasConstructor)
            {
                throw new ArgumentException("The supplied default implementation cannot be created: " + constructType.FullName, nameof(constructType));
            }

            if (HasInheritance && callbacks is object)
            {
                _subTypeOnBeforeDeserialize = (val, ctx) =>
                {   // note: since this only applies when we have inheritance, we don't need to worry about
                    // unobserved side-effects to structs
                    Callback(ref val, TypeModel.CallbackType.BeforeDeserialize, ctx);
                };
            }
        }

        void IProtoTypeSerializer.Callback(object value, TypeModel.CallbackType callbackType, ISerializationContext context)
        {
            if (isRootType && callbacks is object)
                InvokeCallback(callbacks[callbackType], value, context);
        }

        public bool GetMoreSpecificSerializer(object value, out IProtoSerializerWithWireType serializer, out int fieldNumber)
        {
            fieldNumber = 0;
            serializer = null;
            if (!CanHaveInheritance) return false;
            Type actualType = value.GetType();
            if (actualType == ExpectedType) return false;

            for (int i = 0; i < _serializers.Length; i++)
            {
                IProtoSerializerWithWireType ser = _serializers[i];
                if (ser.ExpectedType != ExpectedType && Helpers.IsAssignableFrom(ser.ExpectedType, actualType))
                {
                    serializer = ser;
                    fieldNumber = _fieldNumbers[i];
                    return true;
                }
            }
            if (actualType == _constructType) return false; // needs to be last in case the default concrete type is also a known sub-type
            TypeModel.ThrowUnexpectedSubtype(ExpectedType, actualType); // might throw (if not a proxy)
            return false;
        }

        public void SerializeImpl(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            var token = ProtoWriter.StartSubItem(value, (_isRootType && dest.TakeIsExpectingRootType()) || _prefixLength, dest);
            if (_isRootType) Callback(value, TypeModel.CallbackType.BeforeSerialize, dest.Context);
            // write inheritance first
            IProtoSerializerWithWireType next;
            int fn;
            if (GetMoreSpecificSerializer(value, out next, out fn))
            {
                ProtoWriter.WriteFieldHeaderBegin(fn, dest);
                next.Write(value, dest);
            }
            // write all actual fields
            //Helpers.DebugWriteLine(">> Writing fields for " + forType.FullName);
            for (int i = 0; i < _serializers.Length; i++)
            {
                IProtoSerializer ser = _serializers[i];
                if (ser.ExpectedType == ExpectedType)
                {
                    ProtoWriter.WriteFieldHeaderBegin(_fieldNumbers[i], dest);
                    //Helpers.DebugWriteLine(": " + ser.ToString());
                    ser.Write(value, dest);
                }
            }
            //Helpers.DebugWriteLine("<< Writing fields for " + forType.FullName);
            if (_isExtensible) ProtoWriter.AppendExtensionData((IExtensible)value, dest);
            if (_isRootType) Callback(value, TypeModel.CallbackType.AfterSerialize, dest.Context);
            ProtoWriter.EndSubItem(token, dest);
        }

        protected Action<T, ISerializationContext> _subTypeOnBeforeDeserialize;
        protected delegate T StateGetter<TState>(ref TState state);
        protected delegate void StateSetter<TState>(ref TState state, T value);
        public object CreateInstance(ProtoReader source, bool includeLocalCallback)
        {
            //Helpers.DebugWriteLine("* creating : " + forType.FullName);
            object obj;
            if (_factory != null)
            {
                obj = InvokeCallback(_factory, null, _constructType, source.Context);
            }
            else if (_useConstructor && _hasConstructor)
            {
                obj = Activator.CreateInstance(_constructType
#if !CF && !SILVERLIGHT && !WINRT && !PORTABLE
                , nonPublic: true
#endif
                );
            }
            else
            {
                obj = BclHelpers.GetUninitializedObject(_constructType);
            }
            ProtoReader.NoteObject(obj, source);
            if (_baseCtorCallbacks != null)
            {
                for (int i = 0; i < _baseCtorCallbacks.Length; i++)
                {
                    InvokeCallback(_baseCtorCallbacks[i], obj, _constructType, source.Context);
                }
            }
            if (includeLocalCallback && _callbacks != null) InvokeCallback(_callbacks.BeforeDeserialize, obj, _constructType, source.Context);
            return obj;
        }




        internal static object InvokeCallback(MethodInfo method, object obj, Type constructType, SerializationContext context)
        {
            object result = null;
            if (method != null)
            {   // pass in a streaming context if one is needed, else null
                bool handled;
                ParameterInfo[] parameters = method.GetParameters();
                object[] args;
                switch (parameters.Length)
                {
                    case 0:
                        args = null;
                        handled = true;
                        break;
                    default:
                        args = new object[parameters.Length];
                        handled = true;
                        for (int i = 0; i < args.Length; i++)
                        {
                            object val;
                            Type paramType = parameters[i].ParameterType;
                            if (paramType == typeof(SerializationContext)) val = context;
                            else if (paramType == typeof(System.Type)) val = constructType;
#if PLAT_BINARYFORMATTER || (SILVERLIGHT && NET_4_0)
                            else if (paramType == typeof(System.Runtime.Serialization.StreamingContext)) val = (System.Runtime.Serialization.StreamingContext)context;
#endif
                            else
                            {
                                val = null;
                                handled = false;
                            }
                            args[i] = val;
                        }
                        break;
                }
                if (handled)
                {
                    result = method.Invoke(obj, args);
                }
                else
                {
                    throw Meta.CallbackSet.CreateInvalidCallbackSignature(method);
                }

            }
            return result;
        }
        protected void DeserializeBody<TState>(ref ProtoReader.State state, ref TState bodyState, StateGetter<TState> getter, StateSetter<TState> setter)
        {
            int fieldNumber, lastFieldNumber = 0, lastFieldIndex = 0;
            bool fieldHandled;

            //Debug.WriteLine(">> Reading fields for " + forType.FullName);
            while ((fieldNumber = state.ReadFieldHeader()) > 0)
            {
                fieldHandled = false;
                if (fieldNumber < lastFieldNumber)
                {
                    lastFieldNumber = lastFieldIndex = 0;
                }
                for (int i = lastFieldIndex; i < fieldNumbers.Length; i++)
                {
                    if (fieldNumbers[i] == fieldNumber)
                    {
                        IRuntimeProtoSerializerNode ser = serializers[i];
                        //Debug.WriteLine(": " + ser.ToString());
                        if (ser is IProtoTypeSerializer ts && ts.IsSubType)
                        {
                            // sub-types are implemented differently; pass the entire
                            // state through and unbox again to observe any changes
                            bodyState = (TState)ser.Read(ref state, bodyState);
                        }
                        else
                        {
                            var value = getter(ref bodyState);
                            object boxed = value;
                            object result = ser.Read(ref state, boxed);
                            if (ser.ReturnsValue)
                            {
                                setter(ref bodyState, (T)result);
                            }
                            else if (ExpectedType.IsValueType)
                            {   // make sure changes to structs are preserved
                                setter(ref bodyState, (T)boxed);
                            }
                        }

                        lastFieldIndex = i;
                        lastFieldNumber = fieldNumber;
                        fieldHandled = true;
                        break;
                    }
                }
                if (!fieldHandled)
                {
                    //Debug.WriteLine(": [" + fieldNumber + "] (unknown)");
                    if (isExtensible)
                    {
                        var val = getter(ref bodyState);
                        state.AppendExtensionData((IExtensible)val);
                    }
                    else
                    {
                        state.SkipField();
                    }
                }
            }
        }
#endif
        public bool RequiresOldValue => true;

        private void LoadFromState(CompilerContext ctx, Local value)
        {
            if (HasInheritance)
            {
                var stateType = typeof(SubTypeState<>).MakeGenericType(typeof(T));
                var stateProp = stateType.GetProperty(nameof(SubTypeState<string>.Value));
                ctx.LoadAddress(value, stateType);
                ctx.EmitCall(stateProp.GetGetMethod());
            }
            else
            {
                ctx.LoadValue(value);
            }
        }

        private void WriteToState(CompilerContext ctx, Local state, Local value, Type type)
        {
            if (HasInheritance)
            {
                var stateType = typeof(SubTypeState<>).MakeGenericType(typeof(T));
                var stateProp = stateType.GetProperty(nameof(SubTypeState<string>.Value));

                if (value is null)
                {
                    using var tmp = new Local(ctx, type);
                    ctx.LoadValue(value);
                    ctx.StoreValue(tmp);
                    ctx.LoadAddress(state, stateType);
                    ctx.LoadValue(tmp);
                    ctx.EmitCall(stateProp.GetSetMethod());
                }
                else
                {
                    ctx.LoadAddress(state, stateType);
                    ctx.LoadValue(value);
                    ctx.EmitCall(stateProp.GetSetMethod());
                }
            }
            else
            {
                ctx.LoadValue(value);
                ctx.StoreValue(state);
            }
        }

        void IRuntimeProtoSerializerNode.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Type expected = ExpectedType;
            using Compiler.Local loc = ctx.GetLocalWithValue(expected, valueFrom);
            // pre-callbacks
            EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeSerialize);

            Compiler.CodeLabel startFields = ctx.DefineLabel();
            // inheritance
            if (CanHaveInheritance)
            {
                // if we expect sub-types: do if (IsSubType()) and a switch inside that eventually calls ThrowUnexpectedSubtype
                // otherwise, *just* call ThrowUnexpectedSubtype (it does the IsSubType test itself)
                if (serializers.Any(x => x is IProtoTypeSerializer pts && pts.IsSubType))
                {
                    ctx.LoadValue(loc);
                    ctx.EmitCall(typeof(TypeModel).GetMethod(nameof(TypeModel.IsSubType), BindingFlags.Static | BindingFlags.Public)
                        .MakeGenericMethod(typeof(T)));
                    ctx.BranchIfFalse(startFields, false);

                    for (int i = 0; i < serializers.Length; i++)
                    {
                        IRuntimeProtoSerializerNode ser = serializers[i];
                        Type serType = ser.ExpectedType;
                        if (ser is IProtoTypeSerializer ts && ts.IsSubType)
                        {
                            Compiler.CodeLabel nextTest = ctx.DefineLabel();
                            ctx.LoadValue(loc);
                            ctx.TryCast(serType);

                            using var typed = new Local(ctx, serType);
                            ctx.StoreValue(typed);

                            ctx.LoadValue(typed);
                            ctx.BranchIfFalse(nextTest, false);

                            if (serType.IsValueType)
                            {
                                ctx.LoadValue(loc);
                                ctx.CastFromObject(serType);
                                ser.EmitWrite(ctx, null);
                            }
                            else
                            {
                                ser.EmitWrite(ctx, typed);
                            }
                            
                            ctx.Branch(startFields, false);
                            ctx.MarkLabel(nextTest);
                        }
                    }
                }

                if (assertKnownType)
                {
                    MethodInfo method;
                    if (constructType is object && constructType != ExpectedType)
                    {
                        method = TypeSerializerMethodCache.ThrowUnexpectedSubtype[2].MakeGenericMethod(ExpectedType, constructType);
                    }
                    else
                    {
                        method = TypeSerializerMethodCache.ThrowUnexpectedSubtype[1].MakeGenericMethod(ExpectedType);
                    }
                    ctx.LoadValue(loc);
                    ctx.EmitCall(method);
                }
            }
            // fields

            ctx.MarkLabel(startFields);
            for (int i = 0; i < serializers.Length; i++)
            {
                IRuntimeProtoSerializerNode ser = serializers[i];
                if (!(ser is IProtoTypeSerializer ts && ts.IsSubType))
                    ser.EmitWrite(ctx, loc);
            }

            // extension data
            if (isExtensible)
            {
                ctx.EmitStateBasedWrite(nameof(ProtoWriter.State.AppendExtensionData), loc);
            }
            // post-callbacks
            EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.AfterSerialize);
        }

        private static void EmitInvokeCallback(Compiler.CompilerContext ctx, MethodInfo method, Type constructType, Type type, Local valueFrom)
        {
            if (method is object)
            {
                if (method.IsStatic)
                {
                    // calling a static factory method
                    Debug.Assert(valueFrom is null);
                }
                else
                {
                    // here, we're calling a callback *on an instance*;
                    if (type.IsValueType)
                    {
                        Debug.Assert(valueFrom is object); // can't do that for structs
                        ctx.LoadAddress(valueFrom, type);
                    }
                    else
                    {
                        ctx.LoadValue(valueFrom);
                    }
                    
                }

                ParameterInfo[] parameters = method.GetParameters();
                bool handled = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(ISerializationContext)
                        || parameterType == typeof(StreamingContext)
                        || parameterType == typeof(SerializationContext))
                    {
                        ctx.LoadSerializationContext(parameterType);
                    }
                    else if (parameterType == typeof(Type))
                    {
                        Type tmp = constructType ?? type;
                        ctx.LoadValue(tmp);
                    }
                    else
                    {
                        handled = false;
                    }
                }
                if (handled)
                {
                    ctx.EmitCall(method);
                    if (constructType is object)
                    {
                        if (method.ReturnType == typeof(object))
                        {
                            ctx.CastFromObject(type);
                        }
                    }
                }
                else
                {
                    throw Meta.CallbackSet.CreateInvalidCallbackSignature(method);
                }
            }
        }

        private void EmitCallbackIfNeeded(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            Debug.Assert(valueFrom is object);
            if (isRootType && ((IProtoTypeSerializer)this).HasCallbacks(callbackType))
            {
                if (HasInheritance && callbackType == TypeModel.CallbackType.BeforeDeserialize)
                {
                    ThrowHelper.ThrowInvalidOperationException("Should be using sub-type-state API");
                }
                else if (HasInheritance && callbackType == TypeModel.CallbackType.AfterDeserialize)
                {
                    LoadFromState(ctx, valueFrom);
                    ((IProtoTypeSerializer)this).EmitCallback(ctx, null, callbackType);
                }
                else
                {
                    ((IProtoTypeSerializer)this).EmitCallback(ctx, valueFrom, callbackType);
                }
            }
        }

        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            bool actuallyHasInheritance = false;
            if (CanHaveInheritance)
            {
                for (int i = 0; i < serializers.Length; i++)
                {
                    IRuntimeProtoSerializerNode ser = serializers[i];
                    if (ser.ExpectedType != ExpectedType && ((IProtoTypeSerializer)ser).HasCallbacks(callbackType))
                    {
                        actuallyHasInheritance = true;
                        break;
                    }
                }
            }

            Debug.Assert(((IProtoTypeSerializer)this).HasCallbacks(callbackType), "Shouldn't be calling this if there is nothing to do");
            MethodInfo method = callbacks?[callbackType];
            if (method is null && !actuallyHasInheritance)
            {
                return;
            }

            EmitInvokeCallback(ctx, method, null, ExpectedType, valueFrom);

            if (actuallyHasInheritance && BaseType != ExpectedType)
            {
                throw new NotSupportedException($"Currently, serializatation callbacks are limited to the base-type in a hierarchy, but {ExpectedType.NormalizeName()} defines callbacks; this may be resolved in later versions; it is recommended to make the serialization callbacks 'virtual' methods on {BaseType.NormalizeName()}; or for the best compatibility with other serializers (DataContractSerializer, etc) - make the callbacks non-virtual methods on {BaseType.NormalizeName()} that *call* protected virtual methods on {BaseType.NormalizeName()}");

                //Compiler.CodeLabel @break = ctx.DefineLabel();
                //for (int i = 0; i < serializers.Length; i++)
                //{
                //    IRuntimeProtoSerializerNode ser = serializers[i];
                //    IProtoTypeSerializer typeser;
                //    Type serType = ser.ExpectedType;
                //    if (serType != ExpectedType
                //        && (typeser = (IProtoTypeSerializer)ser).HasCallbacks(callbackType))
                //    {
                //        Compiler.CodeLabel ifMatch = ctx.DefineLabel(), nextTest = ctx.DefineLabel();
                //        ctx.CopyValue();
                //        ctx.TryCast(serType);
                //        ctx.CopyValue();
                //        ctx.BranchIfTrue(ifMatch, true);
                //        ctx.DiscardValue();
                //        ctx.Branch(nextTest, false);
                //        ctx.MarkLabel(ifMatch);
                //        typeser.EmitCallback(ctx, null, callbackType);
                //        ctx.Branch(@break, false);
                //        ctx.MarkLabel(nextTest);
                //    }
                //}
                //ctx.MarkLabel(@break);
                //ctx.DiscardValue();
            }
        }

        void IRuntimeProtoSerializerNode.EmitRead(CompilerContext ctx, Local valueFrom)
        {
            Type inputType = HasInheritance ? typeof(SubTypeState<>).MakeGenericType(ExpectedType) : ExpectedType;
            Debug.Assert(valueFrom is object);

            using Compiler.Local loc = ctx.GetLocalWithValue(inputType, valueFrom);
            using Compiler.Local fieldNumber = new Compiler.Local(ctx, typeof(int));
            if (!ExpectedType.IsValueType && !HasInheritance)
            {   // we're writing a *basic* serializer for ref-type T; it could
                // be null
                EmitCreateIfNull(ctx, loc);
            }

            // pre-callbacks
            if (HasCallbacks(TypeModel.CallbackType.BeforeDeserialize))
            {
                if (HasInheritance)
                {
                    var method = callbacks?[TypeModel.CallbackType.BeforeDeserialize];
                    if (method is object)
                    {
                        // subTypeState.OnBeforeDeserialize(callbackField);
                        ctx.LoadAddress(loc, inputType);
                        var callbackfield = ctx.Scope.DefineSubTypeStateCallbackField<T>(method);
                        ctx.LoadValue(callbackfield, checkAccessibility: false);
                        ctx.EmitCall(inputType.GetMethod(nameof(SubTypeState<string>.OnBeforeDeserialize)));
                    }
                }
                else
                {   // nice and simple; just call it
                    EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeDeserialize);
                }
            }

            Compiler.CodeLabel @continue = ctx.DefineLabel(), processField = ctx.DefineLabel();
            ctx.Branch(@continue, false);

            ctx.MarkLabel(processField);
            foreach (var group in BasicList.GetContiguousGroups(fieldNumbers, serializers))
            {
                Compiler.CodeLabel tryNextField = ctx.DefineLabel();
                int groupItemCount = group.Items.Count;
                if (groupItemCount == 1)
                {
                    // discreet group; use an equality test
                    ctx.LoadValue(fieldNumber);
                    ctx.LoadValue(group.First);
                    Compiler.CodeLabel processThisField = ctx.DefineLabel();
                    ctx.BranchIfEqual(processThisField, true);
                    ctx.Branch(tryNextField, false);
                    WriteFieldHandler(ctx, ExpectedType, loc, processThisField, @continue, group.Items[0]);
                }
                else
                {   // implement as a jump-table-based switch
                    ctx.LoadValue(fieldNumber);
                    ctx.LoadValue(group.First);
                    ctx.Subtract(); // jump-tables are zero-based
                    Compiler.CodeLabel[] jmp = new Compiler.CodeLabel[groupItemCount];
                    for (int i = 0; i < groupItemCount; i++)
                    {
                        jmp[i] = ctx.DefineLabel();
                    }
                    ctx.Switch(jmp);
                    // write the default...
                    ctx.Branch(tryNextField, false);
                    for (int i = 0; i < groupItemCount; i++)
                    {
                        WriteFieldHandler(ctx, ExpectedType, loc, jmp[i], @continue, group.Items[i]);
                    }
                }
                ctx.MarkLabel(tryNextField);
            }

            ctx.LoadState();
            if (isExtensible)
            {
                LoadFromState(ctx, loc);
                ctx.EmitCall(typeof(ProtoReader.State).GetMethod(nameof(ProtoReader.State.AppendExtensionData),
                    new[] { typeof(IExtensible) }));
            }
            else
            {
                ctx.EmitCall(typeof(ProtoReader.State).GetMethod(nameof(ProtoReader.State.SkipField), Type.EmptyTypes));
            }
            ctx.MarkLabel(@continue);
            ctx.EmitStateBasedRead(nameof(ProtoReader.State.ReadFieldHeader), typeof(int));
            ctx.CopyValue();
            ctx.StoreValue(fieldNumber);
            ctx.LoadValue(0);
            ctx.BranchIfGreater(processField, false);

            // post-callbacks
            if (HasCallbacks(TypeModel.CallbackType.AfterDeserialize))
            {
                EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.AfterDeserialize);
            }

            if (HasInheritance)
            {
                // in this scenario, before exiting, we'll leave the T on the stack
                LoadFromState(ctx, loc);
            }
            else if (valueFrom is object && !loc.IsSame(valueFrom))
            {
                LoadFromState(ctx, loc);
                ctx.StoreValue(valueFrom);
            }
        }

        private void WriteFieldHandler(
#pragma warning disable IDE0060
            Compiler.CompilerContext ctx, Type expected, Compiler.Local loc,
#pragma warning restore IDE0060
            Compiler.CodeLabel handler, Compiler.CodeLabel @continue, IRuntimeProtoSerializerNode serializer)
        {
            ctx.MarkLabel(handler);

            //Type serType = serializer.ExpectedType;

            //if (serType == ExpectedType)
            //{
            //    if (canBeNull) EmitCreateIfNull(ctx, loc);
            //    serializer.EmitRead(ctx, loc);
            //}
            //else
            //{
            //    //RuntimeTypeModel rtm = (RuntimeTypeModel)ctx.Model;
            //    if (((IProtoTypeSerializer)serializer).CanCreateInstance())
            //    {
            //        Compiler.CodeLabel allDone = ctx.DefineLabel();

            //        ctx.LoadValue(loc);
            //        ctx.BranchIfFalse(allDone, false); // null is always ok

            //        ctx.LoadValue(loc);
            //        ctx.TryCast(serType);
            //        ctx.BranchIfTrue(allDone, false); // not null, but of the correct type

            //        // otherwise, need to convert it
            //        ctx.LoadReader(false);
            //        ctx.LoadValue(loc);
            //        ((IProtoTypeSerializer)serializer).EmitCreateInstance(ctx);

            //        ctx.EmitCall(typeof(ProtoReader).GetMethod("Merge",
            //            new[] { typeof(ProtoReader), typeof(object), typeof(object)}));
            //        ctx.Cast(expected);
            //        ctx.StoreValue(loc); // Merge always returns a value

            //        // nothing needs doing
            //        ctx.MarkLabel(allDone);
            //    }

            //    if (Helpers.IsValueType(serType))
            //    {
            //        Compiler.CodeLabel initValue = ctx.DefineLabel();
            //        Compiler.CodeLabel hasValue = ctx.DefineLabel();
            //        using (Compiler.Local emptyValue = new Compiler.Local(ctx, serType))
            //        {
            //            ctx.LoadValue(loc);
            //            ctx.BranchIfFalse(initValue, false);

            //            ctx.LoadValue(loc);
            //            ctx.CastFromObject(serType);
            //            ctx.Branch(hasValue, false);

            //            ctx.MarkLabel(initValue);
            //            ctx.InitLocal(serType, emptyValue);
            //            ctx.LoadValue(emptyValue);

            //            ctx.MarkLabel(hasValue);
            //        }
            //    }
            //    else
            //    {
            //        ctx.LoadValue(loc);
            //        ctx.Cast(serType);
            //    }

            //    serializer.EmitRead(ctx, null);
            //}

            bool isSubtype = false;
            if (HasInheritance)
            {
                if (serializer is IProtoTypeSerializer pts && pts.IsSubType)
                {
                    // special-cased; we don't access .Value here, but instead
                    // pass the state down
                    isSubtype = true;
                    serializer.EmitRead(ctx, loc);
                }
                else
                {
                    LoadFromState(ctx, loc);
                    serializer.EmitRead(ctx, null);
                }
            }
            else
            {
                serializer.EmitRead(ctx, loc);
            }

            if (!isSubtype && serializer.ReturnsValue) 
            {
                WriteToState(ctx, loc, null, serializer.ExpectedType);
            }

            //if (serType == ExpectedType)
            //{
            //    if (canBeNull) EmitCreateIfNull(ctx, loc);
            //    serializer.EmitRead(ctx, loc);
            //}

            //if (serializer.ReturnsValue)
            //{   // update the variable
            //    if (Helpers.IsValueType(serType))
            //    {
            //        // but box it first in case of value type
            //        ctx.CastToObject(serType);
            //    }
            //    ctx.StoreValue(loc);
            //}
            ctx.Branch(@continue, false); // "continue"
        }

        bool IProtoTypeSerializer.ShouldEmitCreateInstance
            => factory is object || !useConstructor;

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx, bool callNoteObject)
        {
            // different ways of creating a new instance
            if (factory is object)
            {
                EmitInvokeCallback(ctx, factory, constructType, ExpectedType, null);
            }
            else if (!useConstructor)
            {   // DataContractSerializer style
                ctx.LoadValue(constructType);
                ctx.EmitCall(typeof(BclHelpers).GetMethod(nameof(BclHelpers.GetUninitializedObject)));
                ctx.Cast(ExpectedType);
            }
            else if (constructType.IsClass && hasConstructor)
            {   // XmlSerializer style
                ctx.EmitCtor(constructType);
            }
            else
            {
                ctx.LoadValue(ExpectedType);
                ctx.LoadNullRef();
                ctx.EmitCall(typeof(TypeModel).GetMethod(nameof(TypeModel.ThrowCannotCreateInstance),
                    BindingFlags.Static | BindingFlags.Public));
                ctx.LoadNullRef();
                callNoteObject = false;
            }

            // at this point we have an ExpectedType on the stack

            if (callNoteObject || baseCtorCallbacks is object)
            {
                // we're going to need it multiple times; use a local
                using var loc = new Local(ctx, ExpectedType);
                ctx.StoreValue(loc);

#if FEAT_DYNAMIC_REF
                if (callNoteObject)
                {
                    // track root object creation
                    ctx.LoadState();
                    ctx.LoadValue(loc);
                    ctx.EmitCall(typeof(ProtoReader.State).GetMethod(nameof(ProtoReader.State.NoteObject)));
                }
#endif

                //if (baseCtorCallbacks is object)
                //{
                //    for (int i = 0; i < baseCtorCallbacks.Length; i++)
                //    {
                //        EmitInvokeCallback(ctx, baseCtorCallbacks[i], null, ExpectedType, loc);
                //    }
                //}

                ctx.LoadValue(loc);
            }
        }
        private void EmitCreateIfNull(Compiler.CompilerContext ctx, Compiler.Local storage)
        {
            Debug.Assert(storage is object);
            if (!ExpectedType.IsValueType)
            {
                Compiler.CodeLabel afterNullCheck = ctx.DefineLabel();
                ctx.LoadValue(storage);
                ctx.BranchIfTrue(afterNullCheck, false);

                ((IProtoTypeSerializer)this).EmitCreateInstance(ctx);
                ctx.StoreValue(storage);

                //if (callbacks is object) EmitInvokeCallback(ctx, callbacks.BeforeDeserialize, null, ExpectedType, storage);
                ctx.MarkLabel(afterNullCheck);
            }
        }
    }

    internal static class TypeSerializerMethodCache
    {
        internal static readonly Dictionary<int,MethodInfo> ThrowUnexpectedSubtype
        = (from method in typeof(TypeModel).GetMethods(BindingFlags.Static | BindingFlags.Public)
                where method.Name == nameof(TypeModel.ThrowUnexpectedSubtype) && method.IsGenericMethodDefinition
                where method.GetParameters().Length == 1
                let args = method.GetGenericArguments()
                select new { Count = args.Length, Method = method }).ToDictionary(x => x.Count, x => x.Method);
    }

}
