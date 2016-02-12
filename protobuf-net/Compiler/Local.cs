// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if FEAT_COMPILER
using System;
using TriAxis.RunSharp;
using TriAxis.RunSharp;
#if FEAT_IKVM
using IKVM.Reflection.Emit;
using Type  = IKVM.Reflection.Type;
#else
using System.Reflection.Emit;

#endif

namespace AqlaSerializer.Compiler
{
    internal sealed class Local : IDisposable
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
            AsOperand = new ContextualOperand(new FakeOperand(this), ctx.RunSharpContext.TypeMapper);
        }

#if FEAT_IKVM
        internal Local(CompilerContext ctx, System.Type type, bool fromPool = true, bool zeroed = false)
            : this(ctx, ctx.MapType(type), fromPool, zeroed)
        {
        }
#endif

        internal Local(CompilerContext ctx, Type type, bool fromPool = true, bool zeroed = false)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (type == null) throw new ArgumentNullException(nameof(type));
            _ctx = ctx;
            if (fromPool)
                _value = ctx.GetFromPool(type, zeroed);
            _type = type;
            _fromPool = fromPool;
            AsOperand = new ContextualOperand(new FakeOperand(this), ctx.RunSharpContext.TypeMapper);
        }

        [Obsolete("Don't use == on Local", true)]
        public static bool operator ==(Local a, object b)
        {
            return false;
        }

        [Obsolete("Don't use != on local", true)]
        public static bool operator !=(Local a, object b)
        {
            return false;
        }

        [Obsolete("Don't use == on Local", true)]
        public static bool operator ==(object b, Local a)
        {
            return false;
        }

        [Obsolete("Don't use != on local", true)]
        public static bool operator !=(object b, Local a)
        {
            return false;
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
            return !other.IsNullRef() && ourVal == (object)(other._value);
        }

        public Local AsCopy()
        {
            if (!_fromPool) return this; // can re-use if context-free
            return new Local(_ctx, _value, _type);
        }

        public event EventHandler Disposing;

        public void Dispose()
        {
            Disposing?.Invoke(this, EventArgs.Empty);
            Disposing = null;
            if (_fromPool)
            {
                // only *actually* dispose if this is context-bound; note that non-bound
                // objects are cheekily re-used, and *must* be left intact agter a "using" etc
                _ctx.ReleaseToPool(_value);
                _value = null;
                _ctx = null;
            }

        }

        public static implicit operator Operand(Local local)
        {
            if (local.IsNullRef()) throw new InvalidCastException("Local is null, use " + nameof(StackValueOperand) + " with type specified");
            return local.AsOperand;
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
                if (_local._ctx == null) throw new ObjectDisposedException("Local");
                _local._ctx.LoadValue(_local);
            }

            protected override void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
            {
                LeakedState = false;
                if (_local._ctx == null) throw new ObjectDisposedException("Local");
                EmitGetHelper(g, value, _local.Type, allowExplicitConversion);
                _local._ctx.StoreValue(_local);
            }

            protected override void EmitAddressOf(CodeGen g)
            {
                LeakedState = false;
                if (_local._ctx == null) throw new ObjectDisposedException("Local");
                _local._ctx.LoadRefArg(_local, _local._type);
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