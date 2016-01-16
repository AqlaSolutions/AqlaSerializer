﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if FEAT_COMPILER
using System;
using TriAxis.RunSharp;
using TryAxis.RunSharp;
#if FEAT_IKVM
using IKVM.Reflection.Emit;
using Type  = IKVM.Reflection.Type;
#else
using System.Reflection.Emit;

#endif

namespace AqlaSerializer.Compiler
{
    internal sealed class Local: IDisposable
    {
        LocalBuilder _value;
        CompilerContext _ctx;

        readonly bool _fromPool;
        readonly Type _type;

        Local(CompilerContext ctx, LocalBuilder value, Type type)
        {
            _value = value;
            _type = type;
            _ctx = ctx;
            _fromPool = false;
        }

        internal Local(CompilerContext ctx, Type type, bool fromPool = true)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (type == null) throw new ArgumentNullException(nameof(type));
            _ctx = ctx;
            if (fromPool)
                _value = ctx.GetFromPool(type);
            _type = type;
            _fromPool = fromPool;
            AsOperand = new ContextualOperand(new FakeOperand(this), ctx.RunSharpContext.TypeMapper);
        }

        public Type Type => _type;

        internal LocalBuilder Value
        {
            get
            {
                if (_value == null)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }
                return _value;
            }
        }

        internal bool IsSame(Local other)
        {
            if ((object)this == (object)other) return true;

            object ourVal = _value; // use prop to ensure obj-disposed etc
            return other != null && ourVal == (object)(other._value);
        }

        public Local AsCopy()
        {
            if (!_fromPool) return this; // can re-use if context-free
            return new Local(_ctx, _value, _type);
        }

        public void Dispose()
        {
            if (_fromPool)
            {
                // only *actually* dispose if this is context-bound; note that non-bound
                // objects are cheekily re-used, and *must* be left intact agter a "using" etc
                _ctx.ReleaseToPool(_value);
                _value = null;
                _ctx = null;
            }

        }

        public ContextualOperand AsOperand { get; }

        class FakeOperand : Operand
        {
            readonly Local _local;

            public FakeOperand(Local local)
            {
                _local = local;
            }

            protected override bool DetectsLeaking => false;

            protected override void EmitGet(CodeGen g)
            {
                LeakedState = false;
                _local._ctx.LoadValue(_local);
            }

            protected override void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
            {
                LeakedState = false;
                _local._ctx.StoreValue(_local);
            }

            protected override void EmitAddressOf(CodeGen g)
            {
                LeakedState = false;
                _local._ctx.LoadAddress(_local, _local._type);
            }

            public override Type GetReturnType(ITypeMapper typeMapper)
            {
                return _local._type;
            }

            protected override bool TrivialAccess => true;
        }
    }
}

#endif