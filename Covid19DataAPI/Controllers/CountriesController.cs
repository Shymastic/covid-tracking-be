using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Data;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CountriesController : ControllerBase
    {
        private readonly CovidDbContext _context;

        public CountriesController(CovidDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<Country>>> GetAllCountries()
        {
            try
            {
                var countries = await _context.Countries.ToListAsync();
                return Ok(countries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Country>> GetCountry(int id)
        {
            try
            {
                var country = await _context.Countries.FindAsync(id);

                if (country == null)
                {
                    return NotFound();
                }

                return Ok(country);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("code/{countryCode}")]
        public async Task<ActionResult<Country>> GetCountryByCode(string countryCode)
        {
            try
            {
                var country = await _context.Countries
                    .FirstOrDefaultAsync(c => c.CountryCode == countryCode);

                if (country == null)
                {
                    return NotFound();
                }

                return Ok(country);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Country>> CreateCountry(Country country)
        {
            try
            {
                country.CreatedDate = DateTime.Now;

                _context.Countries.Add(country);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCountry), new { id = country.Id }, country);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}