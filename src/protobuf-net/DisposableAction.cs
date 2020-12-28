#if !NO_RUNTIME
using System;
using AltLinq; using System.Linq;

namespace AqlaSerializer
{
    class DisposableAction : IDisposable
    {
        readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action?.Invoke();
        }
    }
}
#endif