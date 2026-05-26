using System;

namespace Operations.Domain.Entities
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; } // Pilot, Cabin Crew, Ground Staff
        public string Status { get; set; } // Active, Inactive
        public DateTime CreatedAt { get; set; }
        public Employee() { CreatedAt = DateTime.UtcNow; Status = "Active"; EmployeeCode = $"EMP{DateTime.UtcNow.Ticks % 1000000}"; }
    }
}
