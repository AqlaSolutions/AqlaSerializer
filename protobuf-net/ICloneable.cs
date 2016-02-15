#if PHONE8 || SILVERLIGHT || WINRT || PORTABLE
namespace System
{
    interface ICloneable
    {
        object Clone();
    }
}
#endif