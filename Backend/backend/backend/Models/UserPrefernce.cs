using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class UserPrefernce
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        [ForeignKey("User")]
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
        [ForeignKey("Charity")]
        public Guid DefaultCharityId { get; set; }
        public virtual Charity DefaultCharity { get; set; }
        [Required]
        [MaxLength(10)]
        public string PreferredLanguage { get; set; } = "en"; // default to English
        public bool AutoRoundUp { get; set; } = true;
        public decimal RoundUpThreshold { get; set; } = 0.10m; // Minimum round-up amount
        public decimal MonthlyDonationLimit { get; set; } = 50.00m;
        public bool NotifyOnDonation { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
