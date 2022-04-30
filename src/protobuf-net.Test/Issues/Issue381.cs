using ProtoBuf.Meta;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue381
    {
        [Test]
        public void CheckCompilerAvailable()
        {
            Assert.True(RuntimeTypeModel.EnableAutoCompile());
        }
    }
}
