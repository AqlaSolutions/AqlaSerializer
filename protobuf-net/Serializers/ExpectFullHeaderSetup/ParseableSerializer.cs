// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
    sealed class ParseableSerializer : IProtoSerializerWithAutoType
    {
        private readonly MethodInfo _parse;
        public static ParseableSerializer TryCreate(Type type, TypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
#if WINRT || PORTABLE
            MethodInfo method = null;
            
#if WINRT
            foreach (MethodInfo tmp in type.GetTypeInfo().GetDeclaredMethods("Parse"))
#else
            foreach (MethodInfo tmp in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
#endif
            {
                ParameterInfo[] p;
                if (tmp.Name == "Parse" && tmp.IsPublic && tmp.IsStatic && tmp.DeclaringType == type && (p = tmp.GetParameters()) != null && p.Length == 1 && p[0].ParameterType == typeof(string))
                {
                    method = tmp;
                    break;
                }
            }
#else
            MethodInfo method = type.GetMethod("Parse",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null, new Type[] { model.MapType(typeof(string)) }, null);
#endif
            if (method != null && method.ReturnType == type)
            {
                if (Helpers.IsValueType(type))
                {
                    MethodInfo toString = GetCustomToString(type);
                    if (toString == null || toString.ReturnType != model.MapType(typeof(string))) return null; // need custom ToString, fools
                }
                return new ParseableSerializer(method);
            }
            return null;
        }
        private static MethodInfo GetCustomToString(Type type)
        {
#if WINRT
            foreach (MethodInfo method in type.GetTypeInfo().GetDeclaredMethods("ToString"))
            {
                if (method.IsPublic && !method.IsStatic && method.GetParameters().Length == 0) return method;
            }
            return null;
#elif PORTABLE
            MethodInfo method = Helpers.GetInstanceMethod(type, "ToString", Helpers.EmptyTypes);
            if (method == null || !method.IsPublic || method.IsStatic || method.DeclaringType != type) return null;
            return method;
#else

            return type.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                        null, Helpers.EmptyTypes, null);
#endif
        }
        private ParseableSerializer(MethodInfo parse)
        {
            this._parse = parse;
        }
        public Type ExpectedType => _parse.DeclaringType;

        bool IProtoSerializer.RequiresOldValue => false;

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            var v = _parse.Invoke(null, new object[] { source.ReadString() });
            ProtoReader.NoteObject(v, source);
            return v;
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteString(value.ToString(), dest);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                Type type = ExpectedType;
                if (type.IsValueType)
                { // note that for structs, we've already asserted that a custom ToString
                    // exists; no need to handle the box/callvirt scenario

                    // force it to a variable if needed, so we can take the address
                    using (Compiler.Local loc = ctx.GetLocalWithValue(type, valueFrom))
                    {
                        ctx.LoadAddress(loc, type);
                        ctx.EmitCall(GetCustomToString(type));
                    }
                }
                else
                {
                    ctx.LoadValue(valueFrom);
                    if (valueFrom.Type != ctx.MapType(typeof(string)))
                        ctx.EmitCall(ctx.MapType(typeof(object)).GetMethod("ToString"));
                }
                ctx.EmitBasicWrite("WriteString", null);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadString", ctx.MapType(typeof(string)));
                ctx.EmitCall(_parse);
                ctx.CopyValue();
                ctx.CastToObject(_parse.ReturnType);
                ctx.EmitCallNoteObject();
            }
        }
#endif
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this);
        }
    }
}
#endif