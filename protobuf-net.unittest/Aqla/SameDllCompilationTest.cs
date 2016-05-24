using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class SameDllCompilationTest
    {
        [SerializableType]
        public class Foo
        {
            public int A { get; set; }
        }

        [SerializableType]
        public class Bar
        {
            public Foo B { get; set; }
        }

        [Test]
        public void ExecuteSame()
        {
            string assemblyName = nameof(SameDllCompilationTest) + ".dll";
            try
            {
                File.Delete(assemblyName);
            }
            catch
            {
            }

            Compile(assemblyName, true);
            using (File.Open(assemblyName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Compile(assemblyName, true);
                Compile(assemblyName, true);
                Assert.That(() => Compile(assemblyName, false), Throws.TypeOf<IOException>());
            }
        }

        static void Compile(string assemblyName, bool check)
        {
            var tm = TypeModel.Create();
            tm.Add(typeof(Foo), true);
            tm.Add(typeof(Bar), true);
            tm.Compile(
                new RuntimeTypeModel.CompilerOptions()
                {
                    OutputPath = assemblyName,
                    IterativeMode = check ? RuntimeTypeModel.CompilerIterativeMode.ReadAndAppendData : RuntimeTypeModel.CompilerIterativeMode.AppendData,
                    TypeName = nameof(SameDllCompilationTest),
                });
        }
    }
}