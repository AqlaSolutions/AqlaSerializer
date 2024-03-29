﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.Threading;
using AqlaSerializer.unittest.Meta;

namespace AqlaSerializer.unittest.Attribs
{
    [TestFixture]
    public class PointStructTests
    {

        public static RuntimeTypeModel BuildModelWithSurrogate()
        {
            RuntimeTypeModel model = TypeModel.Create();
            model.Add(typeof(PointSurrogate), true);
            model.Add(typeof(Point), false).SetSurrogate(typeof(PointSurrogate));
            return model;
        }

        [ProtoBuf.ProtoContract]
        public struct PointSurrogate {
            private static int toPoint, fromPoint;
            public static int ToPoint { get { return toPoint; } }
            public static int FromPoint { get { return fromPoint; } }
            public PointSurrogate(int x, int y) {
                this.X = x;
                this.Y = y;
            }
            [ProtoBuf.ProtoMember(1)] public int X;
            [ProtoBuf.ProtoMember(2)] public int Y;

            public static explicit operator PointSurrogate (Point value) {
                Interlocked.Increment(ref fromPoint);
                return new PointSurrogate(value.X, value.Y);
            }
            public static implicit operator Point(PointSurrogate value) {
                Interlocked.Increment(ref toPoint);
                return new Point(value.X, value.Y);
            }
        }

        [ProtoBuf.ProtoContract]
        public struct Point
        {
            [ProtoBuf.ProtoMember(1)] private readonly int x;
            [ProtoBuf.ProtoMember(2)] private readonly int y;
            public int X { get { return x; } }
            public int Y { get { return y; } }
            public Point(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        static RuntimeTypeModel BuildModel()
        {
            var model = TypeModel.Create();
            model.Add(typeof(Point), true);
            return model;
        }
        [Test]
        public void RoundTripPoint()
        {
            Point point = new Point(26, 13);
            var model = BuildModel();
            model.SkipCompiledVsNotCheck = true;
            ClonePoint(model, point, "Runtime");

            model.CompileInPlace();
            ClonePoint(model, point, "CompileInPlace");
        }
#if FAKE_COMPILE
        [Ignore]
#endif
        [Test]
        public void FullyCompileWithPrivateField_KnownToFail()
        {
            var model = BuildModel();
            Point point = new Point(26, 13);
            var ex = Assert.Throws<ProtoAggregateException>(() => ClonePoint(model.Compile(), point, "Compile"));
            Assert.AreEqual(ex.Message,
                @"One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: AqlaSerializer.unittest.Attribs.PointStructTests+Point.x)");
        }
        static void ClonePoint(TypeModel model, Point original, string message)
        {
            Point clone = (Point)model.DeepClone(original);
            Assert.AreEqual(original.X, clone.X, message + ": X");
            Assert.AreEqual(original.Y, clone.Y, message + ": Y");
        }

        static void ClonePointCountingConversions(TypeModel model, Point original, string message,
            int toPoint, int fromPoint)
        {
            int oldTo = PointSurrogate.ToPoint, oldFrom = PointSurrogate.FromPoint;
            Point clone = (Point)model.DeepClone(original);
            int newTo = PointSurrogate.ToPoint, newFrom = PointSurrogate.FromPoint;
            Assert.AreEqual(original.X, clone.X, message + ": X");
            Assert.AreEqual(original.Y, clone.Y, message + ": Y");
            Assert.AreEqual(toPoint, newTo - oldTo, message + ": Surrogate to Point");
            Assert.AreEqual(fromPoint, newFrom - oldFrom, message + ": Point to Surrogate");
        }

        [Test]
        public void VerifyPointWithSurrogate()
        {
            var model = BuildModelWithSurrogate();
            model.Compile("PointWithSurrogate", "PointWithSurrogate.dll");
            PEVerify.Verify("PointWithSurrogate.dll");
        }

#if FAKE_COMPILE
        [Ignore]
#endif
        [Test]
        public void VerifyPointDirect()
        {
            var model = BuildModel();
            var ex = Assert.Throws<ProtoAggregateException>(() => model.Compile("PointDirect", "PointDirect.dll"));
            Assert.AreEqual(ex.Message, @"One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: AqlaSerializer.unittest.Attribs.PointStructTests+Point.x)");
            
            //PEVerify.Verify("PointDirect.dll", 1); // expect failure due to field access
        }

        [Test]
        public void RoundTripPointWithSurrogate()
        {
            Point point = new Point(26, 13);
            var model = BuildModelWithSurrogate();
            model.SkipCompiledVsNotCheck = true; // this test uses static state 
            // two Point => Surrogate (one write, one read)
            // one Point <= Surrogate (one read)
            ClonePointCountingConversions(model, point, "Runtime", 1, 2);

            model.CompileInPlace();
            ClonePointCountingConversions(model, point, "CompileInPlace", 1, 2);

            ClonePointCountingConversions(model.Compile(), point, "CompileInPlace", 1, 2);
        }
    }

}
