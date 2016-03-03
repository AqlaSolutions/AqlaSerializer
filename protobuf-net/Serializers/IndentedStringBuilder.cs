#if !NO_RUNTIME
using System;
using System.Text;
using AqlaSerializer.Compiler;

namespace AqlaSerializer.Serializers
{
    class IndentedStringBuilder
    {
        string Spaces => new string(' ', Indent * 4);

        readonly StringBuilder _sb;
        bool _spacesWritten;
        protected int Indent;

        public IndentedStringBuilder(StringBuilder sb)
        {
            _sb = sb;
        }

        public void Reset()
        {
            _sb.Length = 0;
            Indent = 0;
            _spacesWritten = false;
        }

        void EnsureSpaceWritten()
        {
            if (_spacesWritten) return;
            _sb.Append(Spaces);
            _spacesWritten = true;
        }

        public void AppendLineOnNextEmpty(string s)
        {
            if (_spacesWritten) AppendLine("");
            AppendLine(s);
        }

        public void AppendOnNextEmpty(string s)
        {
            if (_spacesWritten) AppendLine("");
            Append(s);
        }

        public void AppendLine()
        {
            AppendLine(string.Empty);
        }

        public void AppendLine(string s)
        {
            EnsureSpaceWritten();
            _sb.AppendLine(s);
            _spacesWritten = false;
        }

        public void Append(string s)
        {
            EnsureSpaceWritten();
            _sb.Append(" " + s);
        }

        protected virtual void WriteStartIndent()
        {

        }

        protected virtual void WriteEndIndent()
        {

        }

        public IDisposable IndentedContent()
        {
            WriteStartIndent();
            Indent++;
            return new DisposableAction(
                () =>
                    {
                        Indent--;
                        WriteEndIndent();
                    });
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}

#endif