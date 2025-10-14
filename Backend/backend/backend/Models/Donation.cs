using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Donation
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public Guid UserId { get; set; }    
        public virtual User User { get; set; }

        [Required]
        [ForeignKey("Charity")]
        public Guid CharityId { get; set; }
        public virtual Charity Charity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = "USD"; // default to USD
        [Column(TypeName = "decimal(18,2)")]
        public decimal PlatformFee { get; set; } = 0.00M;
        [Column(TypeName = "decimal(18,2)")]
        public decimal? StripeFee { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? NetAmount { get; set; }
        [Required]
        [ForeignKey("PaymentMethod")]
        public Guid PaymentMethodId { get; set; }
        public virtual PaymentMethod PaymentMethod { get; set; }
        public string? CustomerEmail { get; set; }
        [Required]
        [StringLength(50)]
        public string Type { get; set; } = "roundup"; // "direct" or "roundup"
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "pending"; // "pending", "completed", "failed"
        [MaxLength(255)]
        public string? StripePaymentIntentId { get; set; }
        [MaxLength(255)]
        public string? StripeChargeId { get; set; } // needed to represent the successful transaction in stripe
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        [Required]
        public bool IsTransferred { get; set; } = false; // whether funds have been transferred 
        [Required]
        public bool isActive { get; set; } = false; // soft delete
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
