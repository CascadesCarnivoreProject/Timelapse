namespace Carnassial.Dialog
{
    public class MetadataFieldResult
    {
        public static readonly MetadataFieldResult Default = new(null, null);

        public string? FileName { get; protected set; }
        public string? Message { get; protected set; }

        protected MetadataFieldResult(string? fileName)
        {
            this.FileName = fileName;
        }

        public MetadataFieldResult(string? fileName, string? message)
            : this(fileName)
        {
            this.Message = message;
        }
    }
}
