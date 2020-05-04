﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Security.Authentication;
using System.Threading.Tasks;
using GitLabApiClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Octokit;
using SDSetupBackendRewrite.Data.Accounts;
using SDSetupCommon;
using SDSetupCommon.Data.Account;

namespace SDSetupBackendRewrite.Data {
    public class SDSetupUser {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        private string _bsonid { get; set; }

        public string SDSetupUserId { get; private set; } = Utilities.CreateGuid().ToCleanString();
        public SDSetupRole SDSetupRole { get; private set; } = SDSetupRole.Administrator;
        public string SessionToken { get; private set; }

        public string LinkedGithubId { get; private set; }
        public string GithubAccessToken { get; private set; }

        public string LinkedGitlabId { get; private set; }
        public string GitlabAccessToken { get; private set; }
        public string GitlabRefreshToken { get; private set; }

        [BsonIgnore]
        private GitHubClient GithubClient;
        [BsonIgnore]
        private GitLabClient GitlabClient;

        public LinkedService PrimaryService { get; private set; }

        public string CreateSessionToken() {
            SessionToken = Utilities.CreateCryptographicallySecureGuid().ToCleanString();
            Program.Users.UpdateUser(this);
            return SessionToken;
        }

        public bool ValidSessionToken(string token) {
            return token == SessionToken;
        }

        public async Task<bool> AuthenticateGithub(string code, string state) {
            GithubClient = new GitHubClient(new ProductHeaderValue("SDSetup-Manager"));
            try {
                OauthToken token = await GithubClient.Oauth.CreateAccessToken(new OauthTokenRequest(Program.ActiveConfig.GithubClientId, Program.ActiveConfig.GithubClientSecret, code));
                GithubAccessToken = token.AccessToken;
                Credentials credentials = new Credentials(GithubAccessToken);
                GithubClient.Credentials = credentials;
                if (String.IsNullOrWhiteSpace(this.LinkedGithubId)) this.LinkedGithubId = (await GetGithubProfile()).userId;
                await Program.Users.UpdateUser(this);
                return true;
            } catch {
                throw new AuthenticationException("Failed to authenticate with GitHub.");
            }
            
        }

        public async Task<bool> AuthenticateGitlab(string code, string state) {
            try {
                using (HttpClient client = new HttpClient()) {
                    Dictionary<string, string> parameters = new Dictionary<string, string>() {
                        { "client_id", Program.ActiveConfig.GitlabClientId },
                        { "client_secret", Program.ActiveConfig.GitlabClientSecret },
                        { "code", code },
                        { "grant_type", "authorization_code" },
                        { "redirect_uri", "http://files.sdsetup.com/api/v2/account/gitlablogincallback" }
                    };

                    HttpContent content = new FormUrlEncodedContent(parameters);
                    Console.WriteLine(await content.ReadAsStringAsync());
                    HttpResponseMessage response = await client.PostAsync("https://gitlab.com/oauth/token", content);
                    if (!response.IsSuccessStatusCode) return false;
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                    GitlabTokenResponse token = JsonConvert.DeserializeObject<GitlabTokenResponse>(await response.Content.ReadAsStringAsync());

                    GitlabAccessToken = token.access_token;
                    GitlabRefreshToken = token.refresh_token;

                    GitlabClient = new GitLabClient("https://gitlab.com/", GitlabAccessToken);

                    if (String.IsNullOrWhiteSpace(this.LinkedGitlabId)) 
                        this.LinkedGitlabId = (await GitlabClient.Users.GetCurrentSessionAsync()).Id.ToString();
                    
                    await Program.Users.UpdateUser(this);
                }
                return true;
            } catch {
                throw new AuthenticationException("Failed to authenticate with GitLab.");
            }

        }

        public async Task<bool> IsAuthenticatedWithGithub() {

            if (String.IsNullOrWhiteSpace(GithubAccessToken)) return false;
            try {
                if (GithubClient == null || GithubClient.Credentials ==  null || GithubClient.Credentials.GetToken() != GithubAccessToken) {
                    GithubClient = new GitHubClient(new ProductHeaderValue("SDSetup-Manager"));
                    Credentials tokenCredentials = new Credentials(GithubAccessToken);
                    GithubClient.Credentials = tokenCredentials;
                }
                //do it in this order in case the call to the user endpoint throws, meaning the access token is no longer valid.
                return ((await GithubClient.User.Current()).Id.ToString() == LinkedGithubId || String.IsNullOrWhiteSpace(LinkedGithubId));
            } catch {
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedWithGitlab() {

            if (String.IsNullOrWhiteSpace(GitlabAccessToken)) return false;
            try {
                if (GitlabClient == null) {
                    GitlabClient = new GitLabClient("https://gitlab.com", GitlabAccessToken);
                }
                return ((await GitlabClient.Users.GetCurrentSessionAsync()).Id.ToString() == LinkedGitlabId || String.IsNullOrWhiteSpace(LinkedGitlabId));
            } catch {
                return false;
            }
        }

        public void UpdateGithubAuthentication(SDSetupUser user) {
            this.GithubAccessToken = user.GithubAccessToken;
            this.GithubClient = user.GithubClient;
            Program.Users.UpdateUser(this);
        }

        public void UpdateGitlabAuthentication(SDSetupUser user) {
            this.GitlabAccessToken = user.GitlabAccessToken;
            this.GitlabRefreshToken = user.GitlabRefreshToken;
            this.GitlabClient = user.GitlabClient;
            Program.Users.UpdateUser(this);
        }

        public string GetSDSetupUserId() {
            return this.SDSetupUserId;
        }

        public string GetGithubUserId() {
            return LinkedGithubId;
        }

        public string GetGitlabUserId() {
            return LinkedGitlabId;
        }

        public void SetPrimaryService(LinkedService service) {
            this.PrimaryService = service;
            Program.Users.UpdateUser(this);
        }

        public async Task<GithubProfile> GetGithubProfile() {
            if (!(await IsAuthenticatedWithGithub())) return null;
            User user = await GithubClient.User.Current();
            return new GithubProfile(user.Id.ToString(), user.Name, user.Email, user.Bio, user.AvatarUrl, this.PrimaryService == LinkedService.GitHub);
        }
        public async Task<GitlabProfile> GetGitlabProfile() {
            if (!(await IsAuthenticatedWithGitlab())) return null;
            var user = await GitlabClient.Users.GetCurrentSessionAsync();
            return new GitlabProfile(user.Id.ToString(), user.Name, user.Email, user.Bio, user.AvatarUrl, this.PrimaryService == LinkedService.GitLab);
        }

        public async Task<SDSetupProfile> GetProfile() {
            SDSetupProfile profile = new SDSetupProfile() {
                userid = SDSetupUserId,
                userRole = SDSetupRole,
                hasLinkedGithub = !String.IsNullOrWhiteSpace(LinkedGithubId),
                hasLinkedGitlab = !String.IsNullOrWhiteSpace(LinkedGitlabId),
                githubProfile = await GetGithubProfile(),
                gitlabProfile = await GetGitlabProfile(),
                primaryService = this.PrimaryService
            };

            switch (profile.primaryService) {
                case LinkedService.GitHub:
                    profile.primaryProfile = profile.githubProfile;
                    break;
                case LinkedService.GitLab:
                    profile.primaryProfile = profile.gitlabProfile;
                    break;
            }

            return profile;
        }
    }
}
