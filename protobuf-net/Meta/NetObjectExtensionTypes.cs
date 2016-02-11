using System;

namespace AqlaSerializer.Meta
{
    [Flags]
    public enum NetObjectExtensionTypes
    {
        None = 0,
        Null = 1,
        Reference = 2,
        /// <summary>
        /// This is required to allow versioning when switching between class, struct and nullable struct
        /// </summary>
        AdvancedVersioning = 4,
        Collection = 8,
        /// <summary>
        /// For this feature main object should be written in field 1 as a group
        /// </summary>
        LateReference = 16,

        All = Null | Reference | AdvancedVersioning | Collection | LateReference
    }

    static class NetObjectExtensionTypesExtensions
    {
        public static bool HasFlag(this NetObjectExtensionTypes value, NetObjectExtensionTypes flag)
        {
            return (value & flag) != 0;
        }
    }
}