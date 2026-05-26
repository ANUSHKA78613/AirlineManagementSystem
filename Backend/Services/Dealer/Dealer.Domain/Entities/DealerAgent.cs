using System;
namespace Dealer.Domain.Entities
{
    public class DealerAgent
    {
        public int AgentId { get; set; }
        public string AgentCode { get; set; }
        public string AgentName { get; set; }
        public string AgencyName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Status { get; set; } // Active, Inactive
        public decimal CommissionRate { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public bool IsActive { get; set; }
        public DealerAgent() { EnrollmentDate = DateTime.UtcNow; IsActive = false; Status = "Inactive"; AgentCode = $"AGT{DateTime.UtcNow.Ticks % 1000000}"; }
    }
}
