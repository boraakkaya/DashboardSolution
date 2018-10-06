using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace WebAPI
{
    public class SPAccessTokenHelper
    {
        private readonly IConfiguration _config;
        static string clientID;
        static string clientSecret;
        static string tenantURL;
        static string tenantID;
        static string spPrinciple;
        static string spAuthUrl;
        public SPAccessTokenHelper(IConfiguration configuration)
        {
            _config = configuration;
            clientID = _config.GetValue<string>("clientID");
            clientSecret = _config.GetValue<string>("clientSecret");
            tenantURL = _config.GetValue<string>("tenantURL");
            tenantID = _config.GetValue<string>("tenantID");
            spPrinciple = _config.GetValue<string>("spPrinciple");
            spAuthUrl = _config.GetValue<string>("spAuthUrl");
        }
        public async Task<string> getSharePointAccessToken(string siteUrl)
        {
            HttpClient client = new HttpClient();
            KeyValuePair<string, string>[] body = new KeyValuePair<string, string>[]
            {
        new KeyValuePair<string, string>("grant_type", "client_credentials"),
        new KeyValuePair<string, string>("client_id", $"{clientID}@{tenantID}"),
        new KeyValuePair<string, string>("resource", $"{spPrinciple}/{siteUrl}@{tenantID}".Replace("https://", "")),
        new KeyValuePair<string, string>("client_secret", clientSecret)
            };
            var content = new FormUrlEncodedContent(body);
            var contentLength = content.ToString().Length;
            string token = "";
            using (HttpResponseMessage response = await client.PostAsync(spAuthUrl, content))
            {
                if (response.Content != null)
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    JObject data = JObject.Parse(responseString);
                    token = data.Value<string>("access_token");
                }
            }
            return token;
        }
    }
}
