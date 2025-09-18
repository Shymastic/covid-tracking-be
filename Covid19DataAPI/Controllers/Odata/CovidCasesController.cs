using Covid19DataAPI.Data;
using Covid19DataAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Covid19DataAPI.Controllers.OData
{
    public class CovidCasesController : ODataController
    {
        private readonly CovidDbContext _context;
        private readonly ILogger<CovidCasesController> _logger;

        public CovidCasesController(CovidDbContext context, ILogger<CovidCasesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET /odata/CovidCases
        [EnableQuery(PageSize = 2000, MaxTop = 2000)]
        public IQueryable<CovidCase> Get()
        {
            _logger.LogInformation($"OData CovidCases GET - Total: {_context.CovidCases.Count()}");
            return _context.CovidCases.Include(c => c.Country).AsQueryable();
        }

        // GET /odata/CovidCases(123)
        [EnableQuery]
        public SingleResult<CovidCase> Get([FromRoute] long key)
        {
            _logger.LogInformation($"OData CovidCases GET by key: {key}");
            return SingleResult.Create(_context.CovidCases
                .Include(c => c.Country)
                .Where(c => c.Id == key));
        }
    }
}