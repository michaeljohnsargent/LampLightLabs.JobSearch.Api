using LampLightLabs.JobSearch.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LampLightLabs.JobSearch.Api.Data
{
    /// <summary>
    /// EF Core database context for persisted job records (Postgres in production, via
    /// <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>).
    ///
    /// Registered with <c>AddDbContext</c> in <c>Program.cs</c>, which registers it Scoped —
    /// one instance per HTTP request. This is intentional, not a default left unexamined:
    /// <see cref="DbContext"/> is not thread-safe and tracks entity state per unit of work,
    /// so it must never be Singleton (one instance shared across every concurrent request
    /// would corrupt tracked state) and gains nothing from being Transient (a fresh instance
    /// per injection site inside the same request would defeat change-tracking and open
    /// multiple connections for what should be one unit of work). Scoped is the only lifetime
    /// that matches how a request-scoped unit of work actually behaves — the textbook case
    /// referenced elsewhere in this codebase's DI registrations.
    /// </summary>
    public class JobSearchDbContext : DbContext
    {
        public JobSearchDbContext(DbContextOptions<JobSearchDbContext> options) : base(options)
        {
        }

        public DbSet<JobRecord> Jobs => Set<JobRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<JobRecord>(entity =>
            {
                // JobRecord.JobId doesn't match EF's "Id"/"{TypeName}Id" primary-key
                // convention (the type is JobRecord, not "Job"), so the key is configured
                // explicitly rather than relying on convention to guess it correctly.
                entity.HasKey(j => j.JobId);
                entity.Property(j => j.JobId).HasMaxLength(64);

                // Store the enum as its string name rather than the underlying int —
                // readable directly in the database, and stable if enum members are
                // ever reordered.
                entity.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);

                entity.Property(j => j.Result).HasMaxLength(2000);
            });
        }
    }
}
