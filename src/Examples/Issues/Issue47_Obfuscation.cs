using AqlaSerializer;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Examples.Issues
{
    public class Issue47_Obfuscation
    {
        [Test]
        public void Test()
        {
#if DEBUG

            var assembly = Assembly.LoadFrom(@"..\..\..\..\..\assorted\Obfuscated\bin\Debug\Obfuscated.dll");
#else
            var assembly = Assembly.LoadFrom(@"..\..\..\..\..\assorted\Obfuscated\bin\Release\Obfuscated.dll");
#endif
            Type obfuscatedType = assembly.GetType("a");
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
    }
}
