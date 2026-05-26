namespace Identity.Application.DTOs
{
    public class VerifyOtpDto
    {
        public string EmailOrPhone { get; set; }
        public string OtpCode { get; set; }
    }
}
