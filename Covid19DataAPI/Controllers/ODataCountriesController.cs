using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers.OData
{
    public class CountriesController : ODataController
    {
        private readonly CovidDataLoader _dataLoader;
        private readonly ILogger<CountriesController> _logger;

        public CountriesController(CovidDataLoader dataLoader, ILogger<CountriesController> logger)
        {
            _dataLoader = dataLoader;
            _logger = logger;
        }

        [EnableQuery(PageSize = 100, MaxTop = 1000)]
        public async Task<IActionResult> Get()
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var countries = _dataLoader.GetCountries().AsQueryable();
                return Ok(countries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OData Countries Get");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [EnableQuery]
        public async Task<IActionResult> Get([FromRoute] int key)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var country = _dataLoader.GetCountry(key);
                if (country == null) return NotFound();
                return Ok(country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting country {Id}", key);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}