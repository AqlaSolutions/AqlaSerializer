// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Examples.Issues
{
    [TestFixture]
    public class SO13802844
    {
        [ProtoBuf.ProtoContract]
        enum AnimationCode {
            [ProtoBuf.ProtoEnum(Name = "AnimationCode_None")]
            None = 0,
            Idle = 1
        }

        [Test]
        public void Execute()
        {
            string s = Serializer.GetProto<AnimationCode>();

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples.Issues;

enum AnimationCode {
   AnimationCode_None = 0;
   Idle = 1;
}
", actual: s);
        }
    }
}
