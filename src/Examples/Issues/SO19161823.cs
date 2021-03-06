﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class SO19161823
    {
        [Test]
        public void Execute([Values(false, true)] bool compile)
        {
            var model = TypeModel.Create();
            model.AutoCompile = compile;
            model.Add(typeof(IDummy), false)
                .SetSurrogate(typeof(DummySurrogate));

            var container = new Container { Data = new Dummy { Positive = 3 } };

            using (var file = File.Create("test.bin"))
            {
                model.Serialize(file, container);
            }

            using (var file = File.OpenRead("test.bin"))
            {
                container = model.Deserialize<Container>(file);
                Assert.AreEqual(3, container.Data.Positive);
            }
        }
        // Outside of the project, cannot be changed
        public interface IDummy
        {
            int Positive { get; set; }
        }


        [SerializableType]
        public class Container
        {
            [SerializableMember(1)]
            public IDummy Data { get; set; }
        }

        public class Dummy : IDummy
        {
            public int Positive { get; set; }
        }

        [SerializableType]
        public class DummySurrogate
        {
            [SerializableMember(1)]
            public int Negative { get; set; }

            [SurrogateConverter]
            public static IDummy From(DummySurrogate value)
            {
                return value == null ? null : new Dummy { Positive = -value.Negative };
            }

            [SurrogateConverter]
            public static DummySurrogate To(IDummy value)
            {
                return value == null ? null : new DummySurrogate { Negative = -value.Positive };
            }
        }
    }
}
