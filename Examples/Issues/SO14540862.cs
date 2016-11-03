// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System.Diagnostics;
using NUnit.Framework;
using AqlaSerializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Examples.Issues
{
    [TestFixture]
    public class SO14540862
    {
        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(10, typeof(Derived))]
        public class Base
        {
            [ProtoBuf.ProtoMember(1)]
            public string BaseFirstProperty { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public string BaseSecProperty { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Derived : Base
        {
            [ProtoBuf.ProtoMember(1)]
            public string DerivedFirstProperty { get; set; }
        }

        [Test]
        public void Execute()
        {
            var assembly = Assembly.LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aqlaserializer.dll"));
            var derived = new Derived()
            {
                BaseFirstProperty = "BaseFirst",
                BaseSecProperty = "BaseSec",
                DerivedFirstProperty = "DerivedFirst"
            };

            var reflectionSerializer = assembly.GetType("AqlaSerializer.Serializer");
            var getTypeSerializer = typeof(Serializer);

            var reflectionMethods = reflectionSerializer.GetMethods(BindingFlags.Static | BindingFlags.Public);
            var reflectionGenericMethodInfo = reflectionMethods.First<MethodInfo>(method => method.Name == "SerializeWithLengthPrefix" && method.GetParameters().Length == 3);
            var reflectionSpecificMethodInfo = reflectionGenericMethodInfo.MakeGenericMethod(new Type[] { derived.GetType() });

            var getTypeMethods = getTypeSerializer.GetMethods(BindingFlags.Static | BindingFlags.Public);
            var getTypeGenericMethodInfo = getTypeMethods.First<MethodInfo>(method => method.Name == "SerializeWithLengthPrefix" && method.GetParameters().Length == 3);
            var getTypeSpecificMethodInfo = getTypeGenericMethodInfo.MakeGenericMethod(new Type[] { derived.GetType() });

            var reflectionStream = new MemoryStream();
            var getTypeStream = new MemoryStream();
            reflectionSpecificMethodInfo.Invoke(null, new object[] { reflectionStream, derived, PrefixStyle.Base128 });
            getTypeSpecificMethodInfo.Invoke(null, new object[] { getTypeStream, derived, PrefixStyle.Base128 });
            
            //Assert.AreEqual(37, (int)reflectionStream.Length, "loaded dynamically");
            //Assert.AreEqual(37, (int)getTypeStream.Length, "loaded statically");
            
        }
    }
}
