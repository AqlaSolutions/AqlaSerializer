// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using AqlaSerializer;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace Examples.Issues
{
    // note that some additional changes were needed beyond what is shown on SO
    // in order to fully test standalone compilation / PEVerify; mainly due to
    // public readonly fields, which protobuf-net will still try and mutate
    [TestFixture] 
    public class SO11705351
    {
        [ProtoBuf.ProtoContract]
        public class Whole
        {
            private readonly PartCollection parts;

            public Whole() { parts = new PartCollection { Whole = this }; }
            [ProtoBuf.ProtoMember(1)]
            public PartCollection Parts { get { return parts; } }
        }

        [ProtoBuf.ProtoContract]
        public class Part
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public Whole Whole { get; set; }
        }

        [ProtoBuf.ProtoContract(IgnoreListHandling = true)]
        public class PartCollection : List<Part>
        {
            public Whole Whole { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Assemblage
        {
            private readonly PartCollection parts = new PartCollection();
            [ProtoBuf.ProtoMember(1)]
            public PartCollection Parts { get { return parts; }}
        }

        [ProtoBuf.ProtoContract]
        public class PartCollectionSurrogate
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public List<Part> Collection { get; set; }

            [ProtoBuf.ProtoMember(2, AsReference = true)]
            public Whole Whole { get; set; }

            public static implicit operator PartCollectionSurrogate(PartCollection value)
            {
                if (value == null) return null;
                return new PartCollectionSurrogate { Collection = value, Whole = value.Whole };
            }

            public static implicit operator PartCollection(PartCollectionSurrogate value)
            {
                if (value == null) return null;

                PartCollection result = new PartCollection {Whole = value.Whole};
                if(value.Collection != null)
                { // add the data we colated
                    result.AddRange(value.Collection);
                }
                return result;
            }
        }

        static RuntimeTypeModel GetModel()
        {
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(PartCollection), true).SetSurrogate(typeof(PartCollectionSurrogate));
            return model;
        }
        static Assemblage GetData()
        {
            var whole = new Whole();
            var part = new Part { Whole = whole };
            whole.Parts.Add(part);
            var assemblage = new Assemblage();
            assemblage.Parts.Add(part);
            return assemblage;
        }
        [Test]
        public void Execute()
        {
            var model = GetModel();
            Execute(model, "Runtime");
            model.CompileInPlace();
            Execute(model, "CompileInPlace");
            Execute(model.Compile(), "Compile");
            model.Compile("SO11705351", "SO11705351.dll");
            PEVerify.AssertValid("SO11705351.dll");
        }
        private static void Execute(TypeModel model, string caption)
        {
            //try
            //{
                using (var stream = new MemoryStream())
                {
                    {
                        var assemblage = GetData();
                        model.Serialize(stream, assemblage);
                    }

                    stream.Position = 0;

                    var obj = (Assemblage) model.Deserialize(stream, null, typeof (Assemblage));
                    {
                        var assemblage = obj;
                        var whole = assemblage.Parts[0].Whole;

                        Assert.AreSame(assemblage.Parts[0].Whole, whole.Parts[0].Whole, "Whole:" + caption);
                        Assert.AreSame(assemblage.Parts[0], whole.Parts[0], "Part:" + caption);
                    }
                }
            //} catch(Exception ex)
            //{
            //    Assert.Fail(ex.Message + ":" + caption);
            //}
        }

        [Test]
        public void CheckSchema()
        {
            var model = GetModel();
            model.Serialize(Stream.Null, GetData()); // to bring the other types into play

            string schema = model.GetSchema(null);

            Assert.AreEqual(@"package Examples.Issues;
import ""bcl.proto""; // schema for protobuf-net's handling of core .NET types

message Assemblage {
   optional PartCollectionSurrogate Parts = 1;
}
message Part {
   optional bcl.NetObjectProxy Whole = 1; // reference-tracked Whole
}
message PartCollectionSurrogate {
   repeated bcl.NetObjectProxy Collection = 1; // reference-tracked Part
   optional bcl.NetObjectProxy Whole = 2; // reference-tracked Whole
}
message Whole {
   optional PartCollectionSurrogate Parts = 1;
}
", schema);
        }
    }
}
