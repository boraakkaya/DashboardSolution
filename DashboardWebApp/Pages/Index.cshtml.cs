using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DashboardWebApp.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace DashboardWebApp.Pages
{
    
    public class IndexModel : PageModel
    {
        private IMemoryCache _cache;
        public IndexModel(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }
        public void OnGet()
        {

        }
        public async Task<JsonResult> OnGetDashboardAsync()
        {
            dummyObj dummy = new dummyObj() { Test = "sadas" };
            return new JsonResult(dummy);
        }
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> OnPostDashboardAsync([FromBody] DataRequest dataRequest)
        {

            dashboardhelper helper = new dashboardhelper(_cache);
            return helper.getCustomers(dataRequest).Result;
        }
        
    }
    public class dummyObj
    {
        public string Test { get; set; }
    }
}
