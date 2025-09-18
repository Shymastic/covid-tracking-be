using Microsoft.AspNetCore.Mvc;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CovidCasesController : ControllerBase
    {
        private readonly CovidDataLoader _dataLoader;
        private readonly ILogger<CovidCasesController> _logger;

        public CovidCasesController(CovidDataLoader dataLoader, ILogger<CovidCasesController> logger)
        {
            _dataLoader = dataLoader;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<object>>> GetAll([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var cases = _dataLoader.GetCovidCases(skip, Math.Min(take, 100));

                // Map to include country info without circular reference
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
                    Country = new
                    {
                        _dataLoader.GetCountry(c.CountryId)?.Id,
                        _dataLoader.GetCountry(c.CountryId)?.CountryCode,
                        _dataLoader.GetCountry(c.CountryId)?.CountryName,
                        _dataLoader.GetCountry(c.CountryId)?.Region,
                        _dataLoader.GetCountry(c.CountryId)?.Population
                    }
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting COVID cases");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> Get(long id)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var covidCase = _dataLoader.GetCovidCase(id);

                if (covidCase == null) return NotFound();

                var country = _dataLoader.GetCountry(covidCase.CountryId);

                var result = new
                {
                    covidCase.Id,
                    covidCase.CountryId,
                    covidCase.ReportDate,
                    covidCase.Confirmed,
                    covidCase.Deaths,
                    covidCase.Recovered,
                    covidCase.Active,
                    covidCase.DailyConfirmed,
                    covidCase.DailyDeaths,
                    Country = country == null ? null : new
                    {
                        country.Id,
                        country.CountryCode,
                        country.CountryName,
                        country.Region,
                        country.Population
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting COVID case {Id}", id);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("country/{countryCode}")]
        public async Task<ActionResult<List<CovidCase>>> GetByCountry(string countryCode)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var cases = _dataLoader.GetCovidCasesByCountry(countryCode.ToUpper());
                return Ok(cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cases for country {CountryCode}", countryCode);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<object>> GetSummary([FromQuery] DateTime? date = null)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var summary = _dataLoader.GetGlobalSummary(date);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting summary");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("treemap/{date}")]
        public async Task<ActionResult<List<object>>> GetTreemapData(DateTime date)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var data = _dataLoader.GetTreemapData(date);

                if (!data.Any())
                {
                    return NotFound($"No data found for date {date:yyyy-MM-dd}");
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting treemap data for {Date}", date);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}