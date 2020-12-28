// Modified by Vladyslav Taranov for AqlaSerializer, 2016

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
    class StackValueOperand : Operand
    {
        readonly Type _type;

        public StackValueOperand(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _type = type;
        }

        protected override bool DetectsLeaking => false;

        protected override void EmitGet(CodeGen g)
        {
            LeakedState = false;
        }

        protected override void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
        {
            LeakedState = false;
            var il = GetILGenerator(g);
            il.Emit(OpCodes.Pop);
            EmitGetHelper(g, value, _type, allowExplicitConversion);
        }

        protected override void EmitAddressOf(CodeGen g)
        {
            throw new NotSupportedException();
        }

        public override Type GetReturnType(ITypeMapper typeMapper)
        {
            return _type;
        }

        protected override bool TrivialAccess => true;
    }
}
#endif