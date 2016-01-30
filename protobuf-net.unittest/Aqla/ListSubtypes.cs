using System;
using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class ListSubtypes
    {
        // ListSubType2 0 a type can only participiate in one inheritance hierarchy
        [SerializableType]
        [SerializeDerivedType(1, typeof(ListSubType1))]
        //[SerializeDerivedType(2, typeof(ListSubType2))]
        [SerializeDerivedType(3, typeof(ListSubType3))]
        public class ListType : List<int>
        {
        }

        [SerializableType]
        //[SerializeDerivedType(1, typeof(ListSubType2))]
        [SerializeDerivedType(2, typeof(ListSubType4))]
        public class ListSubType1 : ListType
        {
        }

        //[SerializableType]
        //public class ListSubType2 : ListSubType1
        //{
        //}

        [SerializableType]
        public class ListSubType3 : ListSubType1
        {
        }

        [SerializableType]
        public class ListSubType4 : ListSubType1
        {
        }

        [SerializableType]
        public class ListSubType5 : ListSubType1
        {
        }

        [SerializableType]
        public class ListSubType6 : ListType
        {
        }

        [SerializableType]
        public class BaseContainer
        {
            [SerializableMember(1, CollectionConcreteType = typeof(ListType))]
            public ListType List { get; set; }
        }

        [TestCase(typeof(ListType), typeof(ListType), TestName = "Base class")]
        [TestCase(typeof(ListSubType1), typeof(ListSubType1), TestName = "Derived class")]
        //[TestCase(typeof(ListSubType2), typeof(ListSubType2), TestName = "Derived class after middle defined in base and middle")]
        [TestCase(typeof(ListSubType3), typeof(ListSubType3), TestName = "Derived class after middle defined in base only")]
        [TestCase(typeof(ListSubType4), typeof(ListSubType1), TestName = "Derived class after middle defined in middle only - will return middle")]
        [TestCase(typeof(ListSubType5), typeof(ListSubType1), TestName = "Derived class after middle not defined - will return middle")]
        [TestCase(typeof(ListSubType6), typeof(ListType), TestName = "Derived class not defined - will return base")]
        public void DifferentSubTypes(Type subType, Type expected)
        {
            var tm = TypeModel.Create();
            var strategy = (DefaultAutoAddStrategy)tm.AutoAddStrategy;
            strategy.DisableAutoRegisteringSubtypes = true;
            var original = new BaseContainer();
            original.List = (ListType)Activator.CreateInstance(subType);
            original.List.Add(1);
            original.List.Add(2);
            original.List.Add(3);
            ListType copy = tm.DeepClone(original).List;
            Assert.That(copy, Is.EqualTo(original.List));
            Assert.That(copy.GetType(), Is.EqualTo(expected));
        }
    }
}