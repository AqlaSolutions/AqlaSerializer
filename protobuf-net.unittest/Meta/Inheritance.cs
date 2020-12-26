﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.Meta
{
    
    [TestFixture]
    public class InheritanceTests
    {
        public class SomeBase { public int A { get; set; } }
        public class SomeDerived : SomeBase { public int B { get; set; } }
        public class AnotherDerived : SomeBase { public int C { get; set; } }
        public class NotInvolved { public int D { get; set; } }
        public class AlsoNotInvolved { public int E { get; set; } }

        static RuntimeTypeModel CreateModel() {
            var model = TypeModel.Create();
            model[typeof(NotInvolved)].Add(1, "D");
            model[typeof(SomeBase)]
                .Add(1, "A")
                .AddSubType(2, typeof(SomeDerived))
                .AddSubType(3, typeof(AnotherDerived));
            model[typeof(SomeDerived)].Add(1, "B");
            model[typeof(AnotherDerived)].Add(1, "C");
            model[typeof(AlsoNotInvolved)].Add(1, "E");
            return model;
        }

        [Test]
        public void CanCreateModel()
        {
            Assert.IsNotNull(CreateModel());
        }
        [Test]
        public void CanCompileModelInPlace()
        {
            CreateModel().CompileInPlace();
        }
        [Test]
        public void CanCompileModelFully()
        {
            CreateModel().Compile("InheritanceTests", "InheritanceTests.dll");
            PEVerify.Verify("InheritanceTests.dll", 0, false);
        }
        [Test]
        public void CheckKeys()
        {
            var model = CreateModel();
            Type someBase = typeof(SomeBase), someDerived = typeof(SomeDerived);
            Assert.AreEqual(model.GetKey(ref someBase), model.GetKey(ref someDerived), "Runtime");

            TypeModel compiled = model.Compile();
            Assert.AreEqual(compiled.GetKey(ref someBase), compiled.GetKey(ref someDerived), "Compiled");
        }
        [Test]
        public void GetBackTheRightType_SomeBase()
        {
            var model = CreateModel();
            Assert.IsInstanceOf(typeof(SomeBase), model.DeepClone(new SomeBase()), "Runtime");

            model.CompileInPlace();
            Assert.IsInstanceOf(typeof(SomeBase), model.DeepClone(new SomeBase()), "In-Place");

            var compiled = model.Compile();
            Assert.IsInstanceOf(typeof(SomeBase), compiled.DeepClone(new SomeBase()), "Compiled");
        }
        [Test]
        public void GetBackTheRightType_SomeDerived()
        {
            var model = CreateModel();
            Assert.IsInstanceOf(typeof(SomeDerived), model.DeepClone(new SomeDerived()), "Runtime");

            model.CompileInPlace();
            Assert.IsInstanceOf(typeof(SomeDerived), model.DeepClone(new SomeDerived()), "In-Place");

            var compiled = model.Compile();
            Assert.IsInstanceOf(typeof(SomeDerived), compiled.DeepClone(new SomeDerived()), "Compiled");
        }

        [Test]
        public void GetBackTheRightType_AnotherDerived()
        {
            var model = CreateModel();
            Assert.IsInstanceOf(typeof(AnotherDerived), model.DeepClone(new AnotherDerived()), "Runtime");

            model.CompileInPlace();
            Assert.IsInstanceOf(typeof(AnotherDerived), model.DeepClone(new AnotherDerived()), "In-Place");

            var compiled = model.Compile();
            Assert.IsInstanceOf(typeof(AnotherDerived), compiled.DeepClone(new AnotherDerived()), "Compiled");
        }

        [Test]
        public void GetBackTheRightType_NotInvolved()
        {
            var model = CreateModel();
            Assert.IsInstanceOf(typeof(NotInvolved), model.DeepClone(new NotInvolved()), "Runtime");

            model.CompileInPlace();
            Assert.IsInstanceOf(typeof(NotInvolved), model.DeepClone(new NotInvolved()), "In-Place");

            var compiled = model.Compile();
            Assert.IsInstanceOf(typeof(NotInvolved), compiled.DeepClone(new NotInvolved()), "Compiled");
        }

    }

}
