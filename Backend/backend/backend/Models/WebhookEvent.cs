using System;
using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class WebhookEvent
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Source { get; set; } // e.g. "stripe" or "plaid"

        [MaxLength(255)]
        public string? EventId { get; set; } // external event ID from Stripe/Plaid

        [Required]
        public string Payload { get; set; } // full JSON payload

        [Required]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow; // UTC timestamp when webhook was received

        [Required]
        public bool Processed { get; set; } = false; // whether it’s already handled

        public DateTime? ProcessedAt { get; set; } // time of processing, nullable
    }
}
