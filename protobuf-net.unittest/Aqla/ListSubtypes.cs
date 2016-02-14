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
        [SerializeDerivedType(1, typeof(ListSubType7))]
        [SerializeDerivedType(2, typeof(Middle))]
        //[SerializeDerivedType(3, typeof(ListSubType2))]
        [SerializeDerivedType(4, typeof(ListSubType3))]
        public class ListType : List<int>
        {
        }

        [SerializableType]
        //[SerializeDerivedType(1, typeof(ListSubType2))]
        [SerializeDerivedType(2, typeof(ListSubType4))]
        public class Middle : ListType
        {
        }

        //[SerializableType]
        //public class ListSubType2 : ListSubType1
        //{
        //}

        [SerializableType]
        public class ListSubType3 : Middle
        {
        }

        [SerializableType]
        public class ListSubType4 : Middle
        {
        }

        [SerializableType]
        public class ListSubType5 : Middle
        {
        }

        [SerializableType]
        public class ListSubType6 : ListType
        {
        }

        [SerializableType]
        public class ListSubType7 : ListType
        {
        }

        [SerializableType]
        public class BaseContainer
        {
            [SerializableMember(1, CollectionConcreteType = typeof(ListType))]
            public ListType List { get; set; }
        }

        [TestCase(typeof(ListType), typeof(ListType), TestName = "Base class")]
        [TestCase(typeof(Middle), typeof(Middle), TestName = "Derived class")]
        //[TestCase(typeof(ListSubType2), typeof(ListSubType2), TestName = "Derived public class after middle defined in base and middle")]
        [TestCase(typeof(ListSubType3), typeof(Middle), TestName = "Derived public class after middle defined in base only AFTER MIDDLE",
            Description = "SubTypes are processed in field asc order. So the first applicable subtype is MIDDLE. " +
                          "Don't jump through middle type or register derived type first.")]
        [TestCase(typeof(ListSubType7), typeof(ListSubType7), TestName = "Derived public class after middle defined in base only BEFORE MIDDLE")]
        [TestCase(typeof(ListSubType4), typeof(ListSubType4), TestName = "Derived public class after middle defined in middle")]
        [TestCase(typeof(ListSubType5), typeof(Middle), TestName = "Derived public class after middle not defined - will return middle")]
        [TestCase(typeof(ListSubType6), typeof(ListType), TestName = "Derived public class not defined - will return base")]
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