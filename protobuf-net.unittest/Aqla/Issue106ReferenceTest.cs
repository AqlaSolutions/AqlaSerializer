using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue106ReferenceTest
    {
        [Test]
        public void Test()
        {
            var model = TypeModel.Create();
            MainObject mainObject = new MainObject();
            TestObject testObject = new TestObject();
            ObjectByReference objectByReference = new ObjectByReference();

            mainObject.TestObjects.Add(testObject);
            mainObject.ObjectByReferences.Add(objectByReference);

            testObject.ObjectByReference = objectByReference;

            // Make sure the reference is the same before serialization.
            Debug.Assert(testObject.ObjectByReference == objectByReference);

            byte[] buf;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                model.Serialize(ms, mainObject);
                buf = ms.ToArray();
            }

            // --> Deserialize.
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(buf))
            {
                mainObject = model.Deserialize<MainObject>(ms);
            }

            testObject = mainObject.TestObjects[0];
            objectByReference = mainObject.ObjectByReferences[0];

            // Fails as now the reference suddenly is not the same anymore!
            Assert.AreSame(testObject.ObjectByReference, objectByReference);
        }

        [SerializableType]
        class MainObject
        {
            [SerializableMember(1)]
            public List<TestObject> TestObjects = new List<TestObject>();

            [SerializableMember(2)]
            public List<ObjectByReference> ObjectByReferences = new List<ObjectByReference>();
        }

        [SerializableType]
        class TestObject
        {
            [SerializableMember(1)]
            public ObjectByReference ObjectByReference { get; set; }
        }

        [SerializableType]
        class ObjectByReference
        {
        }
    }
}