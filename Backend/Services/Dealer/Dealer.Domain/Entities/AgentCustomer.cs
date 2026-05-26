using System;

namespace Dealer.Domain.Entities
{
    /// <summary>
    /// Maps the relationship between a dealer agent and their assigned customers.
    /// Enables tracking which customers were onboarded by which agent for commission purposes.
    /// </summary>
    public class AgentCustomer
    {
        public int AgentCustomerId { get; set; }
        public int AgentId { get; set; }
        public int CustomerId { get; set; } // UserId from Identity service
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime AssignedDate { get; set; }
        public bool IsActive { get; set; }

        public DealerAgent? Agent { get; set; } // Navigation property

        public AgentCustomer()
        {
            AssignedDate = DateTime.UtcNow;
            IsActive = true;
        }
    }
}
