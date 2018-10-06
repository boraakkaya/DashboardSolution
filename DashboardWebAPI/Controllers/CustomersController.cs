using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : Controller
    {

        private readonly IConfiguration _config;
        private IMemoryCache _cache;
        int recordsFiltered = 0;
        public CustomersController(IConfiguration configuration, IMemoryCache memoryCache)
        {
            _config = configuration;
            _cache = memoryCache;

        }

        // GET api/Customers
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
            string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

            //Get list items using spAccessToken
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items";
            var requestMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(requestMethod, requestUrl);
            HttpResponseMessage responseMessage = await client.SendAsync(request);

            SPRestAPIResponseValues values = new SPRestAPIResponseValues();
            values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();
                      
            return Json(values.value);
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [EnableCors("SiteCorsPolicy")]
        [HttpPost]
        public IActionResult Post([FromBody] DataRequest dataRequest)
        {
            //return values.Get("search[value]");
            return new JsonResult(ReturnCustomersSet(dataRequest).Result.Value);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        private async Task<JsonResult> ReturnCustomersSet(DataRequest dataRequest)
        {
            //we will need to retrieve all items count in the list so DataTable knows how many items we have on the set for displaying total page numbers
            //Two scenarios: If there is a search keyword or not. If there is no search keyword return total item count. Else return count depending on the search keyword

            int spRequestItemCount = 0;
            
            string searchQuery = dataRequest.search.value.ToLower();

            spRequestItemCount = await getAllItemsCount(searchQuery, dataRequest);
            

            int totalItemsCount = spRequestItemCount;
            int start = dataRequest.start;
            int length = dataRequest.length;


            List<List<string>> dataPaged = new List<List<string>>();
            if (dataRequest.search.value == "") // get all items according to sort and paging
            {
               dataPaged = await getAllItems(dataRequest);
                recordsFiltered = spRequestItemCount;
            }
            else
            {
                dataPaged = await getItemsBySearchQuery(searchQuery, dataRequest);
            }
           
            var jsonResult =  new JsonResult(new DataResponse()
            {
                draw = dataRequest.draw,
                recordsFiltered = recordsFiltered,
                //recordsFiltered = totalItemsCount,
                recordsTotal = totalItemsCount,
                data = dataPaged
            });

            return jsonResult;

        }
        private async Task<List<List<string>>> getItemsBySearchQuery(string searchQuery,DataRequest dataRequest)
        {
            string sortedColumnName = dataRequest.columns.Find(a => a.data == dataRequest.order[0].column).name;
            int maxItemsToGather = dataRequest.start + dataRequest.length;
            List<List<string>> customers = new List<List<string>>();
            List<Customer> allCustomers;
            if (!_cache.TryGetValue("customers", out allCustomers))
            {

                SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
                string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

                //Get list items using spAccessToken
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                int top = maxItemsToGather > 5000 ? 5000 : maxItemsToGather;
                //string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID lt 5000&$top=5000&$orderBy=" + sortedColumnName +" " + dataRequest.order[0].dir;


                string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID lt 5000&$top=5000";



                var requestMethod = new HttpMethod("GET");
                var request = new HttpRequestMessage(requestMethod, requestUrl);
                HttpResponseMessage responseMessage = await client.SendAsync(request);

                SPRestAPIResponseValues values = new SPRestAPIResponseValues();
                values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();

                //if (maxItemsToGather > 5000)
                //{
                //make second call
                HttpClient client2 = new HttpClient();
                client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
                client2.DefaultRequestHeaders.Add("Accept", "application/json");
                int top2 = maxItemsToGather - 5000;
                //string requestUrl2 = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID gt 5000 and ID lt 10000&$orderBy=" + sortedColumnName + " " + dataRequest.order[0].dir;
                string requestUrl2 = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID ge 5000 and ID lt 10000&$top=5000";
                var requestMethod2 = new HttpMethod("GET");
                var request2 = new HttpRequestMessage(requestMethod2, requestUrl2);
                HttpResponseMessage responseMessage2 = await client.SendAsync(request2);

                SPRestAPIResponseValues values2 = new SPRestAPIResponseValues();
                values2 = await responseMessage2.Content.ReadAsAsync<SPRestAPIResponseValues>();
                values.value.AddRange(values2.value);

                //}
                allCustomers = values.value;
                var cacheEntryOptions = new MemoryCacheEntryOptions()
            // Keep in cache for this time, reset time if accessed.
            .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                // Save data in cache.
                _cache.Set("customers", allCustomers, cacheEntryOptions);

            }
            if (dataRequest.order[0].dir == "asc")
            {
                allCustomers = allCustomers.OrderBy(x => x.GetType().GetProperty(sortedColumnName).GetValue(x, null)).ToList<Customer>();
            }
            else
            {
                allCustomers = allCustomers.OrderByDescending(x => x.GetType().GetProperty(sortedColumnName).GetValue(x, null)).ToList<Customer>();
            }

            allCustomers = allCustomers.FindAll(a => a.FirstName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.LastName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Status.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Position.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Birthday.ToShortDateString().Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            recordsFiltered = allCustomers.Count;
            List<Customer> dataPaged = allCustomers.Skip(dataRequest.start).Take(dataRequest.length).ToList<Customer>();


            IEnumerable<List<string>> data = from req in dataPaged select new List<string>() { req.ID.ToString(), req.FirstName, req.LastName, req.Position, req.Status, req.Birthday.ToShortDateString() };

            customers = data.ToList<List<string>>();


            return customers;
        }
        private async Task<List<List<string>>> getAllItems(DataRequest dataRequest)
        {
            string sortedColumnName = dataRequest.columns.Find(a => a.data == dataRequest.order[0].column).name;
            int maxItemsToGather = dataRequest.start + dataRequest.length;
            List<List<string>> customers = new List<List<string>>();
            List<Customer> allCustomers;
            if (!_cache.TryGetValue("customers", out allCustomers))
            {

                SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
                string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

                //Get list items using spAccessToken
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                int top = maxItemsToGather > 5000 ? 5000 : maxItemsToGather;
                //string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID lt 5000&$top=5000&$orderBy=" + sortedColumnName +" " + dataRequest.order[0].dir;


                string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID lt 5000&$top=5000";



                var requestMethod = new HttpMethod("GET");
                var request = new HttpRequestMessage(requestMethod, requestUrl);
                HttpResponseMessage responseMessage = await client.SendAsync(request);

                SPRestAPIResponseValues values = new SPRestAPIResponseValues();
                values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();

                //if (maxItemsToGather > 5000)
                //{
                //make second call
                HttpClient client2 = new HttpClient();
                client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
                client2.DefaultRequestHeaders.Add("Accept", "application/json");
                int top2 = maxItemsToGather - 5000;
                //string requestUrl2 = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID gt 5000 and ID lt 10000&$orderBy=" + sortedColumnName + " " + dataRequest.order[0].dir;
                string requestUrl2 = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$filter=ID ge 5000 and ID lt 10000&$top=5000";
                var requestMethod2 = new HttpMethod("GET");
                var request2 = new HttpRequestMessage(requestMethod2, requestUrl2);
                HttpResponseMessage responseMessage2 = await client.SendAsync(request2);

                SPRestAPIResponseValues values2 = new SPRestAPIResponseValues();
                values2 = await responseMessage2.Content.ReadAsAsync<SPRestAPIResponseValues>();
                values.value.AddRange(values2.value);

                //}
                allCustomers = values.value;
                var cacheEntryOptions = new MemoryCacheEntryOptions()
            // Keep in cache for this time, reset time if accessed.
            .SetSlidingExpiration(TimeSpan.FromMinutes(5));

                // Save data in cache.
                _cache.Set("customers", allCustomers, cacheEntryOptions);

            }
                if (dataRequest.order[0].dir == "asc")
                {
                    allCustomers = allCustomers.OrderBy(x => x.GetType().GetProperty(sortedColumnName).GetValue(x, null)).ToList<Customer>();
                }
                else
                {
                    allCustomers = allCustomers.OrderByDescending(x => x.GetType().GetProperty(sortedColumnName).GetValue(x, null)).ToList<Customer>();
                }
                List<Customer> dataPaged = allCustomers.Skip(dataRequest.start).Take(dataRequest.length).ToList<Customer>();


                IEnumerable<List<string>> data = from req in dataPaged select new List<string>() { req.ID.ToString(), req.FirstName, req.LastName, req.Position, req.Status, req.Birthday.ToShortDateString() };

                customers = data.ToList<List<string>>();

            
            return customers;
        }


        private async Task<List<List<string>>> getAllItemsSkipTokenTest(DataRequest dataRequest)
        {
            string sortedColumnName = dataRequest.columns.Find(a => a.data == dataRequest.order[0].column).name;
            List<List<string>> customers = new List<List<string>>();
            SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
            string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

            //Get list items using spAccessToken
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$skiptoken=Paged%3dTRUE%26p_ID%3d" + dataRequest.start.ToString() + "&$top=" + dataRequest.length.ToString()
                //+ "&$orderby=" + sortedColumnName + " " + dataRequest.order[0].dir
                + "";
            var requestMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(requestMethod, requestUrl);
            HttpResponseMessage responseMessage = await client.SendAsync(request);

            SPRestAPIResponseValues values = new SPRestAPIResponseValues();
            values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();
            IEnumerable<List<string>> data = from req in values.value select new List<string>() { req.ID.ToString(), req.FirstName, req.LastName, req.Position, req.Status, req.Birthday.ToShortDateString() };

            customers = data.ToList<List<string>>();
            return customers;
        }

        private async Task<int> getAllItemsCount(string searchQuery, DataRequest dataRequest)
        {
            int totalItemCount = 0;
            SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
            string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

            //Get list items using spAccessToken
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/ItemCount";
            if (searchQuery != "")
            {
                
                for (int i = 1; i < dataRequest.columns.Count-1; i++)
                {
                    //skip the first column and last column since it is Date type and ID
                    var filterQuery = "$filter=substringof('"+ searchQuery +"',"  + dataRequest.columns[i].name + ")";
                    requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?" + filterQuery + "&$top=5000";
                    var requestMethod = new HttpMethod("GET");
                    var request = new HttpRequestMessage(requestMethod, requestUrl);
                    HttpResponseMessage responseMessage = await client.SendAsync(request);

                    SPRestAPIResponseValues values = new SPRestAPIResponseValues();
                    values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();
                    totalItemCount += values.value.Count;
                }
                
            }
            else
            {
                var requestMethod = new HttpMethod("GET");
                var request = new HttpRequestMessage(requestMethod, requestUrl);
                HttpResponseMessage responseMessage = await client.SendAsync(request);
                ItemCountResult countResult = new ItemCountResult();
                countResult = await responseMessage.Content.ReadAsAsync<ItemCountResult>();
                totalItemCount = countResult.value;
            }
            
            return totalItemCount;
        }
    }
    public class ItemCountResult
    {
        public int value { get; set; }
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
