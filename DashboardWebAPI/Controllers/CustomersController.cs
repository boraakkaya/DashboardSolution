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
        int spRequestItemCount = 0;
        int allRecordsCount = 0;
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
            string searchQuery = dataRequest.search.value.ToLower();
            spRequestItemCount = await getAllItemsCount(dataRequest);
            int start = dataRequest.start;
            int length = dataRequest.length;
            List<List<string>> dataPaged = new List<List<string>>();
            dataPaged = await getAllItems(dataRequest);
            var jsonResult = new JsonResult(new DataResponse()
            {
                draw = dataRequest.draw,
                recordsFiltered = recordsFiltered,
                recordsTotal = allRecordsCount,
                data = dataPaged
            });

            return jsonResult;

        }
        private async Task<List<List<string>>> getAllItems(DataRequest dataRequest)
        {
            string sortedColumnName = dataRequest.columns.Find(a => a.data == dataRequest.order[0].column).name;
            List<List<string>> customers = new List<List<string>>();
            List<Customer> allCustomers = new List<Customer>();

            if (!_cache.TryGetValue("customers", out allCustomers))
            {

                List<string> endPointsToCall = new List<string>();
                int itemIDCeiling = 5000; //ID ceiling for each call

                int totalCallCount = Convert.ToInt32(Math.Ceiling((double)spRequestItemCount / (double)itemIDCeiling));
                for (int x = 0; x < totalCallCount; x++)
                {
                    endPointsToCall.Add($"https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/items?$select=ID,FirstName,LastName,Position,Status,Birthday&$filter=ID ge {x * itemIDCeiling} and ID lt {(x + 1) * itemIDCeiling}&$top={itemIDCeiling}");
                }

                HttpClient client = new HttpClient();
                SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
                string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                IEnumerable<Task<List<Customer>>> getItemsQuery = from url in endPointsToCall select getItemsbyEndPoint(url, client);

                Task<List<Customer>>[] customerTasks = getItemsQuery.ToArray();

                List<Customer>[] customerValuesArray = await Task.WhenAll(customerTasks);

                allCustomers = new List<Customer>();
                for (int y = 0; y < customerValuesArray.Length; y++)
                {
                    allCustomers.AddRange(customerValuesArray[y]);
                }
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Keep in cache for this time, reset time if accessed.
                .SetSlidingExpiration(TimeSpan.FromSeconds(30));

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
            allRecordsCount = allCustomers.Count;
            recordsFiltered = allCustomers.Count;
            if (dataRequest.search.value != "")
            {
                string searchQuery = dataRequest.search.value;
                allCustomers = allCustomers.FindAll(a => a.FirstName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.LastName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Status.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Position.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || a.Birthday.ToShortDateString().Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                recordsFiltered = allCustomers.Count;
            }
            List<Customer> dataPaged = allCustomers.Skip(dataRequest.start).Take(dataRequest.length).ToList<Customer>();
            IEnumerable<List<string>> data = from req in dataPaged select new List<string>() { req.ID.ToString(), req.FirstName, req.LastName, req.Position, req.Status, req.Birthday.ToShortDateString() };
            customers = data.ToList<List<string>>();
            return customers;
        }

        async Task<List<Customer>> getItemsbyEndPoint(string url, HttpClient client)
        {
            var requestMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(requestMethod, url);
            HttpResponseMessage responseMessage = await client.SendAsync(request);

            SPRestAPIResponseValues values = new SPRestAPIResponseValues();
            values = await responseMessage.Content.ReadAsAsync<SPRestAPIResponseValues>();
            return values.value;
        }

        private async Task<int> getAllItemsCount(DataRequest dataRequest)
        {
            int totalItemCount = 0;
            SPAccessTokenHelper tokenHelper = new SPAccessTokenHelper(_config);
            string spAccessToken = await tokenHelper.getSharePointAccessToken(_config.GetValue<string>("spSiteURL"));

            //Get list items using spAccessToken
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spAccessToken);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            string requestUrl = "https://boraakkaya.sharepoint.com/_api/web/lists/getbytitle('HugeList')/ItemCount";

            var requestMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(requestMethod, requestUrl);
            HttpResponseMessage responseMessage = await client.SendAsync(request);
            ItemCountResult countResult = new ItemCountResult();
            countResult = await responseMessage.Content.ReadAsAsync<ItemCountResult>();
            totalItemCount = countResult.value;


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
