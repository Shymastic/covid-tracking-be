using System.ComponentModel.DataAnnotations;

namespace Covid19DataAPI.Models
{
    public class CovidCase
    {
        public long Id { get; set; }

        public int CountryId { get; set; }

        [Required]
        public DateTime ReportDate { get; set; }

        public long Confirmed { get; set; }

        public long Deaths { get; set; }

        public long Recovered { get; set; }

        public long Active { get; set; }

        public long DailyConfirmed { get; set; }

        public long DailyDeaths { get; set; }

        public Country? Country { get; set; }
    }
}