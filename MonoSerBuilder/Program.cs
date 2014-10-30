﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Reflection;
using AqlaSerializer.Meta;

namespace MonoSerBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            var model = TypeModel.Create();
            var type = Type.GetType("MonoDto.OrderHeader, MonoDto");
            model.Add(type, true);
            type = Type.GetType("MonoDto.OrderDetail, MonoDto");
            model.Add(type, true);
            model.Compile("OrderSerializer", "MonoDtoSerializer.dll");
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("aqlaserializer")) return typeof (AqlaSerializer.Serializer).Assembly;
            return null;
        }
    }
}
