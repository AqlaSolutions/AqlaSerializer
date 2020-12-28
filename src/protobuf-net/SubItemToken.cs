// Modified by Vladyslav Taranov for AqlaSerializer, 2016
namespace AqlaSerializer
{
    /// <summary>
    /// Used to hold particulars relating to nested objects. This is opaque to the caller - simply
    /// give back the token you are given at the end of an object.
    /// </summary>
    public struct SubItemToken
    {
        internal readonly int Value;
        internal SubItemToken(int value) {
            this.Value = value;
        }
    }
}
