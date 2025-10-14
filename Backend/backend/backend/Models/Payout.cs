using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Payout
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CharityId { get; set; }

        [ForeignKey("CharityId")]
        public virtual Charity Charity { get; set; }

        [StringLength(255)]
        public string StripePayoutId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";
    }
}
