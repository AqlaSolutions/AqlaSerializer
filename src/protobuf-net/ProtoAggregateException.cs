// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AltLinq;
#if PLAT_BINARYFORMATTER
using System.Runtime.Serialization;
#endif
namespace AqlaSerializer
{
    /// <summary>
    /// Indicates an error during serialization/deserialization of a proto stream.
    /// </summary>
#if PLAT_BINARYFORMATTER
    [Serializable]
#endif
    public class ProtoAggregateException : ProtoException
    {
        public IEnumerable<Exception> InnerExceptions { get; set; }

        public ProtoAggregateException(ICollection<Exception> innerExceptions)
            : base(GetMessage(innerExceptions))
        {
            InnerExceptions = innerExceptions.ToArray();
        }

        static string GetMessage(ICollection<Exception> exceptions)
        {

            var main = new StringBuilder("One or multiple exceptions occurred: ");

            bool isFirst = true;

            foreach (Exception ex in exceptions)
            {
                if (ex == null) continue;

                if (!isFirst)
                    main.Append(", ");
                else
                    isFirst = false;

                main.Append(ex.GetType().Name + " (" + ex.Message + ")");
            }

            return main.ToString();
        }

        
#if PLAT_BINARYFORMATTER
        /// <summary>Creates a new ProtoAggregateException instance.</summary>
        protected ProtoAggregateException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif

        public override string ToString()
        {
            var main = new StringBuilder(base.ToString());

            main.AppendLine();
            main.AppendLine("Inner exceptions: ");

            foreach (Exception ex in InnerExceptions)
            {
                if (ex == null) continue;
                main.AppendLine(ex.ToString());
                main.AppendLine();
            }

            return main.ToString();
        }
    }
}
