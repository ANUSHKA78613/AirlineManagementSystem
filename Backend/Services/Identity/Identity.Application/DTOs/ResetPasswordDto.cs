namespace Identity.Application.DTOs
{
    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string OtpCode { get; set; }
        public string NewPassword { get; set; }
    }
}
