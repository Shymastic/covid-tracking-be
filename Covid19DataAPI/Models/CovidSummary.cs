namespace Covid19DataAPI.Models
{
    public class CovidSummary
    {
        public DateTime ReportDate { get; set; }
        public long TotalConfirmed { get; set; }
        public long TotalDeaths { get; set; }
        public long TotalRecovered { get; set; }
        public long TotalActive { get; set; }
        public int CountriesReporting { get; set; }
        public double MortalityRate { get; set; }
    }
}