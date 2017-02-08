﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Security.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RedditSharp.Things
{
    /// <summary>
    /// A Reddit Live thread.  https://www.reddit.com/dev/api/#section_live
    /// </summary>
    // https://github.com/reddit/reddit-plugin-liveupdate/blob/master/reddit_liveupdate/models.py#L19
    public class LiveUpdateEvent : CreatedThing
    {
        [Flags]
        public enum LiveUpdateEventPermission
        {
            None = 0,
            Update = 1,
            Manage = 2,
            Settings = 4,
            Edit = 8,
            Close = 16,
            All = Update | Manage | Settings | Edit | Close
        }

        public LiveUpdateEvent(Reddit reddit, JToken json) : base(reddit, json) {
            FullName = Name;
            Name = Name.Replace("LiveUpdateEvent_", "");
        }

        public class LiveUpdateEventUser
        {
            [JsonConverter(typeof(PermissionsConverter))]
            public LiveUpdateEventPermission Permissions { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }
        }

        private const string AcceptContributorInviteUrl = "/api/live/{0}/accept_contribtor_invite";
        private const string CloseThreadUrl = "/api/live/{0}/close_thread";
        private const string EditUrl = "/api/live/{0}/edit";
        private const string InviteContributorUrl = "/api/live/{0}/invite_contributor";
        private const string LeaveContributorUrl = "/api/live/{0}/leave_contributor";
        private const string RemoveContributorUrl = "/api/live/{0}/rm_contributor";
        private const string RevokeContributorInviteUrl = "/api/live/{0}/rm_contributor_invite";
        private const string SetContributorPermissionUrl = "/api/live/{0}/set_contributor_permissions";
        private const string UpdateUrl = "/api/live/{0}/update";
        private const string StrikeUpdateUrl = "/api/live/{0}/strike_update";
        private const string DeleteUpdateUrl = "/api/live/{0}/delete_update";
        private const string GetUrl = "/live/{0}";
        private const string ContributorsUrl = "/live/{0}/contributors.json";
        private const string DiscussionsUrl = "/live/{0}/discussions";
        private const string ReportUrl = "/api/live/{0}/report";

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("description_html")]
        public string DescriptionHtml { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("websocket_uri")]
        public Uri WebsocketUri { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("nsfw")]
        public bool NSFW { get; set; }

        [JsonProperty("viewer_count")]
        public int? ViewerCount { get; set; }

        [JsonProperty("viewer_count_fuzzed")]
        public bool ViewerCountFuzzed { get; set; }

        [JsonProperty("resources")]
        public string Resources { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        /// <summary>
        /// Accept an invite to be a live thread contributor.
        /// </summary>
        public Task AcceptContributorInviteAsync()
        {
            return SimpleActionAsync(String.Format(AcceptContributorInviteUrl, Name));
        }

        /// <summary>
        /// Close the live thread.
        /// </summary>
        public Task CloseAsync()
        {
            return SimpleActionAsync(CloseThreadUrl);
        }

        /// <summary>
        /// Delete an update
        /// </summary>
        /// <param name="update">Update to strike</param>
        public async Task<bool> DeleteUpdateAsync(LiveUpdate update)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(DeleteUpdateUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                id = update.FullName,
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

                return false;
        }

        /// <summary>
        /// Edit a live thread.  Set parameters to empty string to clear those fields.  Or null to ignore them on update.
        /// </summary>
        /// <param name="title">New Title.</param>
        /// <param name="description">New Description</param>
        /// <param name="resources">new Resources</param>
        /// <param name="nsfw">NSFW flag</param>
        public async Task<bool> EditAsync(string title, string description, string resources, bool? nsfw)
        {
            var expando = (IDictionary<string, object>)new ExpandoObject();

            if (title != null)
                expando.Add(new KeyValuePair<string, object>("title", title));

            if (description != null)
                expando.Add(new KeyValuePair<string, object>("description", description));

            if (resources != null)
                expando.Add(new KeyValuePair<string, object>("resources", resources));

            if (nsfw.HasValue)
                expando.Add(new KeyValuePair<string, object>("nsfw", nsfw.Value));

            var request = WebAgent.CreatePost(String.Format(EditUrl, Id));
            WebAgent.WritePostBody(request, expando);

            var response = await WebAgent.GetResponseAsync(request);

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadAsStringAsync();
            JToken json = JToken.Parse(result);
            if (json["json"].ToString().Contains("\"errors\": []"))
            {
                Title = title ?? "";
                Description = description ?? "";
                Resources = resources ?? "";

                if (nsfw.HasValue)
                    NSFW = nsfw.Value;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Get a list of contributors.
        /// </summary>
        /// <returns></returns>
        public async  Task<ICollection<LiveUpdateEvent.LiveUpdateEventUser>> GetContributorsAsync()
        {
            var result = new List<LiveUpdateEvent.LiveUpdateEventUser>();
            var request = WebAgent.CreateGet(String.Format(ContributorsUrl, Name));
            var json = await WebAgent.ExecuteRequestAsync(request);

            JToken users;
            if (json.Type == JTokenType.Array)
            {
                users = json[1]["data"]["children"];
            }
            else
            {
                users = json["data"]["children"];
            }

            foreach (var user in users)
            {
                result.Add(user.ToObject<LiveUpdateEventUser>());
            }

            return result;
        }

        /// <summary>
        /// Get a list of reddit submissions linking to this thread.
        /// </summary>
        /// <returns></returns>
        public Listing<Post> GetDiscussions()
        {
            return new Listing<Post>(Reddit, String.Format(DiscussionsUrl, Name));
        }

        /// <summary>
        /// Get a list of updates to this live event.
        /// </summary>
        /// <returns></returns>
        public Listing<LiveUpdate> GetThread()
        {
            return new Listing<LiveUpdate>(Reddit, string.Format(GetUrl, Name));
        }

        /// <summary>
        /// Get invited contributors.
        /// </summary>
        /// <returns></returns>
        public async Task<ICollection<LiveUpdateEventUser>> GetInvitedContributorsAsync()
        {
            var result = new List<LiveUpdateEventUser>();
            var request = WebAgent.CreateGet(String.Format(ContributorsUrl, Name));
            var json = await WebAgent.ExecuteRequestAsync(request);

            var users = json[1]["data"]["children"];

            foreach (var user in users)
            {
                result.Add((LiveUpdateEventUser)JsonConvert.DeserializeObject(user.ToString()));
            }

            return result;
        }

        /// <summary>
        /// Invite a contributor to the live thread.
        /// </summary>
        /// <param name="name">reddit username.</param>
        /// <param name="permissions">permissions.</param>
        public async Task<bool> InviteContributorAsync(string userName, LiveUpdateEventPermission permissions)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(InviteContributorUrl, Name));
            var perms = GetPermissionsString(permissions);

            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                name = userName,
                permissions = perms,
                type = "liveupdate_contributor_invite",
                uh = Reddit.User.Modhash,

            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Abdicate contributorship of a thread.
        /// </summary>
        public Task LeaveContributorAsync()
        {
            return SimpleActionAsync(LeaveContributorUrl);
        }

        /// <summary>
        /// Remove a contributor from the live thread.
        /// </summary>
        /// <param name="name">RedditUser</param>
        public async Task<bool> RemoveContributorAsync(RedditUser user)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");

            var request = WebAgent.CreatePost(String.Format(RemoveContributorUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                id = user.Kind + "_" + user.Id,
                uh = Reddit.User.Modhash
            });
            var response  = await WebAgent.GetResponseAsync(request);

            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Remove a contributor from the live thread.
        /// </summary>
        /// <param name="name">reddit username.</param>
        public async Task<bool> RemoveContributorAsync(string userName)
        {
            var user = await Reddit.GetUserAsync(userName);
            return await RemoveContributorAsync(userName);
        }

        /// <summary>
        /// Report the live thread.  (Spam by default).
        /// </summary>
        /// <param name="reason">one of the following : "spam" (default), "vote-manipulation", "personal-information", "sexualizing-minors", "site-breaking"</param>
        public async Task<bool> ReportAsync(string reason = "spam")
        {
            var allowed = new List<string>() {
                "spam" ,
                "vote-manipulation" ,
                "personal-information" ,
                "sexualizing-minors" ,
                "site-breaking"
            };

            if (!allowed.Contains(reason))
            {
                var message = "Invalid report type.  Valid types are : ";
                for (int i = 0; i < allowed.Count; i++)
                {
                    message += "'" + allowed[i] + "'";
                    if (i != allowed.Count - 1)
                        message += ", ";
                }
                throw new InvalidOperationException(message);
            }

            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(ReportUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                type = reason,
                uh = Reddit.User.Modhash
            });

            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Revoke an outstanding contributor invite.
        /// </summary>
        /// <param name="name">reddit username</param>
        public async Task<bool> RevokeContributorInviteAsync(RedditUser user)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");

            var request = WebAgent.CreatePost(String.Format(RevokeContributorInviteUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                id = user.Kind + "_" + user.Id,
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Revoke an outstanding contributor invite.
        /// </summary>
        /// <param name="name">reddit username</param>
        public async Task<bool> RevokeContributorInviteAsync(string userName)
        {
            var user = await Reddit.GetUserAsync(userName);
            return await RevokeContributorInviteAsync(user);
        }

        /// <summary>
        /// Set contributor permissions on the live thread.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="permissions">Reddit user</param>
        public async Task<bool> SetContributorPermissionsAsync(RedditUser user, LiveUpdateEventPermission permissions)
        {
            return await SetContributorPermissions(user.Name, permissions);
        }

        /// <summary>
        /// Set contributor permissions on the live thread.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="permissions">Permissions to set.</param>
        public async Task<bool> SetContributorPermissions(string userName, LiveUpdateEventPermission permissions)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(SetContributorPermissionUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                name = userName,
                type = "liveupdate_contributor",
                permissions = GetPermissionsString(permissions),
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Set permissions on a contributor who has been invited but has not accepted.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="permissions">Permissions to set.</param>
        public async Task<bool> SetInvitedContributorPermissionsAsync(RedditUser user, LiveUpdateEventPermission permissions)
        {
            return await SetInvitedContributorPermissionsAsync(user.Name, permissions);
        }

        /// <summary>
        /// Set permissions on a contributor who has been invited but has not accepted.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="permissions">Permissions to set.</param>
        public async Task<bool> SetInvitedContributorPermissionsAsync(string userName, LiveUpdateEventPermission permissions)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(SetContributorPermissionUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                name = userName,
                type = "liveupdate_contributor_invite",
                permissions = GetPermissionsString(permissions),
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Strike an update
        /// </summary>
        /// <param name="update">Update to strike</param>
        public async Task<bool> StrikeUpdateAsync(LiveUpdate update)
        {
            return await StrikeUpdateAsync(update.FullName);
        }

        /// <summary>
        /// Strike an update
        /// </summary>
        /// <param name="fullName">Full name of the update to strike</param>
        public async Task<bool> StrikeUpdateAsync(string fullName)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(StrikeUpdateUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                id = fullName,
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        /// <summary>
        /// Make an update to the live thread
        /// </summary>
        /// <param name="markdown">markdown of the update</param>
        public async Task<bool> UpdateAsync(string markdown)
        {
            if (Reddit.User == null)
                throw new AuthenticationException("No user logged in.");
            var request = WebAgent.CreatePost(String.Format(UpdateUrl, Name));
            WebAgent.WritePostBody(request, new
            {
                api_type = "json",
                body = markdown,
                uh = Reddit.User.Modhash
            });
            var response = await WebAgent.GetResponseAsync(request);
            if (response.IsSuccessStatusCode)
                return true;

            return false;
        }

        private string GetPermissionsString(LiveUpdateEventPermission input)
        {
            // settings of edit + close
            // -all,-close,+edit,-manage,+settings,-update


            if (input == LiveUpdateEventPermission.All)
                return "+all";

            if (input == LiveUpdateEventPermission.None)
                return "-all,-close,-edit,-manage,-settings,-update";

            var result = "-all,";

            if (input.HasFlag(LiveUpdateEventPermission.Close))
                result += "+close,";
            else
                result += "-close,";

            if (input.HasFlag(LiveUpdateEventPermission.Edit))
                result += "+edit,";
            else
                result += "-edit,";

            if (input.HasFlag(LiveUpdateEventPermission.Manage))
                result += "+manage,";
            else
                result += "-manage,";

            if (input.HasFlag(LiveUpdateEventPermission.Settings))
                result += "+settings,";
            else
                result += "-settings,";

            if (input.HasFlag(LiveUpdateEventPermission.Update))
                result += "+update,";
            else
                result += "-update,";

            if (result.EndsWith(","))
                result = result.Remove(result.Length - 1, 1);

            return result;
        }

        private class PermissionsConverter : JsonConverter
        {
            // https://github.com/reddit/reddit-plugin-liveupdate/blob/master/reddit_liveupdate/permissions.py
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var flags = (LiveUpdateEventPermission)value;
                writer.WriteStartArray();
                if (flags.HasFlag(LiveUpdateEventPermission.All))
                {
                    writer.WriteValue("all");
                    writer.WriteEndArray();
                    return;
                }

                if (flags.HasFlag(LiveUpdateEventPermission.None))
                {
                    writer.WriteValue("none");
                    writer.WriteEndArray();
                    return;
                }

                if (flags.HasFlag(LiveUpdateEventPermission.Edit))
                    writer.WriteValue("edit");

                if (flags.HasFlag(LiveUpdateEventPermission.Close))
                    writer.WriteValue("close");

                if (flags.HasFlag(LiveUpdateEventPermission.Manage))
                    writer.WriteValue("manage");

                if (flags.HasFlag(LiveUpdateEventPermission.Settings))
                    writer.WriteValue("settings");

                if (flags.HasFlag(LiveUpdateEventPermission.Update))
                    writer.WriteValue("update");

                writer.WriteEndArray();

            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var array = JArray.Load(reader).ToObject<string[]>();

                var result = LiveUpdateEventPermission.None;
                var exit = false;
                foreach (var item in array)
                {
                    switch (item)
                    {
                        case "all":
                            result = LiveUpdateEventPermission.All;
                            exit = true;
                            break;
                        case "none":
                            result = LiveUpdateEventPermission.None;
                            exit = true;
                            break;
                        case "update":
                            result = result | LiveUpdateEventPermission.Update;
                            break;
                        case "manage":
                            result = result | LiveUpdateEventPermission.Manage;
                            break;
                        case "edit":
                            result = result | LiveUpdateEventPermission.Edit;
                            break;
                        case "settings":
                            result = result | LiveUpdateEventPermission.Settings;
                            break;
                        case "close":
                            result = result | LiveUpdateEventPermission.Close;
                            break;
                    }
                    if (exit)
                        break;
                }
                return result;
            }

            public override bool CanConvert(Type objectType)
            {
                return true;
            }
        }
    }
}
