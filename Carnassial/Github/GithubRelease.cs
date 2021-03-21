using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Carnassial.Github
{
    // see https://developer.github.com/v3/repos/releases/
    internal class GithubRelease
    {
        [JsonProperty(PropertyName = "url")]
        public Uri? Url { get; set; }
        [JsonProperty(PropertyName = "html_url")]
        public Uri? HtmlUrl { get; set; }
        [JsonProperty(PropertyName = "assets_url")]
        public Uri? AssetsUrl { get; set; }
        [JsonProperty(PropertyName = "upload_url")]
        public Uri? UploadUrl { get; set; }
        [JsonProperty(PropertyName = "tarball_url")]
        public Uri? TarballUrl { get; set; }
        [JsonProperty(PropertyName = "zipball_url")]
        public Uri? ZipBallUrl { get; set; }
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }
        [JsonProperty(PropertyName = "tag_name")]
        public string? TagName { get; set; }
        [JsonProperty(PropertyName = "target_commitish")]
        public string? TargetCommitish { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string? Name { get; set; }
        [JsonProperty(PropertyName = "body")]
        public string? Body { get; set; }
        [JsonProperty(PropertyName = "draft")]
        public bool Draft { get; set; }
        [JsonProperty(PropertyName = "prerelease")]
        public bool Prerelease { get; set; }
        [JsonProperty(PropertyName = "created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonProperty(PropertyName = "published_at")]
        public DateTime PublishedAt { get; set; }
        [JsonProperty(PropertyName = "author")]
        public GithubAuthor? Author { get; set; }
        [JsonProperty(PropertyName = "assets")]
        public List<GithubAsset>? Assets { get; set; }
    }
}