using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class MemberRemoveVersioning
    {
        [SerializableType(ImplicitFields = ImplicitFieldsMode.None, EnumPassthru = true)]
        public class CustomClass
        {
            [SerializableMember(1)]
            public int Property { get; set; }
        }

        [SerializableType(ImplicitFields = ImplicitFieldsMode.None, EnumPassthru = true)]
        public class ContainerSimple
        {
            [SerializableMember(1)]
            public int Foo { get; set; }

            [SerializableMember(2)]
            public CustomClass Bar { get; set; }

            [SerializableMember(3)]
            public int Baz { get; set; }
        }

        [SerializableType(ImplicitFields = ImplicitFieldsMode.None, EnumPassthru = true)]
        public class ContainerList
        {
            [SerializableMember(1)]
            public int Foo { get; set; }

            [SerializableMember(2)]
            public List<CustomClass> Bar { get; set; }

            [SerializableMember(3)]
            public int Baz { get; set; }
        }

        [SerializableType(ImplicitFields = ImplicitFieldsMode.None, EnumPassthru = true)]
        public class ContainerFinal
        {
            [SerializableMember(1)]
            public int Foo { get; set; }

            [SerializableMember(3)]
            public int Baz { get; set; }
        }

        [Test]
        public void ExecuteSimpleValue()
        {
            var tm = TypeModel.Create();
            var f = tm.ChangeType<ContainerSimple, ContainerFinal>(new ContainerSimple() { Foo = 1, Baz = 2, Bar = new CustomClass() });
            Assert.That(f.Foo, Is.EqualTo(1));
            Assert.That(f.Baz, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteSimpleNull()
        {
            var tm = TypeModel.Create();
            var f = tm.ChangeType<ContainerSimple, ContainerFinal>(new ContainerSimple() { Foo = 1, Baz = 2, Bar = null });
            Assert.That(f.Foo, Is.EqualTo(1));
            Assert.That(f.Baz, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteListValue()
        {
            var tm = TypeModel.Create();
            var f = tm.ChangeType<ContainerList, ContainerFinal>(new ContainerList() { Foo = 1, Baz = 2, Bar = new List<CustomClass>() { new CustomClass() } });
            Assert.That(f.Foo, Is.EqualTo(1));
            Assert.That(f.Baz, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteListEmpty()
        {
            var tm = TypeModel.Create();
            var f = tm.ChangeType<ContainerList, ContainerFinal>(new ContainerList() { Foo = 1, Baz = 2, Bar = new List<CustomClass>() });
            Assert.That(f.Foo, Is.EqualTo(1));
            Assert.That(f.Baz, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteListNull()
        {
            var tm = TypeModel.Create();
            var f = tm.ChangeType<ContainerList, ContainerFinal>(new ContainerList() { Foo = 1, Baz = 2, Bar = null });
            Assert.That(f.Foo, Is.EqualTo(1));
            Assert.That(f.Baz, Is.EqualTo(2));
        }
    }
}