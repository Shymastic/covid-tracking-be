// Controllers/TestController.cs - Basic routing test
using Microsoft.AspNetCore.Mvc;
using Covid19DataAPI.Data;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("test")]
    public class TestController : ControllerBase
    {
        private readonly CovidDbContext _context;

        public TestController(CovidDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Get()
        {
            Console.WriteLine("=== TEST CONTROLLER HIT ===");
            return Ok(new
            {
                message = "Test controller working",
                timestamp = DateTime.Now,
                countries_count = _context.Countries.Count(),
                cases_count = _context.CovidCases.Count()
            });
        }

        [HttpGet("odata-test")]
        public IActionResult ODataTest()
        {
            Console.WriteLine("=== ODATA TEST ENDPOINT HIT ===");
            return Ok(new
            {
                message = "OData test endpoint working",
                countries = _context.Countries.Take(3).Select(c => new { c.Id, c.CountryName }).ToList(),
                cases = _context.CovidCases.Take(3).Select(c => new { c.Id, c.CountryId, c.Confirmed }).ToList()
            });
        }

        [HttpGet("odata/countries-debug")]
        public IActionResult ODataCountriesDebug()
        {
            Console.WriteLine("=== ODATA COUNTRIES DEBUG HIT ===");
            var countries = _context.Countries.ToList();
            return Ok(new
            {
                total = countries.Count,
                sample = countries.Take(5).ToList()
            });
        }

        [HttpGet("odata/covidcases-debug")]
        public IActionResult ODataCasesDebug()
        {
            Console.WriteLine("=== ODATA CASES DEBUG HIT ===");
            var cases = _context.CovidCases.ToList();
            return Ok(new
            {
                total = cases.Count,
                sample = cases.Take(5).ToList()
            });
        }
    }
}