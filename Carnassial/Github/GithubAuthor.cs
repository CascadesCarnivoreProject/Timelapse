using Newtonsoft.Json;
using System;

namespace Carnassial.Github
{
    internal class GithubAuthor
    {
        [JsonProperty(PropertyName = "login")]
        public string? Login { get; set; }
        [JsonProperty(PropertyName = "id")]
        public int ID { get; set; }
        [JsonProperty(PropertyName = "avatar_url")]
        public Uri? AvatarUrl { get; set; }
        [JsonProperty(PropertyName = "gravatar_id")]
        public string? GravatarID { get; set; }
        [JsonProperty(PropertyName = "url")]
        public Uri? Url { get; set; }
        [JsonProperty(PropertyName = "html_url")]
        public Uri? HtmlUrl { get; set; }
        [JsonProperty(PropertyName = "followers_url")]
        public Uri? FollowersUrl { get; set; }
        [JsonProperty(PropertyName = "following_url")]
        public Uri? FollowingUrl { get; set; }
        [JsonProperty(PropertyName = "gists_url")]
        public Uri? GistsUrl { get; set; }
        [JsonProperty(PropertyName = "starred_url")]
        public Uri? StarredUrl { get; set; }
        [JsonProperty(PropertyName = "subscriptions_url")]
        public Uri? SubscriptionsUrl { get; set; }
        [JsonProperty(PropertyName = "organizations_url")]
        public Uri? OrganizationsUrl { get; set; }
        [JsonProperty(PropertyName = "repos_url")]
        public Uri? ReposUrl { get; set; }
        [JsonProperty(PropertyName = "events_url")]
        public Uri? EventsUrl { get; set; }
        [JsonProperty(PropertyName = "recieved_events_url")]
        public Uri? ReceivedEventsUrl { get; set; }
        [JsonProperty(PropertyName = "type")]
        public string? Type { get; set; }
        [JsonProperty(PropertyName = "site_admin")]
        public bool SiteAdmin { get; set; }
    }
}
