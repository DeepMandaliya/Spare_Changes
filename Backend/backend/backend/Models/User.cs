using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stripe;
using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string Username { get; set; }
        public string? firstName { get; set; }

        public string? lastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public string phoneNumber { get; set; }

        public string authProvider { get; set; }

        public string AuthSubject { get; set; }

        public bool isActive { get; set; }

        public  bool isDeleted  { get; set; }

        public bool updatedAt { get; set; }
       
        public string PlaidUserId { get; set; } = Guid.NewGuid().ToString();
        public string StripeCustomerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }

        public bool termsAccepted { get; set; }

        // Navigation properties
        public virtual ICollection<PlaidItem> PlaidItems { get; set; }
        public virtual ICollection<Models.PaymentMethod> PaymentMethods { get; set; }
        public virtual ICollection<Donation> Donations { get; set; }
        public virtual ICollection<Transaction> Transactions { get; set; }
        // Change this from ICollection to single object
        public virtual DonationPreferences DonationPreferences { get; set; }

    }
}
