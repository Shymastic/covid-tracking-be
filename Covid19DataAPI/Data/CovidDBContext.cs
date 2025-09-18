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
            base.OnModelCreating(modelBuilder);

            // Country configuration
            modelBuilder.Entity<Country>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // We set IDs manually
                entity.Property(e => e.CountryCode).HasMaxLength(10).IsRequired();
                entity.Property(e => e.CountryName).HasMaxLength(100).IsRequired();
                entity.HasIndex(e => e.CountryCode).IsUnique();
            });

            // CovidCase configuration
            modelBuilder.Entity<CovidCase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // We set IDs manually
                entity.HasIndex(e => new { e.CountryId, e.ReportDate }).IsUnique();

                // Foreign key relationship
                entity.HasOne(e => e.Country)
                    .WithMany(c => c.CovidCases)
                    .HasForeignKey(e => e.CountryId);
            });
        }
    }
}