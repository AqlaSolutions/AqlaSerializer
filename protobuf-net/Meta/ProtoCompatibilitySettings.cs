using System;

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Settings of a Protocol Buffers compatibility mode
    /// </summary>
    public class ProtoCompatibilitySettings:ICloneable
    {
        bool _enableCompatibility;

        /// <summary>
        /// Main switch; mind that compatibility modes are not compatible between themselves so changing this settings will make you unable to read your previously written data
        /// </summary>
        public bool EnableCompatibility
        {
            get { return _enableCompatibility; }
            set
            {
                _enableCompatibility = value;
                if (!value) AllowExtensionDefinitions = NetObjectExtensionTypes.All;
            }
        }

        NetObjectExtensionTypes _allowExtensionDefinitions = NetObjectExtensionTypes.All;

        /// <summary>
        /// You may disable some features by removing their types from this bitmask; mind that compatibility modes are not compatible between themselves so changing this settings will make you unable to read your previously written data
        /// </summary>
        public NetObjectExtensionTypes AllowExtensionDefinitions
        {
            get
            {
                return _allowExtensionDefinitions;
            }
            set
            {
                if (value.HasFlag(NetObjectExtensionTypes.LateReference) && !UseOwnFormat)
                    throw new ArgumentException("LateReference mode can't be enabled when UseOwnFormat is disabled");

                if (value.HasFlag(NetObjectExtensionTypes.AdvancedVersioning)
                    && ((value & (NetObjectExtensionTypes.Reference | NetObjectExtensionTypes.Null)) != (NetObjectExtensionTypes.Reference | NetObjectExtensionTypes.Null)))
                {
                    throw new ArgumentException("No need for " + nameof(NetObjectExtensionTypes.AdvancedVersioning) + " when reference or null handling is disabled");
                }
                _allowExtensionDefinitions = value;
                if (value != 0) EnableCompatibility = true;
            }
        }

        public bool UseOwnFormat { get; set; } = true;

        public static ProtoCompatibilitySettings Default => new ProtoCompatibilitySettings();

        public static ProtoCompatibilitySettings None => new ProtoCompatibilitySettings()
        {
            EnableCompatibility = false
        };

        public static ProtoCompatibilitySettings FullCompatibility => new ProtoCompatibilitySettings()
        {
            EnableCompatibility = true,
            AllowExtensionDefinitions = NetObjectExtensionTypes.None,
            UseOwnFormat = false
        };

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }

        public ProtoCompatibilitySettings Clone()
        {
            return (ProtoCompatibilitySettings)MemberwiseClone();
        }
    }
}