using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Data;
using Covid19DataAPI.Models;
using CsvHelper;
using System.Globalization;

namespace Covid19DataAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CovidCasesController : ControllerBase
    {
        private readonly CovidDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CovidCasesController> _logger;

        public CovidCasesController(CovidDbContext context, IHttpClientFactory httpClientFactory, ILogger<CovidCasesController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<CovidCase>>> GetAll()
        {
            try
            {
                var cases = await _context.CovidCases
                    .Include(c => c.Country)
                    .OrderByDescending(c => c.ReportDate)
                    .Take(50)
                    .ToListAsync();

                return Ok(cases);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CovidCase>> Get(long id)
        {
            try
            {
                var covidCase = await _context.CovidCases
                    .Include(c => c.Country)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (covidCase == null) return NotFound();
                return Ok(covidCase);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<CovidCase>> Create(CovidCase covidCase)
        {
            try
            {
                covidCase.Active = covidCase.Confirmed - covidCase.Deaths - covidCase.Recovered;
                _context.CovidCases.Add(covidCase);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(Get), new { id = covidCase.Id }, covidCase);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.Today.AddDays(-1);

                var cases = await _context.CovidCases
                    .Where(c => c.ReportDate.Date == targetDate.Date)
                    .ToListAsync();

                if (!cases.Any())
                {
                    return Ok(new
                    {
                        message = "No data found",
                        date = targetDate,
                        totalCountries = await _context.Countries.CountAsync(),
                        suggestion = "Try importing data first using POST /api/covidcases/import-all"
                    });
                }

                var summary = new
                {
                    ReportDate = targetDate,
                    TotalConfirmed = cases.Sum(c => c.Confirmed),
                    TotalDeaths = cases.Sum(c => c.Deaths),
                    TotalRecovered = cases.Sum(c => c.Recovered),
                    CountriesReporting = cases.Count(),
                    MortalityRate = cases.Sum(c => c.Confirmed) > 0 ?
                        (double)cases.Sum(c => c.Deaths) / cases.Sum(c => c.Confirmed) * 100 : 0
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("treemap/{date}")]
        public async Task<ActionResult> GetTreemapData(DateTime date)
        {
            try
            {
                var cases = await _context.CovidCases
                    .Include(c => c.Country)
                    .Where(c => c.ReportDate.Date == date.Date && c.Confirmed > 0)
                    .OrderByDescending(c => c.Confirmed)
                    .Take(50) // Top 50 countries
                    .ToListAsync();

                if (!cases.Any())
                {
                    return NotFound($"No data found for date {date:yyyy-MM-dd}");
                }

                var totalConfirmed = cases.Sum(c => c.Confirmed);

                var treemapData = cases.Select(c => new
                {
                    CountryName = c.Country!.CountryName,
                    CountryCode = c.Country.CountryCode,
                    Region = c.Country.Region,
                    Confirmed = c.Confirmed,
                    Deaths = c.Deaths,
                    Recovered = c.Recovered,
                    Active = c.Active,
                    PercentOfGlobal = totalConfirmed > 0 ? (double)c.Confirmed / totalConfirmed * 100 : 0,
                    MortalityRate = c.Confirmed > 0 ? (double)c.Deaths / c.Confirmed * 100 : 0
                }).ToList();

                return Ok(treemapData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("import-all")]
        public async Task<ActionResult> ImportAllData()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(10); // Increase timeout for large files

                var results = new List<string>();

                _logger.LogInformation("Starting COVID-19 data import from GitHub...");

                // 1. Import confirmed cases
                var confirmedUrl = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_confirmed_global.csv";
                var confirmedSuccess = await ImportTimeSeriesData(httpClient, confirmedUrl, "confirmed");
                results.Add($"Confirmed cases: {(confirmedSuccess ? "Success" : "Failed")}");

                // 2. Import deaths
                var deathsUrl = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_deaths_global.csv";
                var deathsSuccess = await ImportTimeSeriesData(httpClient, deathsUrl, "deaths");
                results.Add($"Deaths: {(deathsSuccess ? "Success" : "Failed")}");

                // 3. Import recovered
                var recoveredUrl = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_recovered_global.csv";
                var recoveredSuccess = await ImportTimeSeriesData(httpClient, recoveredUrl, "recovered");
                results.Add($"Recovered: {(recoveredSuccess ? "Success" : "Failed")}");

                // 4. Import US daily data (recent date)
                var usUrl = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_daily_reports_us/12-31-2023.csv";
                var usSuccess = await ImportUSData(httpClient, usUrl);
                results.Add($"US Daily: {(usSuccess ? "Success" : "Failed")}");

                var totalCountries = await _context.Countries.CountAsync();
                var totalCases = await _context.CovidCases.CountAsync();

                return Ok(new
                {
                    Message = "Import completed",
                    Results = results,
                    TotalCountries = totalCountries,
                    TotalCaseRecords = totalCases,
                    Timestamp = DateTime.Now,
                    NextSteps = "Try GET /api/covidcases/summary or GET /api/covidcases/treemap/2023-12-31"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import");
                return StatusCode(500, $"Import error: {ex.Message}");
            }
        }

        private async Task<bool> ImportTimeSeriesData(HttpClient httpClient, string url, string dataType)
        {
            try
            {
                _logger.LogInformation($"Importing {dataType} from {url}");

                var csvContent = await httpClient.GetStringAsync(url);
                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Read header first
                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;
                if (headers == null) return false;

                // Find date columns - skip Province/State, Country/Region, Lat, Long
                var dateColumns = headers.Where(h => DateTime.TryParseExact(h, "M/d/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                                         .OrderByDescending(h => DateTime.ParseExact(h, "M/d/yy", CultureInfo.InvariantCulture))
                                         .Take(30) // Last 30 dates only
                                         .ToList();

                _logger.LogInformation($"Found {dateColumns.Count} date columns for {dataType}");

                var processedCount = 0;
                var successCount = 0;

                // Read data rows
                while (await csv.ReadAsync())
                {
                    try
                    {
                        // Handle different CSV formats
                        var countryName = csv.GetField("Country/Region")?.Trim() ??
                                         csv.GetField("Country_Region")?.Trim();

                        if (string.IsNullOrWhiteSpace(countryName)) continue;

                        // Skip sub-regions, focus on countries
                        var provinceName = csv.GetField("Province/State")?.Trim() ??
                                          csv.GetField("Province_State")?.Trim();

                        if (!string.IsNullOrEmpty(provinceName) &&
                            !countryName.Equals("US", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip provinces except for US
                        }

                        // Find or create country
                        var country = await _context.Countries
                            .FirstOrDefaultAsync(c => c.CountryName == countryName);

                        if (country == null)
                        {
                            country = new Country
                            {
                                CountryCode = GetCountryCode(countryName),
                                CountryName = countryName,
                                Region = GetRegion(countryName),
                                CreatedDate = DateTime.Now
                            };
                            _context.Countries.Add(country);
                            await _context.SaveChangesAsync();
                        }

                        // Process recent date columns only
                        foreach (var dateColumn in dateColumns)
                        {
                            var reportDate = DateTime.ParseExact(dateColumn, "M/d/yy", CultureInfo.InvariantCulture);
                            var valueStr = csv.GetField(dateColumn)?.Trim() ?? "0";

                            if (long.TryParse(valueStr, out long value) && value >= 0)
                            {
                                // Find or create COVID case record
                                var existingCase = await _context.CovidCases
                                    .FirstOrDefaultAsync(c => c.CountryId == country.Id &&
                                                       c.ReportDate.Date == reportDate.Date);

                                if (existingCase == null)
                                {
                                    existingCase = new CovidCase
                                    {
                                        CountryId = country.Id,
                                        ReportDate = reportDate
                                    };
                                    _context.CovidCases.Add(existingCase);
                                }

                                // Update based on data type
                                switch (dataType.ToLower())
                                {
                                    case "confirmed":
                                        existingCase.Confirmed = value;
                                        break;
                                    case "deaths":
                                        existingCase.Deaths = value;
                                        break;
                                    case "recovered":
                                        existingCase.Recovered = value;
                                        break;
                                }

                                // Calculate active cases
                                existingCase.Active = Math.Max(0, existingCase.Confirmed - existingCase.Deaths - existingCase.Recovered);
                            }
                        }

                        processedCount++;
                        successCount++;

                        // Save in smaller batches
                        if (processedCount % 20 == 0)
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Processed {processedCount} countries for {dataType}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing record for {dataType}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Completed {dataType} import: {successCount}/{processedCount} records");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing {dataType}");
                return false;
            }
        }

        private async Task<bool> ImportUSData(HttpClient httpClient, string url)
        {
            try
            {
                _logger.LogInformation($"Importing US data from {url}");

                var csvContent = await httpClient.GetStringAsync(url);
                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                var records = csv.GetRecords<dynamic>().ToList();

                // Find or create US country
                var usCountry = await _context.Countries.FirstOrDefaultAsync(c => c.CountryCode == "US");
                if (usCountry == null)
                {
                    usCountry = new Country
                    {
                        CountryCode = "US",
                        CountryName = "United States",
                        Region = "Americas",
                        Population = 331000000,
                        CreatedDate = DateTime.Now
                    };
                    _context.Countries.Add(usCountry);
                    await _context.SaveChangesAsync();
                }

                DateTime reportDate = new DateTime(2023, 12, 31); // Fixed date for demo
                long totalConfirmed = 0, totalDeaths = 0, totalRecovered = 0;

                foreach (var record in records.Take(10)) // Limit for demo
                {
                    try
                    {
                        var recordDict = (IDictionary<string, object>)record;

                        if (recordDict.ContainsKey("Confirmed") && long.TryParse(recordDict["Confirmed"]?.ToString(), out long confirmed))
                            totalConfirmed += confirmed;

                        if (recordDict.ContainsKey("Deaths") && long.TryParse(recordDict["Deaths"]?.ToString(), out long deaths))
                            totalDeaths += deaths;

                        if (recordDict.ContainsKey("Recovered") && long.TryParse(recordDict["Recovered"]?.ToString(), out long recovered))
                            totalRecovered += recovered;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing US record: {ex.Message}");
                    }
                }

                // Create or update US aggregate data
                var existingUSCase = await _context.CovidCases
                    .FirstOrDefaultAsync(c => c.CountryId == usCountry.Id && c.ReportDate.Date == reportDate.Date);

                if (existingUSCase == null)
                {
                    var usCase = new CovidCase
                    {
                        CountryId = usCountry.Id,
                        ReportDate = reportDate,
                        Confirmed = totalConfirmed,
                        Deaths = totalDeaths,
                        Recovered = totalRecovered,
                        Active = totalConfirmed - totalDeaths - totalRecovered
                    };
                    _context.CovidCases.Add(usCase);
                }
                else
                {
                    existingUSCase.Confirmed = totalConfirmed;
                    existingUSCase.Deaths = totalDeaths;
                    existingUSCase.Recovered = totalRecovered;
                    existingUSCase.Active = totalConfirmed - totalDeaths - totalRecovered;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Completed US data import");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing US data from {url}");
                return false;
            }
        }

        private string GetCountryCode(string countryName)
        {
            var mappings = new Dictionary<string, string>
            {
                { "United States", "US" }, { "US", "US" },
                { "China", "CN" }, { "Italy", "IT" }, { "Spain", "ES" },
                { "Germany", "DE" }, { "France", "FR" }, { "Iran", "IR" },
                { "United Kingdom", "GB" }, { "Switzerland", "CH" },
                { "Belgium", "BE" }, { "Netherlands", "NL" },
                { "Austria", "AT" }, { "Korea, South", "KR" },
                { "Canada", "CA" }, { "Portugal", "PT" },
                { "Brazil", "BR" }, { "Australia", "AU" },
                { "Malaysia", "MY" }, { "Turkey", "TR" },
                { "Japan", "JP" }, { "Poland", "PL" },
                { "Thailand", "TH" }, { "Indonesia", "ID" },
                { "Vietnam", "VN" }, { "India", "IN" },
                { "Russia", "RU" }, { "Mexico", "MX" },
                { "Peru", "PE" }, { "South Africa", "ZA" },
                { "Ukraine", "UA" }, { "Argentina", "AR" },
                { "Colombia", "CO" }, { "Philippines", "PH" }
            };

            return mappings.ContainsKey(countryName) ? mappings[countryName] :
                   countryName.Length >= 2 ? countryName.Substring(0, 2).ToUpper() : "UN";
        }

        private string GetRegion(string countryName)
        {
            var asianCountries = new[] { "China", "Japan", "Korea, South", "Thailand", "Malaysia", "Indonesia", "Vietnam", "India", "Iran", "Philippines" };
            var europeanCountries = new[] { "Italy", "Spain", "Germany", "France", "United Kingdom", "Switzerland", "Belgium", "Netherlands", "Austria", "Portugal", "Poland", "Russia", "Ukraine" };
            var americanCountries = new[] { "United States", "US", "Canada", "Brazil", "Mexico", "Peru", "Argentina", "Colombia" };
            var africanCountries = new[] { "South Africa" };

            if (asianCountries.Contains(countryName)) return "Asia";
            if (europeanCountries.Contains(countryName)) return "Europe";
            if (americanCountries.Contains(countryName)) return "Americas";
            if (africanCountries.Contains(countryName)) return "Africa";

            return "Other";
        }

        [HttpPost("import")]
        public async Task<ActionResult> ImportData([FromBody] ImportRequest request)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                _logger.LogInformation($"Testing download from: {request.Url}");

                var csvContent = await httpClient.GetStringAsync(request.Url);
                var lines = csvContent.Split('\n').Take(5).ToList();

                return Ok(new
                {
                    Message = "File downloaded successfully",
                    Url = request.Url,
                    FileSizeBytes = csvContent.Length,
                    FirstFewLines = lines,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Download failed: {ex.Message}");
            }
        }

        [HttpPost("seed")]
        public async Task<ActionResult> SeedTestData()
        {
            try
            {
                // Create test countries if not exist
                if (!await _context.Countries.AnyAsync())
                {
                    var countries = new[]
                    {
                        new Country { CountryCode = "US", CountryName = "United States", Region = "Americas", Population = 331000000 },
                        new Country { CountryCode = "VN", CountryName = "Vietnam", Region = "Asia", Population = 97000000 },
                        new Country { CountryCode = "CN", CountryName = "China", Region = "Asia", Population = 1400000000 }
                    };

                    _context.Countries.AddRange(countries);
                    await _context.SaveChangesAsync();
                }

                // Create test COVID cases
                var usCountry = await _context.Countries.FirstAsync(c => c.CountryCode == "US");
                var vnCountry = await _context.Countries.FirstAsync(c => c.CountryCode == "VN");

                if (!await _context.CovidCases.AnyAsync())
                {
                    var testCases = new[]
                    {
                        new CovidCase
                        {
                            CountryId = usCountry.Id,
                            ReportDate = DateTime.Today.AddDays(-1),
                            Confirmed = 100000000,
                            Deaths = 1000000,
                            Recovered = 98000000,
                            Active = 1000000
                        },
                        new CovidCase
                        {
                            CountryId = vnCountry.Id,
                            ReportDate = DateTime.Today.AddDays(-1),
                            Confirmed = 11500000,
                            Deaths = 43000,
                            Recovered = 11400000,
                            Active = 57000
                        }
                    };

                    _context.CovidCases.AddRange(testCases);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = "Test data created successfully!",
                    countries = await _context.Countries.CountAsync(),
                    cases = await _context.CovidCases.CountAsync()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}