using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting;

namespace DashboardWebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private IHostingEnvironment _env;
        public ValuesController(IHostingEnvironment env)
        {
            _env = env;
        }

        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "value1", "value2" };
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
            return DoSomething(dataRequest);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }
        private IActionResult DoSomething(DataRequest dataRequest)
        {
            var jsonContent = getJSONContents();

            List<ERRequest> allRequests = JsonConvert.DeserializeObject<List<ERRequest>>(jsonContent);
            int totalItemsCount = allRequests.Count;            
            int start = dataRequest.start;
            int length = dataRequest.length;

            string sortedColumnName = dataRequest.columns.Find(a => a.data == dataRequest.order[0].column).name;

            if (dataRequest.order[0].dir == "asc")
            {
                allRequests = allRequests.OrderBy(x => x.GetType().GetProperty(sortedColumnName).GetValue(x, null)).ToList<ERRequest>();
            }
            else
            {
                allRequests = allRequests.OrderByDescending(x => x.GetType().GetProperty(sortedColumnName).GetValue(x,null)).ToList<ERRequest>();
            }
            
            if(dataRequest.search.value != "")
            {
                string searchQuery = dataRequest.search.value.ToLower();
                allRequests = allRequests.FindAll(a => a.Company.Contains(searchQuery,StringComparison.OrdinalIgnoreCase) || a.Product.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            IEnumerable<List<string>> data = from req in allRequests select new List<string>() { req.Company, req.Product, req.Created.ToShortDateString() };
            List<List<string>> dataPaged = data.Skip(start).Take(length).ToList<List<string>>();
            
            return new JsonResult(new DataResponse() { draw = dataRequest.draw, recordsFiltered = data.Count(), recordsTotal = totalItemsCount,
                data = dataPaged
            });   
                
            }
        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {

        }

        public string getJSONContents()
        {
           return System.IO.File.ReadAllText(System.IO.Path.Combine(_env.WebRootPath, "items.json"));
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

    public class Column {
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
    
    public class ERRequest
    {
        public string Company { get; set; }
        public string Product { get; set; }
        public DateTime Created { get; set; }

    }
    

}
