using System;
using System.ComponentModel.DataAnnotations;

namespace Identity.Domain.Entities
{
    public class RegistrationVerification
    {
        [Key]
        public string Email { get; set; }
        public string OtpCode { get; set; }
        public DateTime ExpiryTime { get; set; }
        public bool IsVerified { get; set; }
    }
}
