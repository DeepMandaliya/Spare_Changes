using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Transfer
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid CharityId { get; set; }

        [ForeignKey("CharityId")]
        public virtual Charity Charity { get; set; }

        [StringLength(255)]
        public string StripeTransferId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [StringLength(2000)]
        public string Metadata { get; set; }
    }
}
