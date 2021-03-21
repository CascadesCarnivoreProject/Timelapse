using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Carnassial.Data.Spreadsheet
{
    internal class SharedStringIndex
    {
        private readonly Dictionary<string, int> sharedStrings;

        public bool HasChanges { get; private set; }

        public SharedStringIndex(SharedStringTablePart sharedStringPart, SpreadsheetReadWriteStatus status)
        {
            this.sharedStrings = new Dictionary<string, int>(StringComparer.Ordinal);
            using (Stream sharedStringStream = sharedStringPart.GetStream(FileMode.Open, FileAccess.Read))
            {
                if (sharedStringStream.Length < 1)
                {
                    // nothing to do
                    // XmlReader doesn't handle this case, raising "Root element is missing" from MoveToContent() even though it 
                    // has EOF false prior to the call.
                    return;
                }

                using XmlReader reader = XmlReader.Create(sharedStringStream);
                reader.MoveToContent();

                int sharedStringCount = 0;
                string sharedStringCountAsString = reader.GetAttribute(Constant.OpenXml.Attribute.CountAttribute);
                if (sharedStringCountAsString != null)
                {
                    sharedStringCount = Int32.Parse(sharedStringCountAsString, NumberStyles.None, Constant.InvariantCulture);
                }

                int uniqueSharedStringCount = 0;
                string uniqueSharedStringCountAsString = reader.GetAttribute(Constant.OpenXml.Attribute.UniqueCountAttribute);
                if (uniqueSharedStringCountAsString != null)
                {
                    uniqueSharedStringCount = Int32.Parse(uniqueSharedStringCountAsString, NumberStyles.None, Constant.InvariantCulture);
                }

                if (sharedStringCount != uniqueSharedStringCount)
                {
                    throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.SharedStringIndexDuplicateOrMissing));
                }

                status.BeginExcelLoad(sharedStringCount);

                while (reader.EOF == false)
                {
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        reader.Read();
                    }
                    else if (String.Equals(reader.LocalName, Constant.OpenXml.Element.SharedString, StringComparison.Ordinal))
                    {
                        if (reader.ReadToDescendant(Constant.OpenXml.Element.SharedStringText, Constant.OpenXml.Namespace) == false)
                        {
                            throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SharedStringIndexNoSharedString));
                        }
                        string value = reader.ReadElementContentAsString();
                        this.sharedStrings.Add(value, this.sharedStrings.Count);
                        reader.ReadEndElement();

                        if (status.ShouldUpdateProgress())
                        {
                            status.QueueProgressUpdate(this.sharedStrings.Count);
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
            }

            this.HasChanges = false;
        }

        public void AcceptChanges()
        {
            this.HasChanges = false;
        }

        public int GetOrAdd(string value)
        {
            if (this.sharedStrings.TryGetValue(value, out int index))
            {
                return index;
            }

            index = this.sharedStrings.Count;
            this.sharedStrings.Add(value, index);
            this.HasChanges = true;
            return index;
        }

        // code here is almost identical to SharedStringIndex..ctor()
        // Difference simply a List<> is built rather than a Dictionary<>.  Factoring of this method is debatable but, for now, 
        // it's placed in SharedStringIndex to keep simlar code close together.  If maintenance becomes an issue the Add() calls
        // of the two methods can be factored out to delegates.
        public static List<string> GetSharedStrings(SharedStringTablePart sharedStringPart, SpreadsheetReadWriteStatus status)
        {
            List<string> sharedStrings = new();
            if (sharedStringPart == null)
            {
                return sharedStrings;
            }

            using (Stream sharedStringStream = sharedStringPart.GetStream(FileMode.Open, FileAccess.Read))
            {
                using XmlReader reader = XmlReader.Create(sharedStringStream);
                reader.MoveToContent();
                string sharedStringCountAsString = reader.GetAttribute(Constant.OpenXml.Attribute.CountAttribute);
                if (sharedStringCountAsString == null)
                {
                    throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SpreadsheetReaderWriterNoStringTableCount));
                }
                int sharedStringCount = Int32.Parse(sharedStringCountAsString, NumberStyles.None, Constant.InvariantCulture);
                status.BeginExcelLoad(sharedStringCount);

                while (reader.EOF == false)
                {
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        reader.Read();
                    }
                    else if (String.Equals(reader.LocalName, Constant.OpenXml.Element.SharedString, StringComparison.Ordinal))
                    {
                        if (reader.ReadToDescendant(Constant.OpenXml.Element.SharedStringText, Constant.OpenXml.Namespace) == false)
                        {
                            throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SharedStringIndexNoSharedString));
                        }
                        sharedStrings.Add(reader.ReadElementContentAsString());
                        reader.ReadEndElement();

                        if (status.ShouldUpdateProgress())
                        {
                            status.QueueProgressUpdate(sharedStrings.Count);
                        }
                    }
                    else
                    {
                        reader.Read();
                    }
                }
            }
            return sharedStrings;
        }

        public SharedStringTable ToTable()
        {
            string[] sharedStrings = new string[this.sharedStrings.Count];
            foreach (KeyValuePair<string, int> sharedString in this.sharedStrings)
            {
                sharedStrings[sharedString.Value] = sharedString.Key;
            }

            SharedStringTable table = new()
            {
                Count = UInt32Value.FromUInt32((uint)this.sharedStrings.Count),
                UniqueCount = UInt32Value.FromUInt32((uint)this.sharedStrings.Count)
            };
            foreach (string sharedString in sharedStrings)
            {
                SharedStringItem sharedStringItem = new()
                {
                    Text = new Text()
                    {
                        Text = sharedString
                    }
                };
                table.AppendChild(sharedStringItem);
            }
            return table;
        }
    }
}
