using FraudMonitor.Api.Fraud;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Api.Data;

public class FraudDbContext(DbContextOptions<FraudDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertReason> AlertReasons => Set<AlertReason>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.FullName).HasMaxLength(120);
            e.Property(c => c.HomeCity).HasMaxLength(80);
            e.Property(c => c.HomeState).HasMaxLength(2).IsFixedLength();
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.Property(a => a.AccountType).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.MaskedNumber).HasMaxLength(8);
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.HasOne(a => a.Customer)
                .WithMany(c => c.Accounts)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Merchant>(e =>
        {
            e.Property(m => m.Name).HasMaxLength(120);
            e.Property(m => m.Category).HasConversion<string>().HasMaxLength(30);
            e.Property(m => m.City).HasMaxLength(80);
            e.Property(m => m.State).HasMaxLength(2).IsFixedLength();
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Currency).HasMaxLength(3).IsFixedLength();
            e.Property(t => t.Channel).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

            // The velocity and history-based rules look up "recent transactions for this account".
            e.HasIndex(t => new { t.AccountId, t.Timestamp });

            e.HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Merchant)
                .WithMany()
                .HasForeignKey(t => t.MerchantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Rule>(e =>
        {
            e.Property(r => r.Code).HasMaxLength(40);
            e.HasIndex(r => r.Code).IsUnique();
            e.Property(r => r.Name).HasMaxLength(80);
            e.Property(r => r.Description).HasMaxLength(300);
            e.Property(r => r.Threshold).HasPrecision(10, 2);
            e.HasData(DefaultRules.All);
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            // One alert per flagged transaction.
            e.HasOne(a => a.Transaction)
                .WithOne(t => t.Alert)
                .HasForeignKey<Alert>(a => a.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            // The alert queue filters by status and sorts by age.
            e.HasIndex(a => new { a.Status, a.CreatedAt });
        });

        modelBuilder.Entity<AlertReason>(e =>
        {
            e.HasKey(r => new { r.AlertId, r.RuleId });
            e.Property(r => r.Detail).HasMaxLength(300);
            e.HasOne(r => r.Alert)
                .WithMany(a => a.Reasons)
                .HasForeignKey(r => r.AlertId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Rule)
                .WithMany()
                .HasForeignKey(r => r.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
