using Covid19DataAPI.Models;

namespace Covid19DataAPI.Services
{
    public class CovidDataLoader
    {
        private readonly ILogger<CovidDataLoader> _logger;

        // Static in-memory data cache to avoid repeated loading
        private static readonly Dictionary<string, List<CovidCase>> _dataCache = new();
        private static readonly Dictionary<string, Country> _countryCache = new();
        private static readonly object _lockObject = new();
        private static bool _dataLoaded = false;

        public CovidDataLoader(ILogger<CovidDataLoader> logger)
        {
            _logger = logger;
        }

        public async Task<bool> EnsureDataLoadedAsync()
        {
            lock (_lockObject)
            {
                if (_dataLoaded) return true;
            }

            try
            {
                await LoadStaticDataAsync();

                lock (_lockObject)
                {
                    _dataLoaded = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load COVID data");
                return false;
            }
        }

        private async Task LoadStaticDataAsync()
        {
            _logger.LogInformation("Loading COVID-19 data...");

            // Create static countries
            var countries = new[]
            {
                new Country { Id = 1, CountryCode = "US", CountryName = "United States", Region = "Americas", Population = 331000000 },
                new Country { Id = 2, CountryCode = "CN", CountryName = "China", Region = "Asia", Population = 1400000000 },
                new Country { Id = 3, CountryCode = "IN", CountryName = "India", Region = "Asia", Population = 1380000000 },
                new Country { Id = 4, CountryCode = "BR", CountryName = "Brazil", Region = "Americas", Population = 212000000 },
                new Country { Id = 5, CountryCode = "RU", CountryName = "Russia", Region = "Europe", Population = 146000000 },
                new Country { Id = 6, CountryCode = "FR", CountryName = "France", Region = "Europe", Population = 65000000 },
                new Country { Id = 7, CountryCode = "GB", CountryName = "United Kingdom", Region = "Europe", Population = 67000000 },
                new Country { Id = 8, CountryCode = "TR", CountryName = "Turkey", Region = "Asia", Population = 84000000 },
                new Country { Id = 9, CountryCode = "IR", CountryName = "Iran", Region = "Asia", Population = 84000000 },
                new Country { Id = 10, CountryCode = "DE", CountryName = "Germany", Region = "Europe", Population = 83000000 },
                new Country { Id = 11, CountryCode = "IT", CountryName = "Italy", Region = "Europe", Population = 60000000 },
                new Country { Id = 12, CountryCode = "ID", CountryName = "Indonesia", Region = "Asia", Population = 274000000 },
                new Country { Id = 13, CountryCode = "PK", CountryName = "Pakistan", Region = "Asia", Population = 221000000 },
                new Country { Id = 14, CountryCode = "UA", CountryName = "Ukraine", Region = "Europe", Population = 44000000 },
                new Country { Id = 15, CountryCode = "PL", CountryName = "Poland", Region = "Europe", Population = 38000000 },
                new Country { Id = 16, CountryCode = "ZA", CountryName = "South Africa", Region = "Africa", Population = 59000000 },
                new Country { Id = 17, CountryCode = "NL", CountryName = "Netherlands", Region = "Europe", Population = 17000000 },
                new Country { Id = 18, CountryCode = "MA", CountryName = "Morocco", Region = "Africa", Population = 37000000 },
                new Country { Id = 19, CountryCode = "SA", CountryName = "Saudi Arabia", Region = "Asia", Population = 35000000 },
                new Country { Id = 20, CountryCode = "ES", CountryName = "Spain", Region = "Europe", Population = 47000000 },
                new Country { Id = 21, CountryCode = "CA", CountryName = "Canada", Region = "Americas", Population = 38000000 },
                new Country { Id = 22, CountryCode = "AR", CountryName = "Argentina", Region = "Americas", Population = 45000000 },
                new Country { Id = 23, CountryCode = "MX", CountryName = "Mexico", Region = "Americas", Population = 129000000 },
                new Country { Id = 24, CountryCode = "PH", CountryName = "Philippines", Region = "Asia", Population = 110000000 },
                new Country { Id = 25, CountryCode = "MY", CountryName = "Malaysia", Region = "Asia", Population = 32000000 },
                new Country { Id = 26, CountryCode = "VN", CountryName = "Vietnam", Region = "Asia", Population = 97000000 },
                new Country { Id = 27, CountryCode = "TH", CountryName = "Thailand", Region = "Asia", Population = 70000000 },
                new Country { Id = 28, CountryCode = "JP", CountryName = "Japan", Region = "Asia", Population = 125000000 }
            };

            // Cache countries
            foreach (var country in countries)
            {
                _countryCache[country.CountryCode] = country;
            }

            // Generate realistic COVID data for each country
            var cases = new List<CovidCase>();
            var random = new Random(42); // Fixed seed for consistent data
            long caseId = 1;

            foreach (var country in countries)
            {
                // Generate data for last 30 days
                for (int days = 29; days >= 0; days--)
                {
                    var reportDate = DateTime.Today.AddDays(-days);

                    // Base numbers scaled by population
                    var populationFactor = (double)(country.Population ?? 1000000) / 1000000;
                    var baseCases = (long)(populationFactor * random.Next(50000, 500000));
                    var baseDeaths = (long)(baseCases * (random.NextDouble() * 0.02 + 0.005)); // 0.5-2.5% mortality
                    var baseRecovered = (long)(baseCases * (random.NextDouble() * 0.3 + 0.7)); // 70-100% recovery

                    // Add some daily variation
                    var dailyVariation = random.NextDouble() * 0.2 + 0.9; // 90-110% of base
                    var confirmed = (long)(baseCases * dailyVariation);
                    var deaths = (long)(baseDeaths * dailyVariation);
                    var recovered = (long)(baseRecovered * dailyVariation);

                    // Ensure recovered doesn't exceed confirmed
                    if (recovered > confirmed) recovered = (long)(confirmed * 0.9);

                    var covidCase = new CovidCase
                    {
                        Id = caseId++,
                        CountryId = country.Id,
                        ReportDate = reportDate,
                        Confirmed = confirmed,
                        Deaths = deaths,
                        Recovered = recovered,
                        Active = Math.Max(0, confirmed - deaths - recovered),
                        DailyConfirmed = days == 29 ? 0 : random.Next(1000, 50000),
                        DailyDeaths = days == 29 ? 0 : random.Next(10, 1000)
                    };

                    cases.Add(covidCase);
                }
            }

            // Cache the data
            _dataCache["all"] = cases;

            _logger.LogInformation($"Loaded {countries.Length} countries and {cases.Count} COVID cases");

            await Task.CompletedTask; // Make it async for consistency
        }

        public List<Country> GetCountries()
        {
            return _countryCache.Values.ToList();
        }

        public Country? GetCountry(int id)
        {
            return _countryCache.Values.FirstOrDefault(c => c.Id == id);
        }

        public Country? GetCountryByCode(string code)
        {
            return _countryCache.ContainsKey(code) ? _countryCache[code] : null;
        }

        public List<CovidCase> GetCovidCases(int skip = 0, int take = 50)
        {
            if (!_dataCache.ContainsKey("all")) return new List<CovidCase>();

            return _dataCache["all"]
                .OrderByDescending(c => c.ReportDate)
                .ThenByDescending(c => c.Confirmed)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public CovidCase? GetCovidCase(long id)
        {
            if (!_dataCache.ContainsKey("all")) return null;
            return _dataCache["all"].FirstOrDefault(c => c.Id == id);
        }

        public List<CovidCase> GetCovidCasesByCountry(string countryCode)
        {
            if (!_dataCache.ContainsKey("all")) return new List<CovidCase>();

            var country = GetCountryByCode(countryCode);
            if (country == null) return new List<CovidCase>();

            return _dataCache["all"]
                .Where(c => c.CountryId == country.Id)
                .OrderByDescending(c => c.ReportDate)
                .ToList();
        }

        public List<CovidCase> GetCovidCasesByDate(DateTime date)
        {
            if (!_dataCache.ContainsKey("all")) return new List<CovidCase>();

            return _dataCache["all"]
                .Where(c => c.ReportDate.Date == date.Date)
                .ToList();
        }

        public object GetGlobalSummary(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today.AddDays(-1);
            var cases = GetCovidCasesByDate(targetDate);

            if (!cases.Any())
            {
                return new
                {
                    message = "No data found",
                    date = targetDate,
                    totalCountries = _countryCache.Count
                };
            }

            var totalConfirmed = cases.Sum(c => c.Confirmed);
            var totalDeaths = cases.Sum(c => c.Deaths);
            var totalRecovered = cases.Sum(c => c.Recovered);

            return new
            {
                ReportDate = targetDate,
                TotalConfirmed = totalConfirmed,
                TotalDeaths = totalDeaths,
                TotalRecovered = totalRecovered,
                TotalActive = cases.Sum(c => c.Active),
                CountriesReporting = cases.Count,
                MortalityRate = totalConfirmed > 0 ? (double)totalDeaths / totalConfirmed * 100 : 0,
                RecoveryRate = totalConfirmed > 0 ? (double)totalRecovered / totalConfirmed * 100 : 0
            };
        }

        public List<object> GetTreemapData(DateTime date)
        {
            var cases = GetCovidCasesByDate(date);
            if (!cases.Any()) return new List<object>();

            var totalConfirmed = cases.Sum(c => c.Confirmed);

            return cases
                .Where(c => c.Confirmed > 0)
                .OrderByDescending(c => c.Confirmed)
                .Take(20) // Top 20 for treemap
                .Select(c => {
                    var country = GetCountry(c.CountryId);
                    return new
                    {
                        CountryName = country?.CountryName ?? "Unknown",
                        CountryCode = country?.CountryCode ?? "UN",
                        Region = country?.Region ?? "Unknown",
                        c.Confirmed,
                        c.Deaths,
                        c.Recovered,
                        c.Active,
                        PercentOfGlobal = totalConfirmed > 0 ? (double)c.Confirmed / totalConfirmed * 100 : 0,
                        MortalityRate = c.Confirmed > 0 ? (double)c.Deaths / c.Confirmed * 100 : 0
                    };
                })
                .ToList<object>();
        }
    }
}