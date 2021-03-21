using Carnassial.Database;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows;

namespace Carnassial.Util
{
    [SupportedOSPlatform(Constant.Platform.Windows)]
    public static class RegistryKeyExtensions
    {
        public static bool ReadBoolean(this RegistryKey registryKey, string subKeyPath, bool defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                if (Boolean.TryParse(valueAsString, out bool value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static byte ReadByte(this RegistryKey registryKey, string subKeyPath, byte defaultValue)
        {
            object value = registryKey.GetValue(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            // smallest size for integer registry values is a DWORD, so bytes are written as such and need to be read as such
            if (value is Int32 valueAsInt32)
            {
                return (byte)valueAsInt32;
            }

            if (value is string valueAsString)
            {
                return Byte.Parse(valueAsString, CultureInfo.InvariantCulture);
            }

            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Registry key {0}\\{1} has unhandled type {2}.", registryKey.Name, subKeyPath, value.GetType().FullName));
        }

        public static DateTime ReadDateTime(this RegistryKey registryKey, string subKeyPath, DateTime defaultValue)
        {
            string value = registryKey.ReadString(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            return DateTimeHandler.ParseDatabaseDateTime(value);
        }

        public static double ReadDouble(this RegistryKey registryKey, string subKeyPath, double defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                if (Double.TryParse(valueAsString, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Constant.InvariantCulture, out double value))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        public static LogicalOperator ReadLogicalOperator(this RegistryKey registryKey, string subKeyPath, LogicalOperator defaultValue)
        {
            string valueAsString = registryKey.ReadString(subKeyPath);
            if (valueAsString != null)
            {
                if (String.Equals("And", valueAsString, StringComparison.Ordinal))
                {
                    return LogicalOperator.And;
                }
                if (String.Equals("Or", valueAsString, StringComparison.Ordinal))
                {
                    return LogicalOperator.Or;
                }

                throw new ArgumentOutOfRangeException(nameof(valueAsString), String.Format(CultureInfo.CurrentCulture, "Unknown enum value '{0}'.", valueAsString));
            }

            return defaultValue;
        }

        public static int ReadInteger(this RegistryKey registryKey, string subKeyPath, int defaultValue)
        {
            object value = registryKey.GetValue(subKeyPath);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is Int32)
            {
                return (int)value;
            }

            if (value is string valueAsString)
            {
                return Int32.Parse(valueAsString, NumberStyles.AllowLeadingSign, Constant.InvariantCulture);
            }

            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Registry key {0}\\{1} has unhandled type {2}.", registryKey.Name, subKeyPath, value.GetType().FullName));
        }

        [SupportedOSPlatform("windows")]
        public static Rect ReadRect(this RegistryKey registryKey, string subKeyPath, Rect defaultValue)
        {
            string rectAsString = registryKey.ReadString(subKeyPath);
            if (rectAsString == null)
            {
                return defaultValue;
            }
            return Rect.Parse(rectAsString);
        }

        // read a REG_SZ key's value from the registry
        public static string ReadString(this RegistryKey registryKey, string subKeyPath)
        {
            return (string)registryKey.GetValue(subKeyPath);
        }

        // read a series of REG_SZ keys' values from the registry
        public static MostRecentlyUsedList<string> ReadMostRecentlyUsedList(this RegistryKey registryKey, string subKeyPath)
        {
            RegistryKey subKey = registryKey.OpenSubKey(subKeyPath);
            MostRecentlyUsedList<string> values = new(Constant.NumberOfMostRecentDatabasesToTrack);

            if (subKey != null)
            {
                for (int index = subKey.ValueCount - 1; index >= 0; --index)
                {
                    string listItem = (string)subKey.GetValue(index.ToString(Constant.InvariantCulture));
                    if (listItem != null)
                    {
                        values.SetMostRecent(listItem);
                    }
                }
            }

            return values;
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, bool value)
        {
            registryKey.Write(subKeyPath, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, byte value)
        {
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, DateTime value)
        {
            registryKey.Write(subKeyPath, value.ToString(Constant.Time.DateTimeDatabaseFormat, Constant.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, double value)
        {
            registryKey.Write(subKeyPath, value.ToString(Constant.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, MostRecentlyUsedList<string> values)
        {
            if (values != null)
            {
                // create the key whose values represent elements of the list
                RegistryKey subKey = registryKey.OpenSubKey(subKeyPath, true);
                if (subKey == null)
                {
                    subKey = registryKey.CreateSubKey(subKeyPath);
                }

                // write the values
                int index = 0;
                foreach (string value in values)
                {
                    subKey.SetValue(index.ToString(Constant.InvariantCulture), value);
                    ++index;
                }

                // remove any additional values when the new list is shorter than the old one
                int maximumValueName = subKey.ValueCount;
                for (; index < maximumValueName; ++index)
                {
                    subKey.DeleteValue(index.ToString(Constant.InvariantCulture));
                }
            }
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, int value)
        {
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.DWord);
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, Rect value)
        {
            registryKey.Write(subKeyPath, value.ToString(Constant.InvariantCulture));
        }

        public static void Write(this RegistryKey registryKey, string subKeyPath, string value)
        {
            registryKey.SetValue(subKeyPath, value, RegistryValueKind.String);
        }
    }
}
