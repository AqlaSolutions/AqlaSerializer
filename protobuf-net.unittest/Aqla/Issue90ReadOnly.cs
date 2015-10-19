using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.unittest.Aqla
{

    [TestFixture]
    public class Issue90ReadOnly
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
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