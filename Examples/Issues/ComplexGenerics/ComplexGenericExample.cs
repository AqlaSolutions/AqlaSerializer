// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using AqlaSerializer.Meta;

namespace Examples.Issues.ComplexGenerics
{
/* Written in response to a question about how to handle multiple "packet" subclasses;
 * may as well keep it as a test...
 * */

    using AqlaSerializer;
    using System.Data;
    using NUnit.Framework;
    using System;
    using System.ComponentModel;
    using System.IO;

    [TestFixture]
    public class ComplexGenericTest
    {
        [Test]
        public void EnsureNoSkipInMiddle()
        {
            Assert.That(RuntimeTypeModel.Default.SkipCompiledVsNotCheck, Is.False);
        }

        [Test]
        public void TestX()
        {
            Query query = new X { Result = "abc" };
            Assert.AreEqual(typeof(string), query.GetQueryType());
            var tm = TypeModel.Create();
            Query clone = tm.DeepClone<Query>(query);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(clone, query);
            Assert.IsInstanceOfType(query.GetType(), clone);
            Assert.AreEqual(((X)query).Result, ((X)clone).Result);
        }
        [Test]
        public void TestY()
        {
            Query query = new Y { Result = 1234};
            Assert.AreEqual(typeof(int), query.GetQueryType());
            Query clone = Serializer.DeepClone<Query>(query);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(clone, query);
            Assert.IsInstanceOfType(query.GetType(), clone);
            Assert.AreEqual(((Y)query).Result, ((Y)clone).Result);
        }
        
    }
    public static class QueryExt {
        public static Type GetQueryType(this IQuery query)
        {
            if (query == null) throw new ArgumentNullException("query");
            foreach (Type type in query.GetType().GetInterfaces())
            {
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IQuery<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }
            throw new ArgumentException("No typed query implemented", "query");
        }
    }
    public interface IQuery
    {
        string Result { get; set; }
    }
    public interface IQuery<T> : IQuery
    {
        new T Result { get; set; }
    }

    [ProtoBuf.ProtoInclude(21, typeof(W))]
    [ProtoBuf.ProtoInclude(22, typeof(X))]
    [ProtoBuf.ProtoInclude(23, typeof(Y))]
    [ProtoBuf.ProtoInclude(25, typeof(SpecialQuery))]
    [ProtoBuf.ProtoContract]
    abstract public class Query : IQuery
    {
        public string Result
        {
            get { return ResultString; }
            set { ResultString = value; }
        }
        public abstract string ResultString { get; set; }

        protected static string FormatQueryString<T>(T value)
        {
            return TypeDescriptor.GetConverter(typeof(T))
                .ConvertToInvariantString(value);
        }
        protected static T ParseQueryString<T>(string value)
        {
            return (T) TypeDescriptor.GetConverter(typeof(T))
                .ConvertFromInvariantString(value);
        }
    }
    [ProtoBuf.ProtoContract]
    [ProtoBuf.ProtoInclude(21, typeof(Z))]
    abstract public class SpecialQuery : Query, IQuery<DataSet>
    {
        
        public new DataSet Result { get; set; }

        [ProtoBuf.ProtoMember(1)]
        public override string ResultString
        {
            get {
                if (Result == null) return null;
                using (StringWriter sw = new StringWriter())
                {
                    Result.WriteXml(sw, XmlWriteMode.WriteSchema);
                    return sw.ToString();
                }
            }
            set {
                if (value == null) { Result = null; return; }
                using (StringReader sr = new StringReader(value))
                {
                    DataSet ds = new DataSet();
                    ds.ReadXml(sr, XmlReadMode.ReadSchema);
                }
            }
        }
    }

    [ProtoBuf.ProtoContract]
    public class W : Query, IQuery<bool>
    {
        [ProtoBuf.ProtoMember(1)]
        public new bool Result { get; set; }

        public override string ResultString
        {
            get {return FormatQueryString(Result); }
            set { Result = ParseQueryString<bool>(value); }
        }
    }
    [ProtoBuf.ProtoContract]
    public class X : Query, IQuery<string>
    {
        [ProtoBuf.ProtoMember(1)]
        public new string Result { get; set; }

        public override string ResultString
        {
            get { return Result ; }
            set { Result = value; }
        }
    }
    [ProtoBuf.ProtoContract]
    public class Y : Query, IQuery<int>
    {
        [ProtoBuf.ProtoMember(1)]
        public new int Result { get; set; }

        public override string ResultString
        {
            get { return FormatQueryString(Result); }
            set { Result = ParseQueryString<int>(value); }
        }
    }
    [ProtoBuf.ProtoContract]
    public class Z : SpecialQuery
    {
    }
}
