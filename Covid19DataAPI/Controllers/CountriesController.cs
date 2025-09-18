using Microsoft.AspNetCore.Mvc;
using Covid19DataAPI.Services;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CountriesController : ControllerBase
    {
        private readonly CovidDataImporter _importer;

        public CountriesController(CovidDataImporter importer)
        {
            _importer = importer;
        }

        [HttpGet]
        public ActionResult<List<Country>> GetAll()
        {
            return Ok(CovidDataImporter.GetCountries());
        }

        [HttpGet("{id}")]
        public ActionResult<Country> Get(int id)
        {
            var country = CovidDataImporter.GetCountry(id);
            return country == null ? NotFound() : Ok(country);
        }

        [HttpGet("code/{countryCode}")]
        public ActionResult<Country> GetByCode(string countryCode)
        {
            var country = CovidDataImporter.GetCountryByCode(countryCode);
            return country == null ? NotFound() : Ok(country);
        }


    }
}