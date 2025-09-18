using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Covid19DataAPI.Models
{
    public class Country
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string CountryCode { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string CountryName { get; set; } = "";

        [StringLength(50)]
        public string? Region { get; set; }

        public long? Population { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonIgnore]
        public List<CovidCase> CovidCases { get; set; } = new();
    }
}