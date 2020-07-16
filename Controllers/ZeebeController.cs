using System.Threading.Tasks;
using Cloudstarter.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using fastJSON;

namespace Cloudstarter.Controllers
{
    public class ZeebeController : Controller
    {
        private readonly ILogger<ZeebeController> _logger;
        private readonly IZeebeService _zeebeService;

        public ZeebeController(ILogger<ZeebeController> logger, IZeebeService zeebeService)
        {
            _logger = logger;
            _zeebeService = zeebeService;
        }

        [Route("/status")]
        [HttpGet]
        public async Task<string> Get()
        {
            return (await _zeebeService.Status()).ToString();
        }
        
        [Route("/start")]
        [HttpGet]
        public async Task<string> StartWorkflowInstance()
        {
            var instance = await _zeebeService.StartWorkflowInstance("test-process");
            return instance;
        }
    }
}