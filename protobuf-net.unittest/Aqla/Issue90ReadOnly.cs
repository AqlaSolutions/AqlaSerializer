using AqlaSerializer.Meta;
using System.Collections.Generic;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.unittest.Aqla
{

    [TestFixture]
    public class Issue90ReadOnly
    {
        [ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.AllPublic)]
        public class Sheep
        {
            public IReadOnlyCollection<string> Children { get; set; }
            public Sheep()
            {
                Children = new List<string>();
            }
        }

        [Test]
        public void Execute()
        {
            var dolly = TypeModel.Create().DeepClone(
                new Sheep
                {
                    Children = new[]
                    {
                        "Bonnie",
                        "Sally",
                        "Rosie",
                        "Lucy",
                        "Darcy",
                        "Cotton"
                    }
                });
        }
    }
}