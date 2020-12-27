// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    using System.Collections.Generic;
    using System.IO;
    using AqlaSerializer;

    [TestFixture]
    public class SO7333233
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(2, typeof(Ant)), ProtoBuf.ProtoInclude(3, typeof(Cat))]
        public interface IBeast
        {
            [ProtoBuf.ProtoMember(1)]
            string Name { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Ant : IBeast
        {
            public string Name { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Cat : IBeast
        {
            public string Name { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public interface IRule<T> where T : IBeast
        {
            bool IsHappy(T beast);
        }

        [ProtoBuf.ProtoContract]
        public class AntRule1 : IRule<IAnt>, IRule<Ant>
        {
            public bool IsHappy(IAnt beast)
            {
                return true;
            }
            public bool IsHappy(Ant beast)
            {
                return true;
            }
        }

        [ProtoBuf.ProtoContract]
        public class AntRule2 : IRule<IAnt>, IRule<Ant>
        {
            public bool IsHappy(IAnt beast)
            {
                return true;
            }
            public bool IsHappy(Ant beast)
            {
                return true;
            }
        }

        public interface ICat : IBeast
        {
        }

        public interface IAnt : IBeast
        {
        }


        [ProtoBuf.ProtoContract]
        public class CatRule1 : IRule<ICat>, IRule<Cat>
        {
            public bool IsHappy(ICat beast)
            {
                return true;
            }
            public bool IsHappy(Cat beast)
            {
                return true;
            }
        }

        [ProtoBuf.ProtoContract]
        public class CatRule2 : IRule<ICat>, IRule<Cat>
        {
            public bool IsHappy(ICat beast)
            {
                return true;
            }
            public bool IsHappy(Cat beast)
            {
                return true;
            }
        }

        [Test]
        public  void Execute()
        {
            // note these are unrelated networks, so we can use the same field-numbers
            RuntimeTypeModel.Default[typeof(IRule<Ant>)].AddSubType(1, typeof(AntRule1)).AddSubType(2, typeof(AntRule2));
            RuntimeTypeModel.Default[typeof(IRule<Cat>)].AddSubType(1, typeof(CatRule1)).AddSubType(2, typeof(CatRule2));

            var antRules = new List<IRule<Ant>>();
            antRules.Add(new AntRule1());
            antRules.Add(new AntRule2());

            var catRules = new List<IRule<Cat>>();
            catRules.Add(new CatRule1());
            catRules.Add(new CatRule2());

            using (var fs = File.Create(@"antRules.bin"))
            {
                AqlaSerializer.Serializer.Serialize(fs, antRules);

                fs.Close();
            }

            using (var fs = File.OpenRead(@"antRules.bin"))
            {
                List<IRule<Ant>> list;
                list = AqlaSerializer.Serializer.Deserialize<List<IRule<Ant>>>(fs);

                fs.Close();
            }

            using (var fs = File.Create(@"catRules.bin"))
            {
                AqlaSerializer.Serializer.Serialize(fs, catRules);

                fs.Close();
            }

            using (var fs = File.OpenRead(@"catRules.bin"))
            {
                List<IRule<Cat>> list;
                list = AqlaSerializer.Serializer.Deserialize<List<IRule<Cat>>>(fs);

                fs.Close();
            }
        }
    }
}
