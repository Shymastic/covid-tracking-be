using CsvHelper;
using Covid19DataAPI.Data;
using Covid19DataAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Covid19DataAPI.Services
{
    public class DataImportService
    {
        private readonly CovidDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<DataImportService> _logger;

        public DataImportService(CovidDbContext context, HttpClient httpClient, ILogger<DataImportService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> ImportGlobalDataAsync(string url, string dataType)
        {
            try
            {
                _logger.LogInformation($"Starting import from {url} for type {dataType}");

                // Download CSV content
                var csvContent = await _httpClient.GetStringAsync(url);

                using var reader = new StringReader(csvContent);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Read all records as dynamic
                var records = new List<Dictionary<string, object>>();

                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;

                if (headers == null) return false;

                while (await csv.ReadAsync())
                {
                    var record = new Dictionary<string, object>();
                    foreach (var header in headers)
                    {
                        record[header] = csv.GetField(header) ?? "";
                    }
                    records.Add(record);
                }

                // Find date columns (skip first 4 columns)
                var dateColumns = headers.Skip(4).ToList();

                int processedCount = 0;
                int successCount = 0;

                foreach (var record in records)
                {
                    processedCount++;

                    try
                    {
                        var countryName = record.ContainsKey("Country/Region") ?
                            record["Country/Region"]?.ToString() ?? "" : "";

                        if (string.IsNullOrWhiteSpace(countryName)) continue;

                        // Find or create country
                        var country = await _context.Countries
                            .FirstOrDefaultAsync(c => c.CountryName == countryName);

                        if (country == null)
                        {
                            country = new Country
                            {
                                CountryCode = GetCountryCode(countryName),
                                CountryName = countryName,
                                Region = GetRegionFromCountry(countryName),
                                CreatedDate = DateTime.Now
                            };

                            _context.Countries.Add(country);
                            await _context.SaveChangesAsync();
                        }

                        // Process each date column
                        foreach (var dateColumn in dateColumns)
                        {
                            if (DateTime.TryParseExact(dateColumn, "M/d/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime reportDate))
                            {
                                var valueStr = record.ContainsKey(dateColumn) ?
                                    record[dateColumn]?.ToString() ?? "0" : "0";

                                if (long.TryParse(valueStr, out long value))
                                {
                                    // Find existing record or create new
                                    var existingCase = await _context.CovidCases
                                        .FirstOrDefaultAsync(c => c.CountryId == country.Id && c.ReportDate.Date == reportDate.Date);

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
                                    existingCase.Active = existingCase.Confirmed - existingCase.Deaths - existingCase.Recovered;
                                }
                            }
                        }

                        successCount++;

                        // Save in batches
                        if (processedCount % 10 == 0)
                        {
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error processing record: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Import completed. Processed: {processedCount}, Success: {successCount}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error importing data from {url}");
                return false;
            }
        }

        private string GetCountryCode(string countryName)
        {
            var countryMappings = new Dictionary<string, string>
            {
                { "United States", "US" },
                { "US", "US" },
                { "China", "CN" },
                { "Italy", "IT" },
                { "Spain", "ES" },
                { "Germany", "DE" },
                { "France", "FR" },
                { "Iran", "IR" },
                { "United Kingdom", "GB" },
                { "Switzerland", "CH" },
                { "Belgium", "BE" },
                { "Netherlands", "NL" },
                { "Austria", "AT" },
                { "Korea, South", "KR" },
                { "Canada", "CA" },
                { "Portugal", "PT" },
                { "Brazil", "BR" },
                { "Australia", "AU" },
                { "Malaysia", "MY" },
                { "Turkey", "TR" },
                { "Japan", "JP" },
                { "Poland", "PL" },
                { "Thailand", "TH" },
                { "Indonesia", "ID" },
                { "Vietnam", "VN" },
                { "India", "IN" },
                { "Russia", "RU" },
                { "Mexico", "MX" },
                { "Peru", "PE" },
                { "South Africa", "ZA" },
                { "Ukraine", "UA" },
                { "Argentina", "AR" },
                { "Colombia", "CO" },
                { "Philippines", "PH" }
            };

            return countryMappings.ContainsKey(countryName) ? countryMappings[countryName] :
                   countryName.Length >= 2 ? countryName.Substring(0, 2).ToUpper() : "UN";
        }

        private string GetRegionFromCountry(string countryName)
        {
            var asianCountries = new[] { "China", "Japan", "Korea, South", "Thailand", "Malaysia", "Indonesia", "Vietnam", "India", "Iran", "Philippines" };
            var europeanCountries = new[] { "Italy", "Spain", "Germany", "France", "United Kingdom", "Switzerland", "Belgium", "Netherlands", "Austria", "Portugal", "Poland", "Russia", "Ukraine" };
            var americanCountries = new[] { "United States", "US", "Canada", "Brazil", "Mexico", "Peru", "Argentina", "Colombia" };
            var africanCountries = new[] { "South Africa" };
            var oceaniaCountries = new[] { "Australia" };

            if (asianCountries.Contains(countryName)) return "Asia";
            if (europeanCountries.Contains(countryName)) return "Europe";
            if (americanCountries.Contains(countryName)) return "Americas";
            if (africanCountries.Contains(countryName)) return "Africa";
            if (oceaniaCountries.Contains(countryName)) return "Oceania";

            return "Other";
        }
    }
}