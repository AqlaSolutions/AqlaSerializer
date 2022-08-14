using AqlaSerializer;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Examples.Issues
{
    public class Issue47_Obfuscation
    {
        Assembly _assembly;
        public Issue47_Obfuscation()
        {
#if DEBUG

            _assembly = Assembly.LoadFrom(@"..\..\..\..\..\assorted\Obfuscated\bin\Debug\Obfuscated.dll");
#else
            _assembly = Assembly.LoadFrom(@"..\..\..\..\..\assorted\Obfuscated\bin\Release\Obfuscated.dll");
#endif
        }

        [Test]
        public void TestDuplicateNames()
        {
            Type obfuscatedType = _assembly.GetType("a");
            var instance = Activator.CreateInstance(obfuscatedType);
            var membersWithTheSameName = obfuscatedType.GetMember("a", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.AreEqual(4, membersWithTheSameName.Length);

            var field = obfuscatedType.GetField("a");
            field.SetValue(instance, 8);
            var fieldValue = field.GetValue(instance);
            Assert.AreEqual(8, fieldValue);

            var metaType = AqlaSerializer.Meta.RuntimeTypeModel.Default.Add(obfuscatedType, false);
            metaType.DefaultFormat = ValueFormat.Reference;
            metaType.UseConstructor = false;
            metaType.IgnoreListHandling = true;
            var metaField = metaType.AddField(metaType.GetNextFreeFieldNumber(), "a");
            metaField.SetSettings(x => {
                x.V.Format = ValueFormat.Compact;
            });

            MemoryStream stream = new MemoryStream();
            Serializer.Serialize(stream, instance);
            stream.Position = 0;
            var clone = Serializer.Deserialize(obfuscatedType, stream);

            Assert.IsNotNull(clone);
            var cloneFieldValue = field.GetValue(clone);
            Assert.AreEqual(cloneFieldValue, 8);
        }

        [Test]
        public void TestDuplicateFieldAndPropertyNames()
        {
            Type obfuscatedType = _assembly.GetType("d");

            var metaType = AqlaSerializer.Meta.RuntimeTypeModel.Default.Add(obfuscatedType, false);

            var ex = Assert.Throws<ArgumentException>(() => {
                metaType.AddField(metaType.GetNextFreeFieldNumber(), "d");
            });

            Assert.That(ex.Message.StartsWith("Unable to determine member: d"));
            Assert.That(ex.Message.Contains("Parameter"));
            Assert.That(ex.Message.Contains("memberName"));
        }

        [Test]
        public void TestNonexistentFieldName()
        {
            Type obfuscatedType = _assembly.GetType("d");

            var metaType = AqlaSerializer.Meta.RuntimeTypeModel.Default.Add(obfuscatedType, false);

            var ex = Assert.Throws<ArgumentException>(() => {
                metaType.AddField(metaType.GetNextFreeFieldNumber(), "x");
            });

            Assert.That(ex.Message.StartsWith("Unable to determine member: x"));
            Assert.That(ex.Message.Contains("Parameter"));
            Assert.That(ex.Message.Contains("memberName"));
        }
    }
}
