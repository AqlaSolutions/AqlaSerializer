namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Settings of a Protocol Buffers compatibility mode
    /// </summary>
    public class ProtoCompatibilitySettings
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
                _allowExtensionDefinitions = value;
                if (value != 0) EnableCompatibility = true;
            }
        }
        
        public static ProtoCompatibilitySettings Default { get; } = new ProtoCompatibilitySettings();

        public static ProtoCompatibilitySettings None { get; } = new ProtoCompatibilitySettings()
        {
            EnableCompatibility = false
        };

        public static ProtoCompatibilitySettings Full { get; } = new ProtoCompatibilitySettings()
        {
            EnableCompatibility = true,
            AllowExtensionDefinitions = NetObjectExtensionTypes.None
        };
    }
}