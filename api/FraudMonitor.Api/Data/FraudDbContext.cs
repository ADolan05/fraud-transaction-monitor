using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Api.Data;

public class FraudDbContext(DbContextOptions<FraudDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

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
    }
}
