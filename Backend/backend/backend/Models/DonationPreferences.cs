using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class DonationPreferences
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public Guid? DefaultCharityId { get; set; }

        [ForeignKey("DefaultCharityId")]
        public Charity DefaultCharity { get; set; }

        public int DonationDayOfMonth { get; set; } = 1; // Default to 1st of month

        public bool AutoRoundUp { get; set; } = true;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RoundUpThreshold { get; set; } = 0.10m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyDonationLimit { get; set; } = 50.00m;

        public bool NotifyOnDonation { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
