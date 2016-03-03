#if !NO_RUNTIME
using System.Text;

namespace AqlaSerializer.Serializers
{
    class IndentedWithBracketsStringBuilder : IndentedStringBuilder
    {
        readonly string _openingBracket;
        readonly string _closingBracket;

        public IndentedWithBracketsStringBuilder(StringBuilder sb, string openingBracket = "{", string closingBracket = "}")
            : base(sb)
        {
            _openingBracket = openingBracket;
            _closingBracket = closingBracket;
        }

        protected override void WriteStartIndent()
        {
            AppendLineOnNextEmpty("{");
        }

        protected override void WriteEndIndent()
        {
            AppendLineOnNextEmpty("}");
        }
    }
}
#endif