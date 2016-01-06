// Modified by Vladyslav Taranov for AqlaSerializer, 2014
namespace AqlaSerializer
{
    /// <summary>
    /// Used to hold particulars relating to nested objects. This is opaque to the caller - simply
    /// give back the token you are given at the end of an object.
    /// </summary>
    public struct SubItemToken
    {
        internal readonly long value;
        internal SubItemToken(long value) {
            this.value = value;
        }
    }
}
