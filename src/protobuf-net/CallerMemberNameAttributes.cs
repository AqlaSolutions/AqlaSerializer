using System.Runtime.CompilerServices;

#if !NET_4_5

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    class CallerMemberNameAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    class CallerFilePathAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    class CallerLineNumberAttribute : Attribute
    {
    }
}

#endif

namespace AqlaSerializer.Ignore
{
    class CallerMemberNameUsage
    {
        void Method([CallerMemberName] string name = null)
        {

        }
    }
}