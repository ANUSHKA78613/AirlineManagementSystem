using System;

namespace Operations.Domain.Entities
{
    public class NotificationRecord
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Email { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string Status { get; set; } // Sent, Failed
        public DateTime CreatedAt { get; set; }

        public NotificationRecord()
        {
            CreatedAt = DateTime.UtcNow;
            Status = "Pending";
        }
    }
}
