using AqlaSerializer.unittest;
using System;
using ProtoBuf.Meta;
using Xunit;
using Assert = Xunit.Assert;
using NUnit.Framework;

namespace ProtoBuf.unittest.Serializers
{
    [TestFixture]
    public class UriTests
    {
        public class TypeWithUri
        {
            public Uri Value { get; set; }
        }

        [Test]
        [TestCase("http://example.com", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource with spaces/", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource%20with%20spaces%20encoded", UriKind.Absolute)]
        [TestCase("http://example.com/withquerystring?param1=1&param2=second", UriKind.Absolute)]
        [TestCase("http://example.com/withfragment?param=test#anchorname", UriKind.Absolute)]
        [TestCase("/relative/path/to/file.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file with spaces.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file%20with%20spaces%20encoded.txt", UriKind.Relative)]
        public void TestUriRuntime(string uriString, UriKind uriKind)
        {
            var model = CreateModel();

            var obj = new TypeWithUri { Value = new Uri(uriString, uriKind) };
            TypeWithUri clone = (TypeWithUri) model.DeepClone(obj);
            Assert.Equal(obj.Value, clone.Value);
        }

        [Test]
        [TestCase("http://example.com", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource with spaces/", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource%20with%20spaces%20encoded", UriKind.Absolute)]
        [TestCase("http://example.com/withquerystring?param1=1&param2=second", UriKind.Absolute)]
        [TestCase("http://example.com/withfragment?param=test#anchorname", UriKind.Absolute)]
        [TestCase("/relative/path/to/file.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file with spaces.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file%20with%20spaces%20encoded.txt", UriKind.Relative)]
        public void TestUriInPlace(string uriString, UriKind uriKind)
        {
            var model = CreateModel();
            model.CompileInPlace();
            var obj = new TypeWithUri { Value = new Uri(uriString, uriKind) };

            TypeWithUri clone = (TypeWithUri)model.DeepClone(obj);

            Assert.Equal(obj.Value, clone.Value);
        }

        [Test]
        public void TestUriCanCompileFully()
        {
            var model = CreateModel().Compile("TestUriCanCompileFully", "TestUriCanCompileFully.dll");
            PEVerify.Verify("TestUriCanCompileFully.dll");
        }

        [Test]
        [TestCase("http://example.com", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource with spaces/", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource%20with%20spaces%20encoded", UriKind.Absolute)]
        [TestCase("http://example.com/withquerystring?param1=1&param2=second", UriKind.Absolute)]
        [TestCase("http://example.com/withfragment?param=test#anchorname", UriKind.Absolute)]
        [TestCase("/relative/path/to/file.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file with spaces.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file%20with%20spaces%20encoded.txt", UriKind.Relative)]
        public void TestUriCompiled(string uriString, UriKind uriKind)
        {
            var model = CreateModel().Compile();

            var obj = new TypeWithUri { Value = new Uri(uriString, uriKind) };

            TypeWithUri clone = (TypeWithUri)model.DeepClone(obj);
            Assert.Equal(obj.Value, clone.Value);
        }

        [Test]
        [TestCase("http://example.com", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource with spaces/", UriKind.Absolute)]
        [TestCase("http://example.com/path/to/resource%20with%20spaces%20encoded", UriKind.Absolute)]
        [TestCase("http://example.com/withquerystring?param1=1&param2=second", UriKind.Absolute)]
        [TestCase("http://example.com/withfragment?param=test#anchorname", UriKind.Absolute)]
        [TestCase("/relative/path/to/file.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file with spaces.txt", UriKind.Relative)]
        [TestCase("/relative/path/to/file%20with%20spaces%20encoded.txt", UriKind.Relative)]
        public void TestUriDirect(string uriString, UriKind uriKind)
        {
            var model = AqlaSerializer.Meta.TypeModel.Create();

            var obj = new Uri(uriString, uriKind);
            Uri clone = (Uri)model.DeepClone(obj);
            Assert.Equal(obj, clone);
        }

        static AqlaSerializer.Meta.RuntimeTypeModel CreateModel()
        {
            var model = AqlaSerializer.Meta.TypeModel.Create();
            model.Add(typeof(TypeWithUri), false)
                .Add(1, "Value");
            return model;
        }
    }
}