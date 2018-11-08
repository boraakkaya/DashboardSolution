using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DashboardWebApp.Helpers
{
    public class dashboardhelper
    {
        private IMemoryCache _cache;
        
        public dashboardhelper(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }
        public async Task<JsonResult> getCustomers(DataRequest request)
        {

            string bearerToken = await getBearerToken();
            //hit the api with bearer token
            //api end point https://customersdashboardwebapi.azurewebsites.net/api/customers

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string requestUrl = "https://customersdashboardwebapi.azurewebsites.net/api/customers";
            HttpRequestMessage message = new HttpRequestMessage(new HttpMethod("POST"), requestUrl);
            message.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.SendAsync(message);
            //DataResponse jsonContent = JsonConvert.DeserializeObject<DataResponse>(response.Content.ReadAsStringAsync().Result);
            DataResponse jsonContent = await response.Content.ReadAsAsync<DataResponse>();
            return new JsonResult(jsonContent);

        }
        public async Task<string> getBearerToken()
        {
            string token = "";
            if (!_cache.TryGetValue("apiBearerToken", out token))
            {
                HttpClient httpClient = new HttpClient();
                AuthenticationContext authContext =
                    new AuthenticationContext("https://login.microsoftonline.com/cd1ed347-7cfb-48c9-981a-8ca2f80ba40f");
                
                ClientCredential clientCredential =
                    new ClientCredential("455331a7-80f9-4873-b44f-ace36c0d5d12", "NlkBY+SZjpgDyJ6VFKnk5Wy0PLUTnIhsAHje3UT1u58=");
                AuthenticationResult result =
                    await authContext.AcquireTokenAsync("455331a7-80f9-4873-b44f-ace36c0d5d12", clientCredential);
                token = result.AccessToken;

                //Will set the Cache TimeSpan According to JWT Expiration

                JwtSecurityTokenHandler jwtHandler = new JwtSecurityTokenHandler();
                bool readableToken = jwtHandler.CanReadToken(token);
                
                if(!readableToken)
                {
                    return "";
                }
                else
                {
                    JwtSecurityToken jwtToken = jwtHandler.ReadJwtToken(token);
                    foreach(Claim c in jwtToken.Claims)
                    {
                        if(c.Type == "exp")
                        {
                            string tokenExpiresString = c.Value;
                            DateTime tokenExpiresDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(tokenExpiresString));
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(tokenExpiresDateTime.AddMinutes(-1));
                            _cache.Set("apiBearerToken", token, cacheEntryOptions);
                        }
                    }
                }
            }

                return token;
        }
    }

    public class DataRequest
    {
        public int draw { get; set; }
        public List<Column> columns { get; set; }
        public int length { get; set; }
        public List<Order> order { get; set; }
        public Search search { get; set; }
        public int start { get; set; }
    }

    public class Column
    {
        public int data { get; set; }
        public string name { get; set; }
        public bool searchable { get; set; }
        public bool orderable { get; set; }
        public Search search { get; set; }

    }

    public class Order
    {
        public int column { get; set; }
        public string dir { get; set; }
    }

    public class Search
    {
        public string value { get; set; }
        public bool regex { get; set; }
    }

    public class DataResponse
    {
        public int draw { get; set; }
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public List<List<string>> data { get; set; }

    }
}
