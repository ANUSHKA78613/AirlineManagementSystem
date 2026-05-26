namespace Identity.Application.DTOs
{
    public class RegistrationOtpDto
    {
        public string Email { get; set; }
    }

    public class VerifyRegistrationOtpDto
    {
        public string Email { get; set; }
        public string OtpCode { get; set; }
    }
}
