// Created by Vladyslav Taranov for AqlaSerializer, 2016

#if FEAT_COMPILER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using AltLinq;
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
    class MethodContext : ICodeGenContext
    {
        public MemberInfo Member => _method.Member;
        public Type ReturnType => _method.ReturnType;
        public Type OwnerType => _method.OwnerType;
        public string Name => _method.Name;
        public bool IsStatic => _method.IsStatic;
        public bool IsOverride => _method.IsOverride;
        
        public Type[] ParameterTypes { get; }
        public bool IsParameterArray { get; }
        public bool SupportsScopes { get; }
        public ITypeMapper TypeMapper { get; }
        public StaticFactory StaticFactory { get; }
        public ExpressionFactory ExpressionFactory { get; }

        public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName)
        {
            throw new NotSupportedException();
        }

        public IParameterBasicInfo GetParameterByName(string parameterName)
        {
            return _method.Parameters.First(p => p.Name == parameterName);
        }

        public ILGenerator GetILGenerator()
        {
            return _il;
        }

        public void EndDefinition()
        {
            throw new NotSupportedException();
        }

        public void Complete()
        {
            throw new NotSupportedException();
        }

        readonly ILGenerator _il;
        readonly MethodGenInfo _method;
        public MethodContext(MethodGenInfo method, ILGenerator il, ITypeMapper typeMapper)
        {
            _method = method;
            _il = il;
            TypeMapper = typeMapper;
            ExpressionFactory = new ExpressionFactory(typeMapper);
            StaticFactory = new StaticFactory(TypeMapper);
            SupportsScopes = !method.IsDynamicGen;
            ParameterTypes = method.Parameters.Select(p => p.Type).ToArray();
            IsParameterArray = method.Parameters.LastOrDefault().IsParameterArray;
        }

        public struct MethodGenInfo
        {
            public ReadOnlyCollection<ParameterGenInfo> Parameters { get; }
            public Type ReturnType { get; }
            public Type OwnerType { get; }
            public string Name { get; }
            public MethodInfo Member { get; }
            public bool IsStatic { get; }
            public bool IsOverride { get; }
            public bool IsDynamicGen { get; }

            public MethodGenInfo(string name, MethodInfo member, bool isDynamicGen, bool isOverride, bool isStatic, Type returnType, Type ownerType, params ParameterGenInfo[] parameters)
            {
                IsDynamicGen = isDynamicGen;
                IsOverride = isOverride;
                IsStatic = isStatic;
                Member = member;
                Name = name;
                OwnerType = ownerType;
                ReturnType = returnType;
                Parameters = new ReadOnlyCollection<ParameterGenInfo>(parameters.ToArray());
            }
        }

        public struct ParameterGenInfo : IParameterBasicInfo
        {
            public bool IsParameterArray { get; }
            public int Position { get; }
            public Type Type { get; }
            public string Name { get; }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="type"></param>
            /// <param name="name"></param>
            /// <param name="position">From 1</param>
            /// <param name="isParameterArray"></param>
            public ParameterGenInfo(Type type, string name, int position, bool isParameterArray = false)
            {
                IsParameterArray = isParameterArray;
                Name = name;
                Position = position;
                Type = type;
            }
        }
    }

}
#endif
