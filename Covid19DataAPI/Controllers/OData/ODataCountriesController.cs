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

        public CountriesController(CovidDbContext context)
        {
            _context = context;
        }

        [EnableQuery(PageSize = 50)]
        public IQueryable<Country> Get()
        {
            return _context.Countries.AsQueryable();
        }

        [EnableQuery]
        public SingleResult<Country> Get([FromRoute] int key)
        {
            return SingleResult.Create(_context.Countries.Where(c => c.Id == key));
        }
    }
}