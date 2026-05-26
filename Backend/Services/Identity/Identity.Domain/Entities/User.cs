using System;

namespace Identity.Domain.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // Passenger, Admin, Staff, Dealer
        public DateTime CreatedAt { get; set; }
        public bool IsVerified { get; set; } = false;
        
        // Edge Case Properties
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
        public string? OtpCode { get; set; } // Nullable in DB
        public DateTime? OtpExpiryTime { get; set; }

        // SSO Properties
        public string? ExternalProvider { get; set; }     // e.g. "Google"
        public string? ExternalProviderId { get; set; }   // Google user ID
        public string? ProfileImageUrl { get; set; }      // From SSO provider

        // Image Upload
        public byte[]? ProfileImageBytes { get; set; }    // Stored natively in DB as requested

        public User()
        {
            CreatedAt = DateTime.UtcNow;
            Role = "Passenger"; // Default role
        }
    }
}

