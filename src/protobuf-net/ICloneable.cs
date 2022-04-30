#if PHONE8 || SILVERLIGHT || PORTABLE
namespace System
{
    interface ICloneable
    {
        object Clone();
    }
}
#endif