using System;
using System.Reflection.Emit;

namespace ProtoBuf.Compiler
{
    internal sealed class Local : IDisposable
    {
        // public static readonly Local InputValue = new Local(null, null);
        private LocalBuilder value;
        private readonly Type type;
        private CompilerContext ctx;

        private Local(LocalBuilder value, Type type)
        {
            this.value = value;
            this.type = type;
        }

        internal Local(CompilerContext ctx, Type type)
        {
            this.ctx = ctx;
            if (ctx is object) { value = ctx.GetFromPool(type); }
            this.type = type;
        }

        internal LocalBuilder Value => value ?? throw new ObjectDisposedException(GetType().Name);

        public Type Type => type;

        public Local AsCopy()
        {
            if (ctx is null) return this; // can re-use if context-free
            return new Local(value, this.type);
        }
        
        public void Dispose()
        {
            if (ctx is object)
            {
                // only *actually* dispose if this is context-bound; note that non-bound
                // objects are cheekily re-used, and *must* be left intact agter a "using" etc
                ctx.ReleaseToPool(value);
                value = null; 
                ctx = null;
            }            
        }

        internal bool IsSame(Local other)
        {
            if((object)this == (object)other) return true;

            object ourVal = value; // use prop to ensure obj-disposed etc
            return other is object && ourVal == (object)(other.value); 
        }
    }
}// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if FEAT_COMPILER
using System;
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

        Local(CompilerContext ctx, LocalBuilder value, Type type)
        {
            _value = value;
            Type = type;
            _ctx = ctx;
            _fromPool = false;
            AsOperand = new ContextualOperand(new CodeGen._Local(ctx.G, value), ctx.G.TypeMapper);
        }

#if FEAT_IKVM
        internal Local(CompilerContext ctx, System.Type type, bool fromPool = true, bool zeroed = false)
            : this(ctx, ctx.MapType(type), fromPool, zeroed)
        {
        }
#endif

        internal Local(CompilerContext ctx, Type type, bool fromPool = true, bool zeroed = false)
        {
#if DEBUG_COMPILE_2 && false
            zeroed = true;
#endif
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (type == null) throw new ArgumentNullException(nameof(type));
            _ctx = ctx;
            if (fromPool)
                _value = ctx.GetFromPool(type, zeroed);
            Type = type;
            _fromPool = fromPool;

            AsOperand = _value != null ? new ContextualOperand(new CodeGen._Local(ctx.G, _value), ctx.G.TypeMapper) : ctx.G.Arg(0);
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

        [Obsolete("Don't use + on local", true)]
        public static object operator +(string b, Local a)
        {
            return null;
        }

        [Obsolete("Don't use + on local", true)]
        public static object operator +(Local a, string b)
        {
            return null;
        }

        public Type Type { get; }

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
            return new Local(_ctx, _value, Type);
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
    }
}

#endif