using Microsoft.EntityFrameworkCore;
using The_Charity.Models;

namespace The_Charity.AppDBContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<PlaidItem> PlaidItems { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Charity> Charities { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<UserPrefernce> UserPreferences { get; set; }
        public DbSet<DonationPreferences> DonationPreferences { get; set; }
        public DbSet<Donation> Donations { get; set; }
        public DbSet<Transfer> Transfers { get; set; }
        public DbSet<Payout> Payouts { get; set; }
        public DbSet<Activity> Activities { get; set; }

        public DbSet<Notification> Notifications {  get; set; }
        public DbSet<NotificationTemplate> NotificationsTemplates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User relationships
            modelBuilder.Entity<User>()
                .HasMany(u => u.PlaidItems)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.PaymentMethods)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Transactions)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Donations)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId);

            // UserPreference relationships
            modelBuilder.Entity<UserPrefernce>()
                .HasOne(up => up.User)
                .WithOne()
                .HasForeignKey<UserPrefernce>(up => up.UserId);

            modelBuilder.Entity<UserPrefernce>()
                .HasOne(up => up.DefaultCharity)
                .WithMany()
                .HasForeignKey(up => up.DefaultCharityId);

            // Fix: Change from WithMany to WithOne for DonationPreferences
            modelBuilder.Entity<DonationPreferences>()
                .HasOne(dp => dp.User)
                .WithOne(u => u.DonationPreferences) // Changed from WithMany to WithOne
                .HasForeignKey<DonationPreferences>(dp => dp.UserId) // Specify foreign key
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Donation>()
                .HasOne(d => d.User)
                .WithMany(u => u.Donations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Donation>()
                .HasOne(d => d.Charity)
                .WithMany()
                .HasForeignKey(d => d.CharityId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Donation>()
                .HasOne(d => d.PaymentMethod)
                .WithMany()
                .HasForeignKey(d => d.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Transaction relationships
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Charity)
                .WithMany()
                .HasForeignKey(t => t.CharityId);

            // Unique constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<PlaidItem>()
                .HasIndex(p => p.ItemId)
                .IsUnique();

            modelBuilder.Entity<PaymentMethod>()
                .HasIndex(p => p.StripePaymentMethodId)
                .IsUnique();

            modelBuilder.Entity<Transfer>()
                .HasOne(t => t.Charity)
                .WithMany()
                .HasForeignKey(t => t.CharityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payout>()
                .HasOne(p => p.Charity)
                .WithMany()
                .HasForeignKey(p => p.CharityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Activity>()
                .HasOne(a => a.Charity)
                .WithMany()
                .HasForeignKey(a => a.CharityId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Activity>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            base.OnModelCreating(modelBuilder);
        }
    }
}