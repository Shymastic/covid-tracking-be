using Covid19DataAPI.Models;
using Covid19DataAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Covid19DataAPI.Controllers.OData
{
    public class CovidCasesController : ODataController
    {
        [EnableQuery(PageSize = 50, MaxTop = 1000)]
        public IActionResult Get()
        {
            var cases = CovidDataImporter.GetCovidCases(0, 1000).AsQueryable();
            return Ok(cases);
        }

        [EnableQuery]
        public IActionResult Get([FromRoute] long key)
        {
            var covidCase = CovidDataImporter.GetCovidCase(key);
            return covidCase == null ? NotFound() : Ok(covidCase);
        }

        // OData specific action for getting cases by country
        [EnableQuery]
        [HttpGet]
        public IActionResult GetByCountryCode([FromODataUri] string countryCode)
        {
            var cases = CovidDataImporter.GetCovidCasesByCountry(countryCode).AsQueryable();
            return Ok(cases);
        }

        // OData specific action for getting cases by date range
        [EnableQuery]
        [HttpGet]
        public IActionResult GetByDateRange([FromODataUri] DateTime startDate, [FromODataUri] DateTime endDate)
        {
            var cases = CovidDataImporter.GetCovidCases(0, int.MaxValue)
                .Where(c => c.ReportDate >= startDate && c.ReportDate <= endDate)
                .AsQueryable();
            return Ok(cases);
        }
    }
}