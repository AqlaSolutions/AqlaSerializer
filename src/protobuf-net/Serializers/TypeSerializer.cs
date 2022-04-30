// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
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
#endif

namespace AqlaSerializer.Serializers
{
    sealed class TypeSerializer : IProtoTypeSerializer
    {
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

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            if (_callbacks?[callbackType] != null) return true;
            for (int i = 0; i < _serializers.Length; i++)
            {
                if (_serializers[i].ExpectedType != ExpectedType && ((_serializers[i] as IProtoTypeSerializer)?.HasCallbacks(callbackType) ?? false)) return true;
            }
            return false;
        }
        private readonly Type _constructType;
        public Type ExpectedType { get; }

        public IProtoTypeSerializer GetSubTypeSerializer(int number)
        {
            return (IProtoTypeSerializer)_serializers[Array.IndexOf(_fieldNumbers, number)];
        }

        private readonly IProtoSerializerWithWireType[] _serializers;
        private readonly int[] _fieldNumbers;
        private readonly bool _isRootType, _useConstructor, _isExtensible, _hasConstructor;
        private readonly CallbackSet _callbacks;
        private readonly MethodInfo[] _baseCtorCallbacks;
        private readonly MethodInfo _factory;
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
            if (constructType == null)
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
            this._constructType = constructType;
            this._serializers = serializers;
            this._fieldNumbers = fieldNumbers;
            this._callbacks = callbacks;
            this._isRootType = isRootType;
            this._useConstructor = useConstructor;

            if (baseCtorCallbacks != null && baseCtorCallbacks.Length == 0) baseCtorCallbacks = null;
            this._baseCtorCallbacks = baseCtorCallbacks;
            if (Helpers.GetNullableUnderlyingType(forType) != null)
            {
                throw new ArgumentException("Cannot create a TypeSerializer for nullable types", nameof(forType));
            }

            if (model.MapType(iextensible).IsAssignableFrom(forType))
            {
                if (forType.IsValueType || !isRootType || hasSubTypes)
                {
                    throw new NotSupportedException("IExtensible is not supported in structs or classes with inheritance");
                }
                _isExtensible = true;
            }
            _hasConstructor = !constructType.IsAbstract && Helpers.GetConstructor(constructType, Helpers.EmptyTypes, true) != null;
            if (constructType != forType && useConstructor && !_hasConstructor)
            {
                throw new ArgumentException("The supplied default implementation cannot be created: " + constructType.FullName, nameof(constructType));
            }
        }

        private static readonly System.Type iextensible = typeof(IExtensible);

        private bool CanHaveInheritance
        {
            get
            {
                return (ExpectedType.IsClass || ExpectedType.IsInterface) && !ExpectedType.IsSealed && AllowInheritance;
            }
        }
        bool IProtoTypeSerializer.CanCreateInstance() { return true; }
#if !FEAT_IKVM
        object IProtoTypeSerializer.CreateInstance(ProtoReader source)
        {
            return CreateInstance(source, false);
        }
        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            if (_callbacks != null) InvokeCallback(_callbacks[callbackType], value, _constructType, context);
            IProtoTypeSerializer ser = (IProtoTypeSerializer)GetMoreSpecificSerializer(value);
            ser?.Callback(value, callbackType, context);
        }

        public IProtoSerializerWithWireType GetMoreSpecificSerializer(object value)
        {
            int fieldNumber;
            IProtoSerializerWithWireType serializer;
            GetMoreSpecificSerializer(value, out serializer, out fieldNumber);
            return serializer;
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

        public void Write(object value, ProtoWriter dest)
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
        public object Read(object value, ProtoReader source)
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
#if PLAT_BINARYFORMATTER
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
#if !PORTABLE
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
#endif
        public bool RequiresOldValue => true;

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

}
#endif