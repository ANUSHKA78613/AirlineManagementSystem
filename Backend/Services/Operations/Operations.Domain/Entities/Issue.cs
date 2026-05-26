using System;

namespace Operations.Domain.Entities
{
    public class Issue
    {
        public int IssueId { get; set; }
        public string Category { get; set; } = string.Empty;   // Complaint, Medical, Lost, Other
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        // Status: Open, InProgress, Resolved, Closed
        public string Status { get; set; } = "Open";
        // Priority: Low, Medium, High, Critical
        public string Priority { get; set; } = "Medium";
        public string? PNR { get; set; }           // linked PNR if any
        public int? PassengerId { get; set; }       // linked passenger if any
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        
        // Customer Care specific fields
        public string? PassengerEmail { get; set; }
        public string? StaffReply { get; set; }
    }
}
