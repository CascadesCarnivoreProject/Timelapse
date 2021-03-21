using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Carnassial.Util
{
    /// <summary>
    /// Base class for manipulating application's user preferences and related information in the registry.
    /// </summary>
    public class UserRegistrySettings
    {
        private readonly string keyPath;

        public UserRegistrySettings(string keyPath)
        {
            this.keyPath = keyPath;
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        protected RegistryKey OpenRegistryKey()
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(this.keyPath, true);
            if (registryKey == null)
            {
                registryKey = Registry.CurrentUser.CreateSubKey(this.keyPath);
            }
            return registryKey;
        }
    }
}
