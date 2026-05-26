namespace Dealer.Application.DTOs
{
    public class RegisterDealerDto
    {
        public string AgentName { get; set; } = string.Empty;
        public string AgencyName { get; set; } = string.Empty;
        public string AgentCode { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal CommissionRate { get; set; } = 0.05m;
    }

    public class WalletOperationDto
    {
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class CommissionRequestDto
    {
        public int AgentId { get; set; }
        public string AgentCode { get; set; } = string.Empty;
        public decimal BookingAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public string? PNR { get; set; }
    }
}
