using Microsoft.AspNetCore.Mvc;
using LampLightLabs.JobSearch.Api.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace LampLightLabs.JobSearch.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicationsController : ControllerBase
    {
        private readonly ICsvReaderService _csv;
        
        public ApplicationsController(ICsvReaderService csv)
        {
            _csv = csv;
        }

        //// GET: api/<ApplicationsController1>
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}


        [HttpGet("fromcsv")]
        public IActionResult GetFromCsv()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "applications.csv");
            if (!System.IO.File.Exists(filePath))
                return NotFound($"File not found: {filePath}");

            var rows = _csv.ReadCsv(filePath);
            return Ok(rows);
        }

        //// GET api/<ApplicationsController1>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        //// POST api/<ApplicationsController1>
        //[HttpPost]
        //public void Post([FromBody] string value)
        //{
        //}

        //// PUT api/<ApplicationsController1>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<ApplicationsController1>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
