using Microsoft.AspNetCore.Mvc;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CountriesController : ControllerBase
    {
        private readonly CovidDataLoader _dataLoader;
        private readonly ILogger<CountriesController> _logger;

        public CountriesController(CovidDataLoader dataLoader, ILogger<CountriesController> logger)
        {
            _dataLoader = dataLoader;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<Country>>> GetAll()
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var countries = _dataLoader.GetCountries();
                return Ok(countries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting countries");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Country>> Get(int id)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var country = _dataLoader.GetCountry(id);
                if (country == null) return NotFound();
                return Ok(country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting country {Id}", id);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("code/{countryCode}")]
        public async Task<ActionResult<Country>> GetByCode(string countryCode)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var country = _dataLoader.GetCountryByCode(countryCode.ToUpper());
                if (country == null) return NotFound();
                return Ok(country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting country by code {CountryCode}", countryCode);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}