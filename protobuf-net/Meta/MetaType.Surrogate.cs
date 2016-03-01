// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq;
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
        Type _surrogate;

        public bool HasSurrogate => _surrogate != null;

        /// <summary>
        /// Performs serialization of this type via a surrogate; all
        /// other serialization options are ignored and handled
        /// by the surrogate's configuration.
        /// </summary>
        public void SetSurrogate(Type surrogateType)
        {
            if (surrogateType == Type) surrogateType = null;
            if (surrogateType != null)
            {
                // note that BuildSerializer checks the **CURRENT TYPE** is OK to be surrogated
                if (surrogateType != null && Helpers.IsAssignableFrom(_model.MapType(typeof(IEnumerable)), surrogateType))
                {
                    throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be used as a surrogate");
                }
            }
            ThrowIfFrozen();
            this._surrogate = surrogateType;
            // no point in offering chaining; no options are respected
        }

        internal MetaType GetSurrogateOrSelf()
        {
            if (_surrogate != null) return _model[_surrogate];
            return this;
        }

        internal MetaType GetSurrogateOrBaseOrSelf(bool deep)
        {
            if (_surrogate != null) return _model[_surrogate];
            MetaType snapshot = this.BaseType;
            if (snapshot != null)
            {
                if (deep)
                {
                    MetaType tmp;
                    do
                    {
                        tmp = snapshot;
                        snapshot = snapshot.BaseType;
                    } while (snapshot != null);
                    return tmp;
                }
                return snapshot;
            }
            return this;
        }
    }
}
#endif