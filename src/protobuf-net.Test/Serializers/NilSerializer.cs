// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Text;
using AqlaSerializer.Compiler;
using NUnit.Framework;
using AqlaSerializer.unittest.Serializers;

namespace AqlaSerializer.Serializers
{
    [TestFixture]
    public class NilTests
    {
        [Test]
        public void NilShouldAddNothing() {
            Util.Test("123", nil => nil, "");
        }
    }
    sealed class NilSerializer : IProtoSerializer
    {
        private readonly Type type;
        public bool CanCancelWriting { get; }
        public bool EmitReadReturnsValue { get { return true; } }
        public bool RequiresOldValue { get { return true; } }
        public object Read(object value, ProtoReader reader) { return value; }
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
        
        }

        Type IProtoSerializer.ExpectedType { get { return type; } }
        public NilSerializer(Type type) { this.type = type; }
        void IProtoSerializer.Write(object value, ProtoWriter dest) { }

        void IProtoSerializer.EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            // burn the value off the stack if needed (creates a variable and does a stloc)
            using (Local tmp = ctx.GetLocalWithValue(type, valueFrom)) { }
        }
        void IProtoSerializer.EmitRead(CompilerContext ctx, Local valueFrom)
        {
            throw new NotImplementedException();
        }
    }
}
