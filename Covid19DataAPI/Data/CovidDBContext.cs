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
                entity.Property(e => e.CountryCode).HasMaxLength(10).IsRequired();
                entity.Property(e => e.CountryName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Region).HasMaxLength(50);
                entity.HasIndex(e => e.CountryCode).IsUnique();
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // CovidCase configuration
            modelBuilder.Entity<CovidCase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Ensure auto-increment
                entity.HasIndex(e => new { e.CountryId, e.ReportDate }).IsUnique();
                entity.HasIndex(e => e.ReportDate);

                // Specify column types explicitly
                entity.Property(e => e.Confirmed).HasColumnType("bigint");
                entity.Property(e => e.Deaths).HasColumnType("bigint");
                entity.Property(e => e.Recovered).HasColumnType("bigint");
                entity.Property(e => e.Active).HasColumnType("bigint");
                entity.Property(e => e.DailyConfirmed).HasColumnType("bigint");
                entity.Property(e => e.DailyDeaths).HasColumnType("bigint");

                // Foreign key relationship
                entity.HasOne(e => e.Country)
                    .WithMany(c => c.CovidCases)
                    .HasForeignKey(e => e.CountryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}