using ProtoBuf.Internal;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Event arguments needed to perform type-formatting functions; this could be resolving a Type to a string suitable for serialization, or could
    /// be requesting a Type from a string. If no changes are made, a default implementation will be used (from the assembly-qualified names).
    /// </summary>
    public class TypeFormatEventArgs : EventArgs
    {
        private Type _type;
        private string _formattedName;
        private readonly bool _typeFixed;
        /// <summary>
        /// The type involved in this map; if this is initially null, a Type is expected to be provided for the string in FormattedName.
        /// </summary>
        public Type Type
        {
            get { return _type; }
            set
            {
                if(_type != value)
                {
                    if (_typeFixed) throw new InvalidOperationException("The type is fixed and cannot be changed");
                    _type = value;
                }
            }
        }
        /// <summary>
        /// The formatted-name involved in this map; if this is initially null, a formatted-name is expected from the type in Type.
        /// </summary>
        public string FormattedName
        {
            get { return _formattedName; }
            set
            {
                if (_formattedName != value)
                {
                    if (!_typeFixed) throw new InvalidOperationException("The formatted-name is fixed and cannot be changed");
                    _formattedName = value;
                }
            }
        }
        internal TypeFormatEventArgs(string formattedName)
        {
            if (string.IsNullOrEmpty(formattedName)) ThrowHelper.ThrowArgumentNullException(nameof(formattedName));
            this._formattedName = formattedName;
            // typeFixed = false; <== implicit
        }
        internal TypeFormatEventArgs(System.Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            this._type = type;
            _typeFixed = true;
        }

    }
    /// <summary>
    /// Delegate type used to perform type-formatting functions; the sender originates as the type-model.
    /// </summary>
    public delegate void TypeFormatEventHandler(object sender, TypeFormatEventArgs args);
}
