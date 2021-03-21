using Carnassial.Database;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Carnassial.Data
{
    public class ImageSetRow : SQLiteRow, INotifyPropertyChanged
    {
        private FileSelection fileSelection;
        private string? initialFolderName;
        private long mostRecentFileID;
        private string? log;
        private ImageSetOptions options;
        private string? timeZone;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageSetRow()
        {
            this.fileSelection = FileSelection.All;
            this.initialFolderName = null;
            this.mostRecentFileID = Constant.Database.InvalidID;
            this.log = null;
            this.options = ImageSetOptions.None;
            this.timeZone = null;
        }

        public FileSelection FileSelection
        {
            get
            {
                return this.fileSelection;
            }
            set
            {
                if (this.fileSelection == value)
                {
                    return;
                }
                this.fileSelection = value;
                this.HasChanges |= true;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.FileSelection)));
            }
        }

        public string? InitialFolderName
        {
            get
            {
                return this.initialFolderName;
            }
            set
            {
                if (String.Equals(this.initialFolderName, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
                this.initialFolderName = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.InitialFolderName)));
            }
        }

        public long MostRecentFileID
        {
            get
            {
                return this.mostRecentFileID;
            }
            set
            {
                if (this.mostRecentFileID == value)
                {
                    return;
                }
                this.HasChanges |= true;
                this.mostRecentFileID = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.MostRecentFileID)));
            }
        }

        public string? Log
        {
            get
            {
                return this.log;
            }
            set
            {
                if (String.Equals(this.log, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
                this.log = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.MostRecentFileID)));
            }
        }

        public ImageSetOptions Options
        {
            get
            {
                return this.options;
            }
            set
            {
                if (this.options == value)
                {
                    return;
                }
                this.HasChanges |= true;
                this.options = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Options)));
            }
        }

        public string? TimeZone
        {
            get
            {
                return this.timeZone;
            }
            set
            {
                if (String.Equals(this.timeZone, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
                this.timeZone = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimeZone)));
            }
        }

        public TimeZoneInfo GetTimeZoneInfo()
        {
            Debug.Assert(this.TimeZone != null);
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}
