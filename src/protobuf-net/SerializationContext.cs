// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;

namespace AqlaSerializer
{
    /// <summary>
    /// Additional information about a serialization operation
    /// </summary>
    public sealed class SerializationContext
    {
        private bool _frozen;
        internal void Freeze() { _frozen = true;}
        private void ThrowIfFrozen() { if (_frozen) throw new InvalidOperationException("The serialization-context cannot be changed once it is in use"); }
        private object _context;
        /// <summary>
        /// Gets or sets a user-defined object containing additional information about this serialization/deserialization operation.
        /// </summary>
        public object Context
        {
            get { return _context; }
            set { if (_context != value) { ThrowIfFrozen(); _context = value; } }
        }

        static SerializationContext()
        {
            Default = new SerializationContext();
            Default.Freeze();
        }
        /// <summary>
        /// A default SerializationContext, with minimal information.
        /// </summary>
        internal static SerializationContext Default { get; }

        private System.Runtime.Serialization.StreamingContextStates state = System.Runtime.Serialization.StreamingContextStates.Persistence;
        /// <summary>
        /// Gets or sets the source or destination of the transmitted data.
        /// </summary>
        public System.Runtime.Serialization.StreamingContextStates State
        {
            get { return state; }
            set { if (state != value) { ThrowIfFrozen(); state = value; } }
        }

		/// <summary>
		/// Convert a SerializationContext to a StreamingContext
		/// </summary>
		public static implicit operator System.Runtime.Serialization.StreamingContext(SerializationContext ctx)
        {
            if (ctx == null) return new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.Persistence);
            return new System.Runtime.Serialization.StreamingContext(ctx.state, ctx.context);
        }
        /// <summary>
        /// Convert a StreamingContext to a SerializationContext
        /// </summary>
        public static implicit operator SerializationContext (System.Runtime.Serialization.StreamingContext ctx)
        {
            SerializationContext result = new SerializationContext();

            result.Context = ctx.Context;
            result.State = ctx.State;

			return result;
        }

#if PLAT_BINARYFORMATTER || (SILVERLIGHT && NET_4_0)

#if !(WINRT || PHONE7 || PHONE8)
        private System.Runtime.Serialization.StreamingContextStates _state = System.Runtime.Serialization.StreamingContextStates.Persistence;
        /// <summary>
        /// Gets or sets the source or destination of the transmitted data.
        /// </summary>
        public System.Runtime.Serialization.StreamingContextStates State
        {
            get { return _state; }
            set { if (_state != value) { ThrowIfFrozen(); _state = value; } }
        }
#endif
        /// <summary>
        /// Convert a SerializationContext to a StreamingContext
        /// </summary>
        public static implicit operator System.Runtime.Serialization.StreamingContext(SerializationContext ctx)
        {
#if WINRT || PHONE7 || PHONE8
            return new System.Runtime.Serialization.StreamingContext();
#else
            if (ctx == null) return new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.Persistence);
            return new System.Runtime.Serialization.StreamingContext(ctx._state, ctx._context);
#endif
        }
        /// <summary>
        /// Convert a StreamingContext to a SerializationContext
        /// </summary>
        public static implicit operator SerializationContext (System.Runtime.Serialization.StreamingContext ctx)
        {
            SerializationContext result = new SerializationContext();
#if !(WINRT || PHONE7 || PHONE8)
            result.Context = ctx.Context;
            result.State = ctx.State;
#endif
            return result;
        }
#endif
    }

}
