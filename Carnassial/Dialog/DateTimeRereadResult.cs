using Carnassial.Images;
using System;

namespace Carnassial.Dialog
{
    public class DateTimeRereadResult : MetadataFieldResult
    {
        public static new readonly DateTimeRereadResult Default = new();

        private DateTimeRereadResult()
            : base(null, null)
        {
        }

        public DateTimeRereadResult(FileLoad fileLoad, DateTimeOffset originalDateTime)
            : base(fileLoad.FileName)
        {
            if (fileLoad.MetadataReadResult == MetadataReadResults.None)
            {
                this.Message = App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultFileDateTime);
            }
            else if (fileLoad.MetadataReadResult == MetadataReadResults.DateTimeInferredFromPrevious)
            {
                this.Message = App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultPreviousMetadataDateTime);
            }
            else
            {
                this.Message = App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultMetadataDateTime);
            }

            DateTimeOffset reloadedDateTimeOffset = fileLoad.File.DateTimeOffset;
            if (reloadedDateTimeOffset == originalDateTime)
            {
                this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultNoChange);
            }
            else
            {
                if (reloadedDateTimeOffset.Date == originalDateTime.Date)
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultSameDate);
                }
                else
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultDifferentDate);
                }

                if (reloadedDateTimeOffset.TimeOfDay == originalDateTime.TimeOfDay)
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultSameTime);
                }
                else
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultDifferentTime);
                }

                if (reloadedDateTimeOffset.Offset == originalDateTime.Offset)
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultSameUtcOffset);
                }
                else
                {
                    this.Message += App.FindResource<string>(Constant.ResourceKey.DateTimeRereadResultDifferentUtcOffset);
                }
            }
        }
    }
}
