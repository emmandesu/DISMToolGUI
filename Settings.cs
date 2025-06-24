using System.Configuration;

namespace DismToolGui
{
    internal sealed partial class Settings : ApplicationSettingsBase
    {
        private static readonly Settings defaultInstance = (Settings)Synchronized(new Settings());

        public static Settings Default => defaultInstance;

        [UserScopedSetting()]
        [DefaultSettingValue("False")]
        public bool LicenseAccepted
        {
            get => (bool)this["LicenseAccepted"];
            set => this["LicenseAccepted"] = value;
        }
    }
}
