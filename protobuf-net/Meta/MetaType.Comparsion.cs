// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;

#endif
#endif

namespace AqlaSerializer.Meta
{
    partial class MetaType
    {
        internal sealed class Comparer
            : IComparer
#if !NO_GENERICS
                , System.Collections.Generic.IComparer<MetaType>
#endif
        {
            public static readonly Comparer Default = new Comparer();

            public int Compare(object x, object y)
            {
                return Compare(x as MetaType, y as MetaType);
            }

            public int Compare(MetaType x, MetaType y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

#if FX11
                return string.Compare(x.GetSchemaTypeName(), y.GetSchemaTypeName());
#else
                return string.Compare(x.GetSchemaTypeName(), y.GetSchemaTypeName(), StringComparison.Ordinal);
#endif
            }
        }
    }
}
#endif