using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers.OData
{
    public class CovidCasesController : ODataController
    {
        private readonly CovidDataLoader _dataLoader;
        private readonly ILogger<CovidCasesController> _logger;

        public CovidCasesController(CovidDataLoader dataLoader, ILogger<CovidCasesController> logger)
        {
            _dataLoader = dataLoader;
            _logger = logger;
        }

        [EnableQuery(PageSize = 50, MaxTop = 1000)]
        public async Task<IActionResult> Get()
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var cases = _dataLoader.GetCovidCases(0, 1000).AsQueryable();
                return Ok(cases);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OData CovidCases Get");
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [EnableQuery]
        public async Task<IActionResult> Get([FromRoute] long key)
        {
            try
            {
                await _dataLoader.EnsureDataLoadedAsync();
                var covidCase = _dataLoader.GetCovidCase(key);
                if (covidCase == null) return NotFound();
                return Ok(covidCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting COVID case {Id}", key);
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}