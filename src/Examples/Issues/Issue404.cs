﻿using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue404
    {
        [ProtoContract]
        [ProtoInclude(5, typeof(Person))]
        [ProtoInclude(6, typeof(Address))]
        class Entity
        {
            [ProtoMember(1)]
            public Guid Id { get; set; }
        }

        [ProtoContract]
        class Person : Entity
        {
            [ProtoMember(1)]
            public string Name { get; set; }
            [ProtoMember(2)]
            public Address Address { get; set; }
        }

        [ProtoContract]
        class Address : Entity
        {
            [ProtoMember(1)]
            public string Line1 { get; set; }
            [ProtoMember(2)]
            public string Line2 { get; set; }
        }
        [Test]
        public void GuidsAreNonZero()
        {
            List<Person> persons = new List<Person>(), cloneList;
            for (int i = 0; i < 10; i++)
            {
                Person person = new Person
                {
                    Id = Guid.NewGuid(),
                    Name = $"myName: {i}",
                    Address = new Address
                    {
                        Id = Guid.NewGuid(),
                        Line1 = $"Line1: {i}",
                        Line2 = $"Line2: {i}",
                    }
                };
                persons.Add(person);
            }
            Serializer.PrepareSerializer<Person>();

            using (var file = new MemoryStream())
            {
                // Serialize{
                Serializer.Serialize(file, persons);
                file.Position = 0;
                // Deserialize
                cloneList = Serializer.Deserialize<List<Person>>(file);
            }
            Assert.AreEqual(persons.Count, cloneList.Count);
            for(int i = 0; i < persons.Count; i++)
            {
                var x = persons[i];
                var y = cloneList[i];

                Assert.AreEqual(x.Id, y.Id);
                Assert.AreEqual(x.Name, y.Name);
                var xA = x.Address;
                var yA = y.Address;
                Assert.AreEqual(xA.Id, yA.Id);
                Assert.AreEqual(xA.Line1, yA.Line1);
                Assert.AreEqual(xA.Line2, yA.Line2);
            }
        }
    }
}
