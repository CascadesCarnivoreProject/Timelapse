using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;

namespace Carnassial
{
    public class LocalizedApplication : Application
    {
        #if DEBUG
        // hook to allow tests to set culture
        internal static bool UseCurrentCulture { get; set; }
        #endif

        private static readonly Dictionary<string, ResourceDictionary> ResourcesByCultureName;

        static LocalizedApplication()
        {
            #if DEBUG
            LocalizedApplication.UseCurrentCulture = false;
            #endif

            LocalizedApplication.ResourcesByCultureName = new Dictionary<string, ResourceDictionary>();
            CultureInfo keyboardCulture = NativeMethods.GetKeyboardCulture();
            if (LocalizedApplication.TryLoadCultureResources(keyboardCulture, out string cultureName, out ResourceDictionary? cultureDictionary))
            {
                LocalizedApplication.ResourcesByCultureName.Add(cultureName, cultureDictionary);
            }
        }

        public LocalizedApplication()
        {
            #if DEBUG
            if (LocalizedApplication.UseCurrentCulture == false)
            {
                string debugCultureName = CultureInfo.CurrentCulture.Name.StartsWith("es", StringComparison.Ordinal) ? "en-IN" : "es-CL";
                CultureInfo testCulture = CultureInfo.GetCultureInfo(debugCultureName);
                CultureInfo.CurrentCulture = testCulture;
                CultureInfo.CurrentUICulture = testCulture;
                CultureInfo.DefaultThreadCurrentCulture = testCulture;
                CultureInfo.DefaultThreadCurrentUICulture = testCulture;
            }
            #endif
        }

        public static TResource FindResource<TResource>(string key)
        {
            return (TResource)App.Current.FindResource(key);
        }

        public static TResource FindResource<TResource>(string key, CultureInfo culture)
        {
            if (LocalizedApplication.ResourcesByCultureName.TryGetValue(culture.Name, out ResourceDictionary? dictionary) == false)
            {
                LocalizedApplication.ResourcesByCultureName.TryGetValue(culture.TwoLetterISOLanguageName, out dictionary);
            }
            if (dictionary != null)
            {
                object resource = dictionary[key];
                if (resource != null)
                {
                    return (TResource)resource;
                }
            }

            return LocalizedApplication.FindResource<TResource>(key);
        }

        public static string FormatResource(string key, params object?[] args)
        {
            string format = LocalizedApplication.FindResource<string>(key);
            return String.Format(CultureInfo.CurrentCulture, format, args);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // culture specific resources can't be loaded in the constructor since
            // 1) MergedDictionaries gets cleared afterwards
            // 2) culture resources need to be merged last to take effect
            CultureInfo uiCulture = CultureInfo.CurrentUICulture;
            if (LocalizedApplication.TryLoadCultureResources(uiCulture, out string cultureName, out ResourceDictionary? cultureDictionary))
            {
                LocalizedApplication.ResourcesByCultureName.Add(cultureName, cultureDictionary);
                this.Resources.MergedDictionaries.Add(cultureDictionary);
            }
        }

        private static bool TryLoadCultureResources(CultureInfo culture, out string cultureName, [NotNullWhen(true)] out ResourceDictionary? cultureDictionary)
        {
            cultureName = culture.TwoLetterISOLanguageName;
            if (Constant.UserInterface.Localizations.Contains(cultureName) == false)
            {
                if (Constant.UserInterface.Localizations.Contains(culture.Name) == false)
                {
                    cultureDictionary = null;
                    return false;
                }

                // zh-Hans and other cultures identified by full name
                cultureName = culture.Name;
            }

            if (LocalizedApplication.ResourcesByCultureName.TryGetValue(cultureName, out cultureDictionary))
            {
                return false;
            }

            cultureDictionary = new ResourceDictionary()
            {
                Source = new Uri("pack://application:,,,/Carnassial;component/Properties/SharedResources." + cultureName + ".xaml", UriKind.RelativeOrAbsolute)
            };
            return true;
        }
    }
}
