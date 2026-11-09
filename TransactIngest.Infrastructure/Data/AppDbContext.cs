using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TransactIngest.Domain.Entities;

namespace TransactIngest.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();

        public DbSet<TransactionAudit> TransactionAudits =>
            Set<TransactionAudit>();

        protected override void ConfigureConventions(
                ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();
        }

        private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public UtcDateTimeConverter()
                : base(
                    value => value,
                    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            {
            }
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TransactionRecord>(payment =>
            {
                payment.HasKey(p => p.TransactionRecordId);

                payment.Property(p => p.SourceSystem)
                    .IsRequired()
                    .HasMaxLength(20);

                payment.Property(p => p.CardNumber)
                    .HasMaxLength(20);

                payment.Property(p => p.LocationCode)
                    .IsRequired()
                    .HasMaxLength(20);

                payment.Property(p => p.ProductName)
                    .IsRequired()
                    .HasMaxLength(100);

                payment.Property(p => p.Amount)
                    .HasPrecision(18, 2);

                payment.Property(p => p.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                payment.HasIndex(p => new
                {
                    p.SourceSystem,
                    p.TransactionId
                })
                .IsUnique();

                payment.HasMany(p => p.AuditHistory)
                    .WithOne(a => a.TransactionRecord)
                    .HasForeignKey(a => a.TransactionRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TransactionAudit>(audit =>
            {
                audit.HasKey(a => a.TransactionAuditId);

                audit.Property(a => a.FieldName)
                    .IsRequired()
                    .HasMaxLength(50);

                audit.Property(a => a.OldValue)
                    .HasMaxLength(500);

                audit.Property(a => a.NewValue)
                    .HasMaxLength(500);
            });
        }
    }
}
