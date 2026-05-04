using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LampLightLabs.JobSearch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ShowInfoController : ControllerBase
    {
        [HttpGet(Name = "ShowInfo")]
        public string Get()
        {
            return "Hey there... This is the info!";
        }
    }
}
