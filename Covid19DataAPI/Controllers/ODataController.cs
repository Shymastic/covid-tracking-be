// Controllers/ODataController.cs - Replace complex OData with simple endpoints
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Data;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("odata")]
    public class ODataController : ControllerBase
    {
        private readonly CovidDbContext _context;
        
        public ODataController(CovidDbContext context)
        {
            _context = context;
        }

        // GET /odata/Countries
        [HttpGet("Countries")]
        [EnableQuery(PageSize = 200, MaxTop = 500)]
        public IActionResult GetCountries()
        {
            Console.WriteLine($"=== OData Countries called - {_context.Countries.Count()} records ===");
            return Ok(_context.Countries.AsQueryable());
        }

        // GET /odata/Countries/{id}
        [HttpGet("Countries/{id}")]
        public IActionResult GetCountry(int id)
        {
            Console.WriteLine($"=== OData Countries single: {id} ===");
            var country = _context.Countries.FirstOrDefault(c => c.Id == id);
            return country == null ? NotFound() : Ok(country);
        }

        // GET /odata/CovidCases
        [HttpGet("CovidCases")]
        [EnableQuery(PageSize = 50, MaxTop = 1000)]
        public IActionResult GetCovidCases()
        {
            Console.WriteLine($"=== OData CovidCases called - {_context.CovidCases.Count()} records ===");
            return Ok(_context.CovidCases.Include(c => c.Country).AsQueryable());
        }

        // GET /odata/CovidCases/{id}
        [HttpGet("CovidCases/{id}")]
        public IActionResult GetCovidCase(long id)
        {
            Console.WriteLine($"=== OData CovidCases single: {id} ===");
            var covidCase = _context.CovidCases
                .Include(c => c.Country)
                .FirstOrDefault(c => c.Id == id);
            return covidCase == null ? NotFound() : Ok(covidCase);
        }

        // Removed custom $metadata endpoint to avoid conflict with OData built-in metadata

        // Additional helper endpoints for frontend
        [HttpGet("Countries/ByRegion/{region}")]
        [EnableQuery]
        public IActionResult GetCountriesByRegion(string region)
        {
            Console.WriteLine($"=== OData Countries by region: {region} ===");
            var countries = _context.Countries
                .Where(c => c.Region != null && c.Region.ToLower() == region.ToLower())
                .AsQueryable();
            return Ok(countries);
        }

        [HttpGet("CovidCases/Latest")]
        [EnableQuery]
        public IActionResult GetLatestCovidCases()
        {
            Console.WriteLine("=== OData Latest CovidCases called ===");
            var latestDate = _context.CovidCases.Max(c => c.ReportDate);
            var cases = _context.CovidCases
                .Include(c => c.Country)
                .Where(c => c.ReportDate == latestDate)
                .AsQueryable();
            return Ok(cases);
        }

        [HttpGet("CovidCases/Summary/{date?}")]
        public IActionResult GetSummaryByDate(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today.AddDays(-1);
            Console.WriteLine($"=== OData Summary for date: {targetDate:yyyy-MM-dd} ===");
            
            var cases = _context.CovidCases
                .Include(c => c.Country)
                .Where(c => c.ReportDate.Date == targetDate.Date)
                .ToList();
                
            if (!cases.Any())
            {
                return Ok(new { message = "No data for date", date = targetDate });
            }
            
            var summary = new
            {
                Date = targetDate,
                TotalConfirmed = cases.Sum(c => c.Confirmed),
                TotalDeaths = cases.Sum(c => c.Deaths),
                TotalRecovered = cases.Sum(c => c.Recovered),
                TotalActive = cases.Sum(c => c.Active),
                CountriesReporting = cases.Count,
                TopCountries = cases
                    .OrderByDescending(c => c.Confirmed)
                    .Take(10)
                    .Select(c => new {
                        c.Country?.CountryName,
                        c.Confirmed,
                        c.Deaths,
                        c.Active
                    })
            };
            
            return Ok(summary);
        }
    }
}