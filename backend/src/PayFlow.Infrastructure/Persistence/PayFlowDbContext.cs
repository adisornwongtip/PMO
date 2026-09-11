using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;

namespace PayFlow.Infrastructure.Persistence;

public class PayFlowDbContext : DbContext, IPayFlowDbContext
{
    public PayFlowDbContext(DbContextOptions<PayFlowDbContext> options) : base(options)
    {
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<RoutingAttemptLog> RoutingAttemptLogs => Set<RoutingAttemptLog>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();
    public DbSet<ReconciliationItem> ReconciliationItems => Set<ReconciliationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Merchant
        modelBuilder.Entity<Merchant>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => m.ApiKey).IsUnique();
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.MerchantId, t.IdempotencyKey }).IsUnique();
            b.Property(t => t.Amount).HasPrecision(18, 2);
            b.HasMany(t => t.RoutingLogs)
             .WithOne()
             .HasForeignKey(l => l.PaymentTransactionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // RoutingRule
        modelBuilder.Entity<RoutingRule>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.MinAmount).HasPrecision(18, 2);
            b.Property(r => r.MaxAmount).HasPrecision(18, 2);
        });

        // ReconciliationBatch
        modelBuilder.Entity<ReconciliationBatch>(b =>
        {
            b.HasKey(rb => rb.Id);
            b.Property(rb => rb.TotalSettledAmount).HasPrecision(18, 2);
            b.HasMany(rb => rb.Items)
             .WithOne()
             .HasForeignKey(i => i.BatchId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ReconciliationItem
        modelBuilder.Entity<ReconciliationItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.StatementAmount).HasPrecision(18, 2);
            b.Property(i => i.InternalAmount).HasPrecision(18, 2);
            b.Property(i => i.Fee).HasPrecision(18, 2);
            b.Property(i => i.NetSettlement).HasPrecision(18, 2);
        });
    }
}
