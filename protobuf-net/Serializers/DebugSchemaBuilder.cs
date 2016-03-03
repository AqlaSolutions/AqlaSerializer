// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer.Meta;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    interface IDebugSchemaBuilder
    {
        void SingleValueSerializer(IProtoSerializer thisSerializer, string description = "");
        IDisposable SingleTailDecorator(IProtoSerializer thisSerializer, string description = "");
        IDisposable GroupSerializer(IProtoSerializer thisSerializer, string description = "");
        IDisposable Field(int number, string name = "");

        /// <summary>
        /// Returns null if already added
        /// </summary>
        IDebugSchemaBuilder Contract(object id);
    }

    class DebugSchemaBuilder : IDebugSchemaBuilder
    {
        class LevelState
        {
            public bool AnyElementWritten;
            public bool IsGroup;
            public int Nested;
        }

        readonly Stack<LevelState> _nesting = new Stack<LevelState>(new[] { MakeRootState() });
        readonly Dictionary<object, IDebugSchemaBuilder> _contracts = new Dictionary<object, IDebugSchemaBuilder>();

        readonly IndentedStringBuilder _sb = new IndentedWithBracketsStringBuilder(new StringBuilder());

        static LevelState MakeRootState()
        {
            return new LevelState() { IsGroup = true};
        }

        LevelState State => _nesting.Peek();
        
        public void Reset()
        {
            _sb.Reset();
            _nesting.Clear();
            _nesting.Push(MakeRootState());
            _contracts.Clear();
        }

        public void SingleValueSerializer(IProtoSerializer thisSerializer, string description = "")
        {
            using (Item())
                AppendSerializer(thisSerializer, description);
        }

        public IDisposable SingleTailDecorator(IProtoSerializer thisSerializer, string description = "")
        {
            var item = Item();
            AppendSerializer(thisSerializer, description);
            return item;
        }

        public IDisposable GroupSerializer(IProtoSerializer thisSerializer, string description = "")
        {
            var item = Item();
            AppendSerializer(thisSerializer, description);
            return Combine(item, IndentedContent());
        }

        void AppendSerializer(IProtoSerializer serializer, string description = "")
        {
            string name = serializer.GetType().Name;
            if (name.EndsWith("Serializer")) name = name.Remove(name.Length - "Serializer".Length);
            if (name.EndsWith("Decorator")) name = name.Remove(name.Length - "Decorator".Length);
            if (name.EndsWith("Value")) name = name.Remove(name.Length - "Value".Length);
            if ((name == "Property" || name == "Field") && !string.IsNullOrEmpty(description))
                name = serializer.ExpectedType.Name + "." + description;
            else
            {
                if (!string.Equals(name, serializer.ExpectedType.Name, StringComparison.OrdinalIgnoreCase))
                    name += " : " + serializer.ExpectedType.Name;
                if (!string.IsNullOrEmpty(description)) name += " = " + description;
            }
            _sb.AppendLine(name);
        }

        public IDisposable Field(int number, string name = "")
        {
            var item = Item();
            _sb.AppendLine("#" + number + (!string.IsNullOrEmpty(name) ? ": " + name + " " : ""));
            return item;
        }

        /// <summary>
        /// Returns null if already added
        /// </summary>
        public IDebugSchemaBuilder Contract(object id)
        {
            using (Item()) _sb.AppendLine("LinkTo [" + id + "]");
            if (_contracts.ContainsKey(id)) return null;
            var b = new DebugSchemaBuilder();
            _contracts.Add(id, b);
            return b;
        }
        
        IDisposable Item()
        {
            if (!State.IsGroup && State.AnyElementWritten) throw new InvalidOperationException("Can't write multiple elements when not in a group");

            if (State.AnyElementWritten)
                _sb.AppendLine(",");
            else if (!State.IsGroup)
                _sb.AppendOnNextEmpty("-> ");

            State.AnyElementWritten = true;
            var s = State;
            _nesting.Push(new LevelState() { Nested = _nesting.Count });

            return new DisposableAction(
                () =>
                    {
                        _nesting.Pop();
                        if (State != s) throw new InvalidOperationException("Expected another nested level");
                    });
        }

        IDisposable IndentedContent()
        {
            State.IsGroup = true;
            return _sb.IndentedContent();
        }

        public override string ToString()
        {
            if (_nesting.Count > 1) throw new InvalidOperationException("Can't generate debug schema because not all nested levels were closed");
            var sb = new StringBuilder();
            sb.AppendLine(_sb.ToString());
            foreach (var b in _contracts)
            {
                sb.AppendLine();
                sb.AppendLine(b.Key + ":");
                sb.AppendLine(b.Value.ToString());
            }
            return sb.ToString();
        }


        IDisposable Combine(IDisposable a, IDisposable b)
        {
            return new DisposableAction(
                () =>
                    {
                        // disposed in reverse order
                        b?.Dispose();
                        a?.Dispose();
                    });
        }
    }
}

#endif