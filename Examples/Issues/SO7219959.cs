﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO7219959
    {
        [Test]
        public void Test()
        {
            Family family = new Family();
            Child child1 = new Child(1);
            Child child2 = new Child(2);
            Parent parent = new Parent(new List<Child>() {child1, child2});
            family.Add(parent);

            string file = "sandbox.txt";

            try
            {
                File.Delete(file);
            }
            catch
            {
            }

            using (var fs = File.OpenWrite(file))
            {
                fs.SetLength(0);
                Serializer.Serialize(fs, family);
            }
            using (var fs = File.OpenRead(file))
            {
                family = Serializer.Deserialize<Family>(fs);
            }

            System.Diagnostics.Debug.Assert(family != null, "1. Expect family not null, but not the case.");
        }


        [ProtoBuf.ProtoContract()]
        public class Child
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)] internal Parent Parent;

            private Child()
            {
            }

            public Child(int i)
            {
            }
        }

        [ProtoBuf.ProtoContract(SkipConstructor = true)]
        public class Parent
        {
            [ProtoBuf.ProtoMember(1)]
            protected List<Child> m_Children;

            /// <summary>
            /// ProtoBuf deserialization constructor (fails here)
            /// </summary>
            private Parent()
            {
                Initialize();
            }

            [ProtoBuf.ProtoBeforeDeserialization] // could also use OnDeserializing
            private void Initialize()
            {
                m_Children = new List<Child>();
            }

            public Parent(List<Child> children)
            {
                m_Children = children;
                m_Children.ForEach(x => x.Parent = this);
            }
        }

        [ProtoBuf.ProtoContract()]
        public class Family
        {
            [ProtoBuf.ProtoMember(1)] protected List<Parent> m_Parents;

            public void Add(Parent parent)
            {
                m_Parents.Add(parent);
            }

            public Family()
            {
                m_Parents = new List<Parent>();
            }
        }
    }
}