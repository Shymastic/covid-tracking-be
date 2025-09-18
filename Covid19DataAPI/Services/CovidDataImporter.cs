using CsvHelper;
using Covid19DataAPI.Models;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Covid19DataAPI.Services
{
    public class CovidDataImporter
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CovidDataImporter> _logger;
        private readonly CovidDataSources _dataSources;
        private readonly ImportSettings _importSettings;

        // In-memory storage
        private static readonly Dictionary<string, Country> _countries = new();
        private static readonly List<CovidCase> _covidCases = new();
        private static readonly object _lockObject = new();
        private static bool _dataImported = false;

        public CovidDataImporter(HttpClient httpClient, ILogger<CovidDataImporter> logger, IOptions<CovidDataSources> dataSources, IOptions<ImportSettings> importSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _dataSources = dataSources.Value;
            _importSettings = importSettings.Value;
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task<bool> ImportAllDataAsync()
        {
            lock (_lockObject)
            {
                if (_dataImported) return true;
            }

            try
            {
                _logger.LogInformation("Starting COVID-19 data import from configured URLs...");

                var success = true;

                // Import confirmed cases
                if (!await ImportTimeSeriesAsync(_dataSources.GlobalConfirmed, "confirmed"))
                    success = false;

                // Import deaths
                if (!await ImportTimeSeriesAsync(_dataSources.GlobalDeaths, "deaths"))
                    success = false;

                // Import recovered (may fail as this dataset is discontinued)
                await ImportTimeSeriesAsync(_dataSources.GlobalRecovered, "recovered");

                lock (_lockObject)
                {
                    _dataImported = success;
                }

                _logger.LogInformation($"Import completed. Countries: {_countries.Count}, Cases: {_covidCases.Count}");
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import COVID data");
                return false;
            }
        }

        private async Task<bool> ImportTimeSeriesAsync(string url, string dataType)
        {
            try
            {
                _logger.LogInformation($"Importing {dataType} from: {url}");

                var csvContent = await _httpClient.GetStringAsync(url);
                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Read header
                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;

                if (headers == null)
                {
                    _logger.LogWarning($"No headers found in {dataType} CSV");
                    return false;
                }

                // Find date columns (skip first 4: Province/State, Country/Region, Lat, Long)
                var dateColumns = headers
                    .Skip(4)
                    .Where(h => DateTime.TryParseExact(h, "M/d/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    .OrderByDescending(h => DateTime.ParseExact(h, "M/d/yy", CultureInfo.InvariantCulture))
                    .Take(_importSettings.MaxDaysToImport)
                    .ToList();

                _logger.LogInformation($"Processing {dateColumns.Count} date columns for {dataType}");

                var processedCountries = 0;

                // Read data rows
                while (await csv.ReadAsync() && processedCountries < _importSettings.MaxCountriesToProcess)
                {
                    try
                    {
                        var countryName = csv.GetField("Country/Region")?.Trim();
                        var provinceName = csv.GetField("Province/State")?.Trim();

                        // Skip if no country name or if it's a province/state (except for aggregated data)
                        if (string.IsNullOrWhiteSpace(countryName) ||
                            (!string.IsNullOrWhiteSpace(provinceName) && !countryName.Equals("US", StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // Ensure country exists
                        if (!_countries.ContainsKey(countryName))
                        {
                            var country = new Country
                            {
                                Id = _countries.Count + 1,
                                CountryCode = GetCountryCode(countryName),
                                CountryName = countryName,
                                Region = GetRegion(countryName),
                                Population = GetPopulation(countryName),
                                CreatedDate = DateTime.Now
                            };
                            _countries[countryName] = country;
                        }

                        var countryObj = _countries[countryName];

                        // Process each date column
                        foreach (var dateColumn in dateColumns)
                        {
                            var reportDate = DateTime.ParseExact(dateColumn, "M/d/yy", CultureInfo.InvariantCulture);
                            var valueStr = csv.GetField(dateColumn)?.Trim() ?? "0";

                            if (long.TryParse(valueStr, out long value) && value >= 0)
                            {
                                // Find or create COVID case record
                                var existingCase = _covidCases
                                    .FirstOrDefault(c => c.CountryId == countryObj.Id && c.ReportDate.Date == reportDate.Date);

                                if (existingCase == null)
                                {
                                    existingCase = new CovidCase
                                    {
                                        Id = _covidCases.Count + 1,
                                        CountryId = countryObj.Id,
                                        ReportDate = reportDate
                                    };
                                    _covidCases.Add(existingCase);
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

                        processedCountries++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing record in {dataType}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"Successfully imported {dataType}: {processedCountries} countries processed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing {dataType} from {url}");
                return false;
            }
        }

        // Data access methods
        public static List<Country> GetCountries() => _countries.Values.ToList();

        public static Country? GetCountry(int id) => _countries.Values.FirstOrDefault(c => c.Id == id);

        public static Country? GetCountryByCode(string code) =>
            _countries.Values.FirstOrDefault(c => c.CountryCode.Equals(code, StringComparison.OrdinalIgnoreCase));

        public static List<CovidCase> GetCovidCases(int skip = 0, int take = 50) =>
            _covidCases.OrderByDescending(c => c.ReportDate).ThenByDescending(c => c.Confirmed).Skip(skip).Take(take).ToList();

        public static CovidCase? GetCovidCase(long id) => _covidCases.FirstOrDefault(c => c.Id == id);

        public static List<CovidCase> GetCovidCasesByCountry(string countryCode)
        {
            var country = GetCountryByCode(countryCode);
            return country == null ? new List<CovidCase>() :
                _covidCases.Where(c => c.CountryId == country.Id).OrderByDescending(c => c.ReportDate).ToList();
        }

        public static List<CovidCase> GetCovidCasesByDate(DateTime date) =>
            _covidCases.Where(c => c.ReportDate.Date == date.Date).ToList();

        private string GetCountryCode(string countryName) => countryName switch
        {
            "United States" or "US" => "US",
            "China" => "CN",
            "India" => "IN",
            "Brazil" => "BR",
            "Russia" => "RU",
            "France" => "FR",
            "United Kingdom" => "GB",
            "Turkey" => "TR",
            "Iran" => "IR",
            "Germany" => "DE",
            "Italy" => "IT",
            "Indonesia" => "ID",
            "Pakistan" => "PK",
            "Ukraine" => "UA",
            "Poland" => "PL",
            "South Africa" => "ZA",
            "Netherlands" => "NL",
            "Morocco" => "MA",
            "Saudi Arabia" => "SA",
            "Spain" => "ES",
            "Canada" => "CA",
            "Argentina" => "AR",
            "Mexico" => "MX",
            "Philippines" => "PH",
            "Malaysia" => "MY",
            "Vietnam" => "VN",
            "Thailand" => "TH",
            "Japan" => "JP",
            _ => countryName.Length >= 2 ? countryName[..2].ToUpper() : "UN"
        };

        private string GetRegion(string countryName) => countryName switch
        {
            "China" or "India" or "Indonesia" or "Pakistan" or "Turkey" or "Iran" or "Philippines" or "Malaysia" or "Vietnam" or "Thailand" or "Japan" or "Saudi Arabia" => "Asia",
            "Russia" or "France" or "United Kingdom" or "Germany" or "Italy" or "Ukraine" or "Poland" or "Netherlands" or "Spain" => "Europe",
            "United States" or "US" or "Brazil" or "Canada" or "Argentina" or "Mexico" => "Americas",
            "South Africa" or "Morocco" => "Africa",
            _ => "Other"
        };

        private long GetPopulation(string countryName) => countryName switch
        {
            "China" => 1400000000,
            "India" => 1380000000,
            "United States" or "US" => 331000000,
            "Indonesia" => 274000000,
            "Pakistan" => 221000000,
            "Brazil" => 212000000,
            "Russia" => 146000000,
            "Mexico" => 129000000,
            "Japan" => 125000000,
            "Philippines" => 110000000,
            "Vietnam" => 97000000,
            "Turkey" => 84000000,
            "Iran" => 84000000,
            "Germany" => 83000000,
            "Thailand" => 70000000,
            "United Kingdom" => 67000000,
            "France" => 65000000,
            "Italy" => 60000000,
            "South Africa" => 59000000,
            "Spain" => 47000000,
            "Argentina" => 45000000,
            "Ukraine" => 44000000,
            "Poland" => 38000000,
            "Canada" => 38000000,
            "Morocco" => 37000000,
            "Saudi Arabia" => 35000000,
            "Malaysia" => 32000000,
            "Netherlands" => 17000000,
            _ => 10000000
        };
    }

    // Configuration classes
    public class CovidDataSources
    {
        public string GlobalConfirmed { get; set; } = "";
        public string GlobalDeaths { get; set; } = "";
        public string GlobalRecovered { get; set; } = "";
        public string USDaily { get; set; } = "";
    }

    public class ImportSettings
    {
        public bool AutoImportOnStartup { get; set; } = true;
        public int MaxCountriesToProcess { get; set; } = 50;
        public int MaxDaysToImport { get; set; } = 10;
    }
}