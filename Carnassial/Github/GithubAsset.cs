using Newtonsoft.Json;
using System;

namespace Carnassial.Github
{
    internal class GithubAsset
    {
        [JsonProperty(PropertyName = "url")]
        public Uri? Url { get; set; }
        [JsonProperty(PropertyName = "browser_download_url")]
        public Uri? BrowserDownloadUrl { get; set; }
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string? Name { get; set; }
        [JsonProperty(PropertyName = "label")]
        public string? Label { get; set; }
        [JsonProperty(PropertyName = "state")]
        public string? State { get; set; }
        [JsonProperty(PropertyName = "content_type")]
        public string? ContentType { get; set; }
        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }
        [JsonProperty(PropertyName = "download_count")]
        public int DownloadCount { get; set; }
        [JsonProperty(PropertyName = "created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonProperty(PropertyName = "updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonProperty(PropertyName = "uploader")]
        public GithubAuthor? Uploader { get; set; }
    }
}
