// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AltLinq;
using AqlaSerializer.Meta;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    // TODO go in wide instead, all deep objects add to queue and process in wide
    sealed class TypeSerializer : IProtoTypeSerializer
    {
        public bool DemandWireTypeStabilityStatus() => true;

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            if (callbacks != null && callbacks[callbackType] != null) return true;
            for (int i = 0; i < serializers.Length; i++)
            {
                if (serializers[i].ExpectedType != forType && ((serializers[i] as IProtoTypeSerializer)?.HasCallbacks(callbackType) ?? false)) return true;
            }
            return false;
        }
        private readonly Type forType, constructType;
#if WINRT
        private readonly TypeInfo typeInfo;
#endif
        public Type ExpectedType { get { return forType; } }

        public IProtoTypeSerializer GetSubTypeSerializer(int number)
        {
            return (IProtoTypeSerializer)serializers[Array.IndexOf(fieldNumbers, number)];
        }

        private readonly IProtoSerializerWithWireType[] serializers;
        private readonly int[] fieldNumbers;
        private readonly bool isRootType, useConstructor, isExtensible, hasConstructor;
        private readonly CallbackSet callbacks;
        private readonly MethodInfo[] baseCtorCallbacks;
        private readonly MethodInfo factory;
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
                           fieldNumbers[i].ToString() + " on: " + forType.FullName);
                if (!hasSubTypes && serializers[i].ExpectedType != forType)
                {
                    hasSubTypes = true;
                }
            }
            this.forType = forType;
            this.factory = factory;
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
            this.constructType = constructType;
            this.serializers = serializers;
            this.fieldNumbers = fieldNumbers;
            this.callbacks = callbacks;
            this.isRootType = isRootType;
            this.useConstructor = useConstructor;

            if (baseCtorCallbacks != null && baseCtorCallbacks.Length == 0) baseCtorCallbacks = null;
            this.baseCtorCallbacks = baseCtorCallbacks;
#if !NO_GENERICS
            if (Helpers.GetNullableUnderlyingType(forType) != null)
            {
                throw new ArgumentException("Cannot create a TypeSerializer for nullable types", "forType");
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
                isExtensible = true;
            }
#if WINRT
            TypeInfo constructTypeInfo = constructType.GetTypeInfo();
            hasConstructor = !constructTypeInfo.IsAbstract && Helpers.GetConstructor(constructTypeInfo, Helpers.EmptyTypes, true) != null;
#else
            hasConstructor = !constructType.IsAbstract && Helpers.GetConstructor(constructType, Helpers.EmptyTypes, true) != null;
#endif
            if (constructType != forType && useConstructor && !hasConstructor)
            {
                throw new ArgumentException("The supplied default implementation cannot be created: " + constructType.FullName, "constructType");
            }
        }
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
                return (forType.IsClass || forType.IsInterface) && !forType.IsSealed && AllowInheritance;
#endif
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
            if (callbacks != null) InvokeCallback(callbacks[callbackType], value, constructType, context);
            IProtoTypeSerializer ser = (IProtoTypeSerializer)GetMoreSpecificSerializer(value);
            if (ser != null) ser.Callback(value, callbackType, context);
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
            if (actualType == forType) return false;

            for (int i = 0; i < serializers.Length; i++)
            {
                IProtoSerializerWithWireType ser = serializers[i];
                if (ser.ExpectedType != forType && Helpers.IsAssignableFrom(ser.ExpectedType, actualType))
                {
                    serializer = ser;
                    fieldNumber = fieldNumbers[i];
                    return true;
                }
            }
            if (actualType == constructType) return false; // needs to be last in case the default concrete type is also a known sub-type
            TypeModel.ThrowUnexpectedSubtype(forType, actualType); // might throw (if not a proxy)
            return false;
        }

        public void Write(object value, ProtoWriter dest)
        {
            var token = ProtoWriter.StartSubItem(value, _prefixLength, dest);
            if (isRootType) Callback(value, TypeModel.CallbackType.BeforeSerialize, dest.Context);
            // write inheritance first
            // TODO add WriteFieldHeaderBegin to emit for subtypes AND members
            IProtoSerializerWithWireType next;
            int fn;
            if (GetMoreSpecificSerializer(value, out next, out fn))
            {
                ProtoWriter.WriteFieldHeaderBegin(fn, dest);
                next.Write(value, dest);
            }
            // write all actual fields
            //Helpers.DebugWriteLine(">> Writing fields for " + forType.FullName);
            for (int i = 0; i < serializers.Length; i++)
            {
                IProtoSerializer ser = serializers[i];
                if (ser.ExpectedType == forType)
                {
                    ProtoWriter.WriteFieldHeaderBegin(fieldNumbers[i], dest);
                    //Helpers.DebugWriteLine(": " + ser.ToString());
                    ser.Write(value, dest);
                }
            }
            //Helpers.DebugWriteLine("<< Writing fields for " + forType.FullName);
            if (isExtensible) ProtoWriter.AppendExtensionData((IExtensible)value, dest);
            if (isRootType) Callback(value, TypeModel.CallbackType.AfterSerialize, dest.Context);
            ProtoWriter.EndSubItem(token, dest);
        }
        public object Read(object value, ProtoReader source)
        {
            var token = ProtoReader.StartSubItem(source);
            if (isRootType && value != null) { Callback(value, TypeModel.CallbackType.BeforeDeserialize, source.Context); }
            int fieldNumber, lastFieldNumber = 0, lastFieldIndex = 0;
            bool fieldHandled;

            //Helpers.DebugWriteLine(">> Reading fields for " + forType.FullName);
            while ((fieldNumber = source.ReadFieldHeader()) > 0)
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
                        IProtoSerializer ser = serializers[i];
                        //Helpers.DebugWriteLine(": " + ser.ToString());
                        Type serType = ser.ExpectedType;
                        if (value == null ||  !Helpers.IsInstanceOfType(forType, value))
                        {
                            if (serType == forType && CanCreateInstance) value = CreateInstance(source, true);
                        }
                        if (ser.ReturnsValue)
                        {
                            value = ser.Read(value, source);
                        }
                        else
                        { // pop
                            ser.Read(value, source);
                        }

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
                    if (isExtensible)
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
            if (isRootType) { Callback(value, TypeModel.CallbackType.AfterDeserialize, source.Context); }
            ProtoReader.EndSubItem(token, source);
            return value;
        }




        internal static object InvokeCallback(MethodInfo method, object obj, Type constructType, SerializationContext context)
        {
            object result = null;
            object[] args;
            if (method != null)
            {   // pass in a streaming context if one is needed, else null
                bool handled;
                ParameterInfo[] parameters = method.GetParameters();
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
        public object CreateInstance(ProtoReader source, bool includeLocalCallback)
        {
            //Helpers.DebugWriteLine("* creating : " + forType.FullName);
            object obj;
            if (factory != null)
            {
                obj = InvokeCallback(factory, null, constructType, source.Context);
            }
            else if (useConstructor && hasConstructor)
            {
                obj = Activator.CreateInstance(constructType
#if !CF && !SILVERLIGHT && !WINRT && !PORTABLE
, true
#endif
);
            }
            else
            {
                obj = BclHelpers.GetUninitializedObject(constructType);
            }
            ProtoReader.NoteObject(obj, source);
            if (baseCtorCallbacks != null)
            {
                for (int i = 0; i < baseCtorCallbacks.Length; i++)
                {
                    InvokeCallback(baseCtorCallbacks[i], obj, constructType, source.Context);
                }
            }
            if (includeLocalCallback && callbacks != null) InvokeCallback(callbacks.BeforeDeserialize, obj, constructType, source.Context);
            return obj;
        }
#endif
        bool IProtoSerializer.RequiresOldValue { get { return true; } }
        bool IProtoSerializer.ReturnsValue { get { return false; } } // updates field directly
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Type expected = ExpectedType;
            using (Compiler.Local loc = ctx.GetLocalWithValue(expected, valueFrom))
            {
                // pre-callbacks
                EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.BeforeSerialize);

                Compiler.CodeLabel startFields = ctx.DefineLabel();
                // inheritance
                if (CanHaveInheritance)
                {
                    for (int i = 0; i < serializers.Length; i++)
                    {
                        IProtoSerializer ser = serializers[i];
                        Type serType = ser.ExpectedType;
                        if (serType != forType)
                        {
                            Compiler.CodeLabel ifMatch = ctx.DefineLabel(), nextTest = ctx.DefineLabel();
                            ctx.LoadValue(loc);
                            ctx.TryCast(serType);
                            ctx.CopyValue();
                            ctx.BranchIfTrue(ifMatch, true);
                            ctx.DiscardValue();
                            ctx.Branch(nextTest, true);
                            ctx.MarkLabel(ifMatch);
                            ser.EmitWrite(ctx, null);
                            ctx.Branch(startFields, false);
                            ctx.MarkLabel(nextTest);
                        }
                    }


                    if (constructType != null && constructType != forType)
                    {
                        using (Compiler.Local actualType = new Compiler.Local(ctx, ctx.MapType(typeof(System.Type))))
                        {
                            // would have jumped to "fields" if an expected sub-type, so two options:
                            // a: *exactly* that type, b: an *unexpected* type
                            ctx.LoadValue(loc);
                            ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                            ctx.CopyValue();
                            ctx.StoreValue(actualType);
                            ctx.LoadValue(forType);
                            ctx.BranchIfEqual(startFields, true);

                            ctx.LoadValue(actualType);
                            ctx.LoadValue(constructType);
                            ctx.BranchIfEqual(startFields, true);
                        }
                    }
                    else
                    {
                        // would have jumped to "fields" if an expected sub-type, so two options:
                        // a: *exactly* that type, b: an *unexpected* type
                        ctx.LoadValue(loc);
                        ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                        ctx.LoadValue(forType);
                        ctx.BranchIfEqual(startFields, true);
                    }
                    // unexpected, then... note that this *might* be a proxy, which
                    // is handled by ThrowUnexpectedSubtype
                    ctx.LoadValue(forType);
                    ctx.LoadValue(loc);
                    ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("GetType"));
                    ctx.EmitCall(ctx.MapType(typeof(TypeModel)).GetMethod("ThrowUnexpectedSubtype",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));

                }
                // fields

                ctx.MarkLabel(startFields);
                for (int i = 0; i < serializers.Length; i++)
                {
                    IProtoSerializer ser = serializers[i];
                    if (ser.ExpectedType == forType) ser.EmitWrite(ctx, loc);
                }

                // extension data
                if (isExtensible)
                {
                    ctx.LoadValue(loc);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("AppendExtensionData"));
                }
                // post-callbacks
                EmitCallbackIfNeeded(ctx, loc, TypeModel.CallbackType.AfterSerialize);
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
                    Type parameterType = parameters[0].ParameterType;
                    if (parameterType == ctx.MapType(typeof(SerializationContext)))
                    {
                        ctx.LoadSerializationContext();
                    }
                    else if (parameterType == ctx.MapType(typeof(System.Type)))
                    {
                        Type tmp = constructType;
                        if (tmp == null) tmp = type; // no ?? in some C# profiles
                        ctx.LoadValue(tmp);
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
            Helpers.DebugAssert(valueFrom != null);
            if (isRootType && ((IProtoTypeSerializer)this).HasCallbacks(callbackType))
            {
                ((IProtoTypeSerializer)this).EmitCallback(ctx, valueFrom, callbackType);
            }
        }
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            bool actuallyHasInheritance = false;
            if (CanHaveInheritance)
            {

                for (int i = 0; i < serializers.Length; i++)
                {
                    IProtoSerializer ser = serializers[i];
                    if (ser.ExpectedType != forType && ((ser as IProtoTypeSerializer)?.HasCallbacks(callbackType) ?? false))
                    {
                        actuallyHasInheritance = true;
                    }
                }
            }

            Helpers.DebugAssert(((IProtoTypeSerializer)this).HasCallbacks(callbackType), "Shouldn't be calling this if there is nothing to do");
            MethodInfo method = callbacks == null ? null : callbacks[callbackType];
            if (method == null && !actuallyHasInheritance)
            {
                return;
            }
            ctx.LoadAddress(valueFrom, ExpectedType);
            EmitInvokeCallback(ctx, method, actuallyHasInheritance, null, forType);

            if (actuallyHasInheritance)
            {
                Compiler.CodeLabel @break = ctx.DefineLabel();
                for (int i = 0; i < serializers.Length; i++)
                {
                    IProtoSerializer ser = serializers[i];
                    IProtoTypeSerializer typeser;
                    Type serType = ser.ExpectedType;
                    if (serType != forType &&
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

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Type expected = ExpectedType;
            Helpers.DebugAssert(valueFrom != null);

            using (Compiler.Local loc = ctx.GetLocalWithValue(expected, valueFrom))
            using (Compiler.Local fieldNumber = new Compiler.Local(ctx, ctx.MapType(typeof(int))))
            {
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
                foreach (BasicList.Group group in BasicList.GetContiguousGroups(fieldNumbers, serializers))
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
                        WriteFieldHandler(ctx, expected, loc, processThisField, @continue, (IProtoSerializer)group.Items[0]);
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
                            WriteFieldHandler(ctx, expected, loc, jmp[i], @continue, (IProtoSerializer)group.Items[i]);
                        }
                    }
                    ctx.MarkLabel(tryNextField);
                }

                EmitCreateIfNull(ctx, loc);
                ctx.LoadReaderWriter();
                if (isExtensible)
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

                if (valueFrom != null && !loc.IsSame(valueFrom))
                {
                    ctx.LoadValue(loc);
                    ctx.Cast(valueFrom.Type);
                    ctx.StoreValue(valueFrom);
                }
            }
        }

        private void WriteFieldHandler(
            Compiler.CompilerContext ctx, Type expected, Compiler.Local loc,
            Compiler.CodeLabel handler, Compiler.CodeLabel @continue, IProtoSerializer serializer)
        {
            ctx.MarkLabel(handler);
            Type serType = serializer.ExpectedType;
            if (serType == forType)
            {
                // emit create if null
                Helpers.DebugAssert(loc != null);
                if (!ExpectedType.IsValueType && CanCreateInstance)
                {
                    Compiler.CodeLabel afterIf = ctx.DefineLabel();
                    Compiler.CodeLabel ifContent = ctx.DefineLabel();

                    // if != null && of correct type
                    ctx.LoadValue(loc);
                    ctx.BranchIfFalse(ifContent, false);
                    ctx.LoadValue(loc);
                    ctx.TryCast(forType);
                    ctx.BranchIfFalse(ifContent, false);
                    ctx.Branch(afterIf, false);
                    {
                        ctx.MarkLabel(ifContent);

                        ((IProtoTypeSerializer)this).EmitCreateInstance(ctx);

                        if (callbacks != null) EmitInvokeCallback(ctx, callbacks.BeforeDeserialize, true, null, forType);
                        ctx.StoreValue(loc);
                    }
                    ctx.MarkLabel(afterIf);
                }

                serializer.EmitRead(ctx, loc);
            }
            else
            {
                ctx.LoadValue(loc);
                if (forType.IsValueType || !serializer.ReturnsValue)
                    ctx.Cast(serType);
                else
                    ctx.TryCast(serType); // default value can be another inheritance branch
                serializer.EmitRead(ctx, null);
            }

            if (serializer.ReturnsValue)
            {   // update the variable
                ctx.StoreValue(loc);
            }
            ctx.Branch(@continue, false); // "continue"
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            // different ways of creating a new instance
            bool callNoteObject = true;
            if (factory != null)
            {
                EmitInvokeCallback(ctx, factory, false, constructType, forType);
            }
            else if (!useConstructor || (useConstructor && !hasConstructor))
            {   // DataContractSerializer style
                ctx.LoadValue(constructType);
                ctx.EmitCall(ctx.MapType(typeof(BclHelpers)).GetMethod("GetUninitializedObject"));
                ctx.Cast(forType);
            }
            else if (constructType.IsClass && hasConstructor)
            {   // XmlSerializer style
                ctx.EmitCtor(constructType);
            }
            else
            {
                ctx.LoadValue(ExpectedType);
                ctx.EmitCall(ctx.MapType(typeof(TypeModel)).GetMethod("ThrowCannotCreateInstance",
                    BindingFlags.Static | BindingFlags.Public));
                ctx.LoadNullRef();
                callNoteObject = false;
            }
            if (callNoteObject)
            {
                // track root object creation
                ctx.CopyValue();
                ctx.LoadReaderWriter();
                ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("NoteObject",
                        BindingFlags.Static | BindingFlags.Public));
            }
            if (baseCtorCallbacks != null)
            {
                for (int i = 0; i < baseCtorCallbacks.Length; i++)
                {
                    EmitInvokeCallback(ctx, baseCtorCallbacks[i], true, null, forType);
                }
            }
        }
        private void EmitCreateIfNull(Compiler.CompilerContext ctx, Compiler.Local storage)
        {
            Helpers.DebugAssert(storage != null);
            if (!ExpectedType.IsValueType && CanCreateInstance)
            {
                Compiler.CodeLabel afterNullCheck = ctx.DefineLabel();
                ctx.LoadValue(storage);
                ctx.BranchIfTrue(afterNullCheck, false);

                ((IProtoTypeSerializer)this).EmitCreateInstance(ctx);

                if (callbacks != null) EmitInvokeCallback(ctx, callbacks.BeforeDeserialize, true, null, forType);
                ctx.StoreValue(storage);
                ctx.MarkLabel(afterNullCheck);
            }
        }
#endif
    }

}
#endif