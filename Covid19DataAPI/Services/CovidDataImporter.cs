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
        public static void CalculateDailyChanges()
        {
            Console.WriteLine("Calculating daily changes...");

            // Group cases by country and sort by date
            var casesByCountry = _covidCases
                .GroupBy(c => c.CountryId)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.ReportDate).ToList());

            foreach (var countryId in casesByCountry.Keys)
            {
                var countryCases = casesByCountry[countryId];

                for (int i = 1; i < countryCases.Count; i++)
                {
                    var currentCase = countryCases[i];
                    var previousCase = countryCases[i - 1];

                    // Calculate daily changes
                    currentCase.DailyConfirmed = Math.Max(0, currentCase.Confirmed - previousCase.Confirmed);
                    currentCase.DailyDeaths = Math.Max(0, currentCase.Deaths - previousCase.Deaths);

                    // For first day, daily = total
                    if (i == 1)
                    {
                        previousCase.DailyConfirmed = previousCase.Confirmed;
                        previousCase.DailyDeaths = previousCase.Deaths;
                    }
                }
            }

            Console.WriteLine("Daily changes calculated successfully");
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

                // Calculate daily changes after all data is imported
                if (success)
                {
                    CalculateDailyChanges();
                }

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

                // Dictionary to aggregate province/state data by country
                var countryAggregation = new Dictionary<string, Dictionary<DateTime, long>>();
                var processedCountries = new HashSet<string>();

                // Read data rows
                while (await csv.ReadAsync())
                {
                    try
                    {
                        var countryName = csv.GetField("Country/Region")?.Trim();
                        var provinceName = csv.GetField("Province/State")?.Trim();

                        // Skip if no country name
                        if (string.IsNullOrWhiteSpace(countryName))
                            continue;

                        // Normalize country names
                        countryName = NormalizeCountryName(countryName);

                        // Initialize country aggregation if not exists
                        if (!countryAggregation.ContainsKey(countryName))
                        {
                            countryAggregation[countryName] = new Dictionary<DateTime, long>();
                        }

                        // Process each date column
                        foreach (var dateColumn in dateColumns)
                        {
                            var reportDate = DateTime.ParseExact(dateColumn, "M/d/yy", CultureInfo.InvariantCulture);
                            var valueStr = csv.GetField(dateColumn)?.Trim() ?? "0";

                            if (long.TryParse(valueStr, out long value) && value >= 0)
                            {
                                // Aggregate values by country (sum provinces/states)
                                if (!countryAggregation[countryName].ContainsKey(reportDate))
                                {
                                    countryAggregation[countryName][reportDate] = 0;
                                }
                                countryAggregation[countryName][reportDate] += value;
                            }
                        }

                        processedCountries.Add(countryName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing record in {dataType}: {ex.Message}");
                    }
                }

                // Now create Country and CovidCase objects from aggregated data
                foreach (var countryEntry in countryAggregation)
                {
                    var countryName = countryEntry.Key;
                    var countryData = countryEntry.Value;

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

                    // Create CovidCase records for each date
                    foreach (var dateData in countryData)
                    {
                        var reportDate = dateData.Key;
                        var aggregatedValue = dateData.Value;

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
                                existingCase.Confirmed = aggregatedValue;
                                break;
                            case "deaths":
                                existingCase.Deaths = aggregatedValue;
                                break;
                            case "recovered":
                                existingCase.Recovered = aggregatedValue;
                                break;
                        }

                        // Calculate active cases
                        existingCase.Active = Math.Max(0, existingCase.Confirmed - existingCase.Deaths - existingCase.Recovered);
                    }
                }

                _logger.LogInformation($"Successfully imported {dataType}: {processedCountries.Count} unique countries processed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing {dataType} from {url}");
                return false;
            }
        }

        private string NormalizeCountryName(string countryName)
        {
            return countryName switch
            {
                "US" => "United States",
                "Korea, South" => "South Korea",
                "Taiwan*" => "Taiwan",
                "Burma" => "Myanmar",
                _ => countryName
            };
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
            "Canada" => "CA",
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
            "Argentina" => "AR",
            "Mexico" => "MX",
            "Philippines" => "PH",
            "Malaysia" => "MY",
            "Vietnam" => "VN",
            "Thailand" => "TH",
            "Japan" => "JP",
            "South Korea" => "KR",
            "Taiwan" => "TW",
            "Myanmar" => "MM",
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
        public int MaxCountriesToProcess { get; set; } = 500;
        public int MaxDaysToImport { get; set; } = 10;
    }


}