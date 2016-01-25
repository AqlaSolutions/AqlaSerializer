﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class SurrogateSerializer : IProtoTypeSerializer
    {
        bool IProtoTypeSerializer.HasCallbacks(AqlaSerializer.Meta.TypeModel.CallbackType callbackType) { return false; }
#if FEAT_COMPILER
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, AqlaSerializer.Meta.TypeModel.CallbackType callbackType) { }
        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx) { throw new NotSupportedException(); }
#endif
        bool IProtoTypeSerializer.CanCreateInstance() { return false; }
#if !FEAT_IKVM
        object IProtoTypeSerializer.CreateInstance(ProtoReader source) { throw new NotSupportedException(); }
        void IProtoTypeSerializer.Callback(object value, AqlaSerializer.Meta.TypeModel.CallbackType callbackType, SerializationContext context) { }
#endif

        public bool ReturnsValue { get { return false; } }
        public bool RequiresOldValue { get { return true; } }
        public Type ExpectedType { get { return forType; } }
        private readonly Type forType, declaredType;
        private readonly MethodInfo toTail, fromTail;
        IProtoTypeSerializer rootTail;

        public SurrogateSerializer(TypeModel model, Type forType, Type declaredType, IProtoTypeSerializer rootTail)
        {
            Helpers.DebugAssert(forType != null, "forType");
            Helpers.DebugAssert(declaredType != null, "declaredType");
            Helpers.DebugAssert(rootTail != null, "rootTail");
            Helpers.DebugAssert(rootTail.RequiresOldValue, "RequiresOldValue");
            Helpers.DebugAssert(!rootTail.ReturnsValue, "ReturnsValue");
            Helpers.DebugAssert(declaredType == rootTail.ExpectedType || Helpers.IsSubclassOf(declaredType, rootTail.ExpectedType));
            this.forType = forType;
            this.declaredType = declaredType;
            this.rootTail = rootTail;
            toTail = GetConversion(model, true);
            fromTail = GetConversion(model, false);
        }
        private static bool HasCast(TypeModel model, Type type, Type from, Type to, out MethodInfo op)
        {
#if WINRT
            System.Collections.Generic.List<MethodInfo> list = new System.Collections.Generic.List<MethodInfo>();
            foreach (var item in type.GetRuntimeMethods())
            {
                if (item.IsStatic) list.Add(item);
            }
            MethodInfo[] found = list.ToArray();
#else
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] found = type.GetMethods(flags);
#endif
            ParameterInfo[] paramTypes;
            Type convertAttributeType = null;
            for (int i = 0; i < found.Length; i++)
            {
                MethodInfo m = found[i];
                if (m.ReturnType != to) continue;
                paramTypes = m.GetParameters();
                if(paramTypes.Length == 1 && paramTypes[0].ParameterType == from)
                {
                    if (AttributeMap.GetAttribute(AttributeMap.Create(model, m, false), "AqlaSerializer.ProtoConverterAttribute") != null
                        || AttributeMap.GetAttribute(AttributeMap.Create(model, m, false), "AqlaSerializer.SurrogateConverterAttribute") != null)
                    {
                        op = m;
                        return true;
                    }
                }
            }

            for(int i = 0 ; i < found.Length ; i++)
            {
                MethodInfo m = found[i];
                if ((m.Name != "op_Implicit" && m.Name != "op_Explicit") || m.ReturnType != to)
                {
                    continue;
                }
                paramTypes = m.GetParameters();
                if(paramTypes.Length == 1 && paramTypes[0].ParameterType == from)
                {
                    op = m;
                    return true;
                }
            }
            op = null;
            return false;
        }

        public MethodInfo GetConversion(TypeModel model, bool toTail)
        {
            Type to = toTail ? declaredType : forType;
            Type from = toTail ? forType : declaredType;
            MethodInfo op;
            if (HasCast(model, declaredType, from, to, out op) || HasCast(model, forType, from, to, out op))
            {
                return op;
            }
            throw new InvalidOperationException("No suitable conversion operator found for surrogate: " +
                forType.FullName + " / " + declaredType.FullName);
        }

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter writer)
        {
            rootTail.Write(toTail.Invoke(null, new object[] { value }), writer);
        }
        public object Read(object value, ProtoReader source)
        {
            var reservedTrap = ProtoReader.ReserveNoteObject(source);
            // convert the incoming value
            object[] args = { value };
            value = toTail.Invoke(null, args); // don't note, references are not to surrogate but to the final object
            // invoke the tail and convert the outgoing value
            args[0] = rootTail.Read(value, source);
            var r = fromTail.Invoke(null, args);
            ProtoReader.NoteReservedTrappedObject(reservedTrap, r, source); // TODO emit
            return r;
        }
#endif

#if FEAT_COMPILER
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            Helpers.DebugAssert(valueFrom != null); // don't support stack-head for this
            using (Compiler.Local converted = new Compiler.Local(ctx, declaredType)) // declare/re-use local
            {
                ctx.LoadValue(valueFrom); // load primary onto stack
                ctx.EmitCall(toTail); // static convert op, primary-to-surrogate

                if (!Helpers.IsValueType(toTail.ReturnType))
                    ctx.CopyValue();

                ctx.StoreValue(converted); // store into surrogate local

                Compiler.CodeLabel afterNoteObject = default(Compiler.CodeLabel);
                if (!Helpers.IsValueType(toTail.ReturnType))
                {
                    afterNoteObject = ctx.DefineLabel();
                    ctx.BranchIfFalse(afterNoteObject, true);
                }
                ctx.LoadValue(converted);
                ctx.CastToObject(toTail.ReturnType);
                ctx.EmitCallNoteObject();
                if (!Helpers.IsValueType(toTail.ReturnType))
                    ctx.MarkLabel(afterNoteObject);

                rootTail.EmitRead(ctx, converted); // downstream processing against surrogate local

                ctx.LoadValue(converted); // load from surrogate local
                ctx.EmitCall(fromTail);  // static convert op, surrogate-to-primary
                ctx.StoreValue(valueFrom); // store back into primary
            }
        }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.LoadValue(valueFrom);
            ctx.EmitCall(toTail);
            rootTail.EmitWrite(ctx, null);
        }
#endif
    }
}
#endif