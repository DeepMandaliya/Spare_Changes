using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        public Guid CharityId { get; set; }

        [StringLength(255)]
        public string? PlaidTransactionId { get; set; }

        [StringLength(255)]
        public string? StripePaymentIntentId { get; set; }

        [StringLength(255)]
        public string? StripePayoutId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RoundUpAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Refunded

        [StringLength(1000)]
        public string? Description { get; set; }

        public DateTime TransactionDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        [StringLength(255)]
        public string? CustomerEmail { get; set; } 

        public bool IsDeleted { get; set; } = false; 

        // Concurrency Token
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Charity? Charity { get; set; }
    }
}