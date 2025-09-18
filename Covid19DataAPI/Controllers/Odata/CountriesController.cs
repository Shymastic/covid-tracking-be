using Covid19DataAPI.Data;
using Covid19DataAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Covid19DataAPI.Controllers.OData
{
    public class CountriesController : ODataController
    {
        private readonly CovidDbContext _context;
        private readonly ILogger<CountriesController> _logger;

        public CountriesController(CovidDbContext context, ILogger<CountriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET /odata/Countries
        [EnableQuery(PageSize = 200, MaxTop = 1000)]
        public IQueryable<Country> Get()
        {
            _logger.LogInformation($"OData Countries GET - Total: {_context.Countries.Count()}");
            return _context.Countries.AsQueryable();
        }

        // GET /odata/Countries(5)
        [EnableQuery]
        public SingleResult<Country> Get([FromRoute] int key)
        {
            _logger.LogInformation($"OData Countries GET by key: {key}");
            return SingleResult.Create(_context.Countries.Where(c => c.Id == key));
        }
    }
}
