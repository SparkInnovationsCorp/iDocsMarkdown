using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.NetworkInformation;

namespace iDocsMarkdown.Controllers
{
    public class _BaseController : Controller
    {
        protected readonly IConfiguration _configuration;

        public _BaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
    }
}
