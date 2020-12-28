using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    /// <summary>
    /// List handler disabling after adding member of such type with enabled list handling
    /// </summary>
    [TestFixture]
    public class Issue7ListHandlingCallbacks2
    {
        [Test]
        public void Execute()
        {
            var model = TypeModel.Create();
            model.AutoAddMissingTypes = false;
            model.AutoCompile = false;
            // register container type
            this.RegisterListType(model, typeof(TestList<string>));
            model.Add(typeof(Container), true).AddField(1, "Content").AsReference = false;
            model.SkipCompiledVsNotCheck = true;
            model.CompileInPlace();

            var originalList = new List<string>() { "1", "2", "3" };

            // check without container
            {
                var deserializedList = model.DeepClone(new TestList<string>(originalList));

                Assert.That(deserializedList.InnerList, Is.EqualTo(originalList));
                Assert.That(deserializedList.IsDeserializationCalled, Is.EqualTo(true));
                // please note we've set metaType.UseConstructor = false
                Assert.That(deserializedList.IsDefaultConstructorCalled, Is.EqualTo(false));
            }

            // check with container
            {
                var deserialized = model.DeepClone(new Container() { Content = new TestList<string>(originalList) });
                var deserializedList = deserialized.Content;

                Assert.That(deserializedList.InnerList, Is.EqualTo(originalList));
                Assert.That(deserializedList.IsDeserializationCalled, Is.EqualTo(true));
                // please note we've set metaType.UseConstructor = false
                Assert.That(deserializedList.IsDefaultConstructorCalled, Is.EqualTo(false));
            }
        }

        private void RegisterListType(RuntimeTypeModel model, Type type)
        {
            var metaType = model.Add(type, applyDefaultBehaviourIfNew: false);
            metaType.UseConstructor = false;
            metaType.IgnoreListHandling = true;
            metaType.AsReferenceDefault = true;

            metaType.AddField(1, "innerList").AsReference = false;

            metaType.Callbacks.AfterDeserialize = type.GetMethod(
                "AfterDeserializeAsSubObject",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(metaType.Callbacks.AfterDeserialize, !Is.EqualTo(null));
        }

        public class Container
        {
            public TestList<string> Content { get; set; }
        }

        public class TestList<T> : TestBaseClass, IList<T>
        {
            private readonly List<T> innerList;

            public TestList(List<T> list)
            {
                this.innerList = list;
            }

            public TestList() : base()
            {
            }

            public int Count { get; }

            public List<T> InnerList => this.innerList;

            public bool IsReadOnly { get; }

            public T this[int index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public void Add(T item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(T item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<T> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public int IndexOf(T item)
            {
                throw new NotImplementedException();
            }

            public void Insert(int index, T item)
            {
                throw new NotImplementedException();
            }

            public bool Remove(T item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }

    public abstract class TestBaseClass
    {
        public bool IsDefaultConstructorCalled;

        public bool IsDeserializationCalled;

        protected TestBaseClass()
        {
            this.IsDefaultConstructorCalled = true;
        }

        [AfterDeserializationCallback]
        protected void AfterDeserializeAsSubObject(SerializationContext context)
        {
            //Assert.That(context.Context, !Is.EqualTo(null));
            this.IsDeserializationCalled = true;
        }
    }
}