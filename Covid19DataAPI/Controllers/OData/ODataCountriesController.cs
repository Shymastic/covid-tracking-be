using Covid19DataAPI.Models;
using Covid19DataAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Covid19DataAPI.Controllers.OData
{
    public class CountriesController : ODataController
    {
        [EnableQuery(PageSize = 50)]
        public IActionResult Get()
        {
            var countries = CovidDataImporter.GetCountries().AsQueryable();
            return Ok(countries);
        }

        [EnableQuery]
        public IActionResult Get([FromRoute] int key)
        {
            var country = CovidDataImporter.GetCountry(key);
            return country == null ? NotFound() : Ok(country);
        }

        // OData specific action for getting countries by region
        [EnableQuery]
        [HttpGet]
        public IActionResult GetByRegion([FromODataUri] string region)
        {
            var countries = CovidDataImporter.GetCountries()
                .Where(c => c.Region != null && c.Region.Equals(region, StringComparison.OrdinalIgnoreCase))
                .AsQueryable();
            return Ok(countries);
        }
    }
}