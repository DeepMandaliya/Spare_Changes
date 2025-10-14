using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class NotificationTemplate
    {

        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Channel { get; set; } = null!; // 'email' or 'inapp'

        [StringLength(500)]
        public string? Subject { get; set; }

        public string? BodyHtml { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}