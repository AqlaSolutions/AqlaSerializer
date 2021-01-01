﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Examples.Issues
{
    [TestFixture]
    public class SO13162642
    {
        [Ignore("See it later, very slow"), Test]
        public void Execute()
        {
            using (var f = File.Create("Data.protobuf"))
            {
                Serializer.Serialize<IEnumerable<DTO>>(f, GenerateData(100000));
            }

            using (var f = File.OpenRead("Data.protobuf"))
            {
                var dtos = Serializer.DeserializeItems<DTO>(f, AqlaSerializer.PrefixStyle.Base128, 1);
                Console.WriteLine(dtos.Count());
            }
            Console.Read();
        }

        [Ignore("Ok, see it later, very slow"), Test]
        public void ExecuteWorkaround()
        {
            using (var f = File.Create("Data.protobuf"))
            {
                foreach(var obj in GenerateData(1000000))
                {
                    Serializer.SerializeWithLengthPrefix<DTO>(
                        f, obj, PrefixStyle.Base128, Serializer.ListItemTag);
                }
            }

            using (var f = File.OpenRead("Data.protobuf"))
            {
                var dtos = Serializer.DeserializeItems<DTO>(f, AqlaSerializer.PrefixStyle.Base128, 1);
                Console.WriteLine(dtos.Count());
            }
            Console.Read();
        }

        static IEnumerable<DTO> GenerateData(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // reduce to 1100 to use much less memory
                var dto = new DTO { Data = new byte[1101] };
                for (int j = 0; j < dto.Data.Length; j++)
                {
                    // fill with data
                    dto.Data[j] = (byte)(i + j);
                }
                yield return dto;
            }
        }

        [ProtoBuf.ProtoContract]
        public class DTO
        {
            [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.Group)]
            public byte[] Data { get; set; }
        }
    }
}
