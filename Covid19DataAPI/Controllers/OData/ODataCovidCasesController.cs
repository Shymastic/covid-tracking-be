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

        public CovidCasesController(CovidDbContext context)
        {
            _context = context;
        }

        [EnableQuery(PageSize = 50, MaxTop = 1000)]
        public IQueryable<CovidCase> Get()
        {
            return _context.CovidCases.Include(c => c.Country).AsQueryable();
        }

        [EnableQuery]
        public SingleResult<CovidCase> Get([FromRoute] long key)
        {
            return SingleResult.Create(_context.CovidCases
                .Include(c => c.Country)
                .Where(c => c.Id == key));
        }
    }
}