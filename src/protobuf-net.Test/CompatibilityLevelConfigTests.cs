using ProtoBuf.Meta;
using System;
using NUnit.Framework;


namespace ProtoBuf.Test
{
    // explore different ways of manually configuring the compatibility level
    public class CompatibilityLevelConfigTests
    {
        
        public CompatibilityLevelConfigTests()
            { }

        private string Log(string message)
        {
            _log?.WriteLine(message);
            return message;
        }

        private static TypeModel CreateModelVanilla()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            var mt = model.Add<SomeRandomType>();
            Assert.AreEqual(CompatibilityLevel.Level200, mt.CompatibilityLevel);
            return model;
        }

        private static TypeModel CreateModelDefaulted()
        {
            var model = RuntimeTypeModel.Create();
            model.DefaultCompatibilityLevel = CompatibilityLevel.Level300;
            model.AutoCompile = false;
            var mt = model.Add<SomeRandomType>();
            Assert.AreEqual(CompatibilityLevel.Level300, mt.CompatibilityLevel);
            return model;
        }

        private static TypeModel CreateModelCallback()
        {
            var model = RuntimeTypeModel.Create();
            model.BeforeApplyDefaultBehaviour += (s, e) => e.MetaType.CompatibilityLevel = CompatibilityLevel.Level300;
            model.AutoCompile = false;
            var mt = model.Add<SomeRandomType>();
            Assert.AreEqual(CompatibilityLevel.Level300, mt.CompatibilityLevel);
            return model;
        }

        [Test]
        public void VanillaSchema()
            => Assert.AreEqual(@"syntax = ""proto3"";
package ProtoBuf.Test;
import ""protobuf-net/bcl.proto""; // schema for protobuf-net's handling of core .NET types

message SomeRandomType {
   .bcl.Guid Id = 1; // default value could not be applied: 00000000-0000-0000-0000-000000000000
   .bcl.DateTime When = 2;
}
", Log(CreateModelVanilla().GetSchema(typeof(SomeRandomType), ProtoSyntax.Proto3)));

        [Test]
        public void ModelDefaultedSchema()
    => Assert.AreEqual(@"syntax = ""proto3"";
package ProtoBuf.Test;
import ""google/protobuf/timestamp.proto"";

message SomeRandomType {
   string Id = 1; // default value could not be applied: 00000000-0000-0000-0000-000000000000
   .google.protobuf.Timestamp When = 2;
}
", Log(CreateModelDefaulted().GetSchema(typeof(SomeRandomType), ProtoSyntax.Proto3)));

        [Test]
        public void CallbackHookSchema()
    => Assert.AreEqual(@"syntax = ""proto3"";
package ProtoBuf.Test;
import ""google/protobuf/timestamp.proto"";

message SomeRandomType {
   string Id = 1; // default value could not be applied: 00000000-0000-0000-0000-000000000000
   .google.protobuf.Timestamp When = 2;
}
", Log(CreateModelCallback().GetSchema(typeof(SomeRandomType), ProtoSyntax.Proto3)));

        [ProtoContract]
        public class SomeRandomType
        {
            [ProtoMember(1)]
            public Guid Id { get; set; }
            [ProtoMember(2)]
            public DateTime When { get; set; }
        }
    }

    
}
