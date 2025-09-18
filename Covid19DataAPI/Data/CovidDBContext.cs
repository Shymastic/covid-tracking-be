using Microsoft.EntityFrameworkCore;
using Covid19DataAPI.Models;

namespace Covid19DataAPI.Data
{
    public class CovidDbContext : DbContext
    {
        public CovidDbContext(DbContextOptions<CovidDbContext> options) : base(options)
        {
        }

        public DbSet<Country> Countries { get; set; }
        public DbSet<CovidCase> CovidCases { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Country>().HasKey(c => c.Id);
            modelBuilder.Entity<CovidCase>().HasKey(c => c.Id);

            modelBuilder.Entity<CovidCase>()
                .HasOne(c => c.Country)
                .WithMany(co => co.CovidCases)
                .HasForeignKey(c => c.CountryId);
        }
    }
}