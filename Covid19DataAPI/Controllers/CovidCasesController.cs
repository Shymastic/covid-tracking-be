using Microsoft.AspNetCore.Mvc;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CovidCasesController : ControllerBase
    {
        private readonly CovidDataImporter _importer;

        public CovidCasesController(CovidDataImporter importer)
        {
            _importer = importer;
        }

        [HttpGet]
        public ActionResult<List<object>> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var cases = CovidDataImporter.GetCovidCases(skip, Math.Min(take, 100));

            var result = cases.Select(c => new
            {
                c.Id,
                c.CountryId,
                c.ReportDate,
                c.Confirmed,
                c.Deaths,
                c.Recovered,
                c.Active,
                c.DailyConfirmed,
                c.DailyDeaths,
                Country = CovidDataImporter.GetCountry(c.CountryId)
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public ActionResult<object> Get(long id)
        {
            var covidCase = CovidDataImporter.GetCovidCase(id);
            if (covidCase == null) return NotFound();

            return Ok(new
            {
                covidCase.Id,
                covidCase.CountryId,
                covidCase.ReportDate,
                covidCase.Confirmed,
                covidCase.Deaths,
                covidCase.Recovered,
                covidCase.Active,
                Country = CovidDataImporter.GetCountry(covidCase.CountryId)
            });
        }

        [HttpGet("summary")]
        public ActionResult<object> GetSummary([FromQuery] DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today.AddDays(-1);
            var cases = CovidDataImporter.GetCovidCasesByDate(targetDate);

            if (!cases.Any())
            {
                return Ok(new
                {
                    message = "No data found",
                    date = targetDate,
                    totalCountries = CovidDataImporter.GetCountries().Count,
                    suggestion = "Try a different date or run POST /api/covidcases/import first"
                });
            }

            var totalConfirmed = cases.Sum(c => c.Confirmed);
            var totalDeaths = cases.Sum(c => c.Deaths);
            var totalRecovered = cases.Sum(c => c.Recovered);

            return Ok(new
            {
                ReportDate = targetDate,
                TotalConfirmed = totalConfirmed,
                TotalDeaths = totalDeaths,
                TotalRecovered = totalRecovered,
                TotalActive = cases.Sum(c => c.Active),
                CountriesReporting = cases.Count,
                MortalityRate = totalConfirmed > 0 ? (double)totalDeaths / totalConfirmed * 100 : 0
            });
        }

        [HttpGet("treemap/{date}")]
        public ActionResult<List<object>> GetTreemapData(DateTime date)
        {
            var cases = CovidDataImporter.GetCovidCasesByDate(date);
            if (!cases.Any()) return NotFound($"No data found for {date:yyyy-MM-dd}");

            var totalConfirmed = cases.Sum(c => c.Confirmed);

            var result = cases
                .Where(c => c.Confirmed > 0)
                .OrderByDescending(c => c.Confirmed)
                .Take(20)
                .Select(c => {
                    var country = CovidDataImporter.GetCountry(c.CountryId);
                    return new
                    {
                        CountryName = country?.CountryName ?? "Unknown",
                        CountryCode = country?.CountryCode ?? "UN",
                        Region = country?.Region ?? "Unknown",
                        c.Confirmed,
                        c.Deaths,
                        c.Recovered,
                        c.Active,
                        PercentOfGlobal = totalConfirmed > 0 ? (double)c.Confirmed / totalConfirmed * 100 : 0,
                        MortalityRate = c.Confirmed > 0 ? (double)c.Deaths / c.Confirmed * 100 : 0
                    };
                }).ToList();

            return Ok(result);
        }

        [HttpPost("import")]
        public async Task<ActionResult> ImportData()
        {
            var success = await _importer.ImportAllDataAsync();
            return Ok(new
            {
                Success = success,
                Message = success ? "Data imported successfully" : "Import failed - check logs",
                Countries = CovidDataImporter.GetCountries().Count,
                Cases = CovidDataImporter.GetCovidCases(0, int.MaxValue).Count,
                Timestamp = DateTime.Now
            });
        }


    }
}