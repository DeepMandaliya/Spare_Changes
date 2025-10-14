using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class PaymentMethod
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [StringLength(200)]
        public string? Label { get; set; }

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string StripePaymentMethodId { get; set; } = null!;

        public string? StripeCustomerId { get; set; }

        [StringLength(10)]
        public string? Last4Digit { get; set; }

        [StringLength(100)]
        public string? Brand { get; set; }

        public int? ExpMonth { get; set; }

        public int? ExpYear { get; set; }

        public bool IsDefault { get; set; }

        public bool RequiresVerification { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
    
    }
}
