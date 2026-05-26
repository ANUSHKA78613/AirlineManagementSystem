using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Payment.Infrastructure.Persistence;
using Serilog;

namespace Payment.API.Services
{
    public interface IRazorpayService
    {
        Task<RazorpayOrderResponse> CreateOrderAsync(string pnr, decimal amount, string currency = "INR");
        Task<RazorpayVerificationResponse> VerifyPaymentAsync(string orderId, string paymentId, string signature, string pnr, decimal amount);
        Task<RazorpayRefundResponse> CreateRefundAsync(string pnr, decimal? amount = null, string? reason = null);
        Task<bool> ValidateWebhookAsync(string signature, string body);
        Task<PaymentWebhookData?> ParseWebhookPayloadAsync(string body);
    }

    public class RazorpayOrderResponse
    {
        public string? KeyId { get; set; }
        public string? OrderId { get; set; }
        public long Amount { get; set; }
        public string? Currency { get; set; }
        public string? Receipt { get; set; }
        public string? Error { get; set; }
    }

    public class RazorpayVerificationResponse
    {
        public bool IsValid { get; set; }
        public string? TransactionId { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
    }

    public class RazorpayRefundResponse
    {
        public bool Success { get; set; }
        public string? RefundId { get; set; }
        public string? RefundStatus { get; set; }
        public string? Error { get; set; }
    }

    public class PaymentWebhookData
    {
        public string? EventType { get; set; }
        public string? PaymentId { get; set; }
        public string? RefundId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? Method { get; set; }
        public string? PNR { get; set; }
    }

    public class RazorpayService : IRazorpayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PaymentDbContext _dbContext;

        public RazorpayService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            PaymentDbContext dbContext)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _dbContext = dbContext;
        }

        public async Task<RazorpayOrderResponse> CreateOrderAsync(string pnr, decimal amount, string currency = "INR")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pnr))
                    return new RazorpayOrderResponse { Error = "PNR is required" };

                if (amount <= 0)
                    return new RazorpayOrderResponse { Error = "Amount must be greater than zero" };

                var keyId = _config["Razorpay:KeyId"];
                var keySecret = _config["Razorpay:KeySecret"];

                if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                    return new RazorpayOrderResponse { Error = "Razorpay credentials not configured" };

                // Convert amount to paise (1 INR = 100 paise)
                var amountPaise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

                var payload = new
                {
                    amount = amountPaise,
                    currency = currency,
                    receipt = pnr,
                    payment_capture = 1,
                    notes = new { pnr = pnr }
                };

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                
                var authBytes = Encoding.ASCII.GetBytes($"{keyId}:{keySecret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(authBytes));

                var json = JsonSerializer.Serialize(payload);
                var response = await client.PostAsync(
                    "v1/orders",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"Razorpay order creation failed for PNR {pnr}: {responseBody}");
                    return new RazorpayOrderResponse { Error = "Failed to create Razorpay order" };
                }

                using var doc = JsonDocument.Parse(responseBody);
                var orderId = doc.RootElement.GetProperty("id").GetString();

                Log.Information($"Razorpay order created successfully. OrderId: {orderId}, PNR: {pnr}, Amount: {amount}");

                return new RazorpayOrderResponse
                {
                    KeyId = keyId,
                    OrderId = orderId,
                    Amount = amountPaise,
                    Currency = currency,
                    Receipt = pnr
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating Razorpay order: {ex.Message}");
                return new RazorpayOrderResponse { Error = ex.Message };
            }
        }

        public async Task<RazorpayVerificationResponse> VerifyPaymentAsync(
            string orderId,
            string paymentId,
            string signature,
            string pnr,
            decimal amount)
        {
            try
            {
                var keySecret = _config["Razorpay:KeySecret"];

                if (string.IsNullOrWhiteSpace(keySecret))
                    return new RazorpayVerificationResponse { Error = "Razorpay secret not configured" };

                if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(signature))
                    return new RazorpayVerificationResponse { Error = "Missing verification parameters" };

                // Verify signature: HMAC-SHA256(order_id|payment_id, secret)
                var data = $"{orderId}|{paymentId}";
                var expectedSignature = ComputeHmacSha256Hex(keySecret, data);

                if (!FixedTimeEqualsHex(expectedSignature, signature))
                {
                    Log.Warning($"Invalid payment signature for PNR {pnr}. Provided: {signature}, Expected: {expectedSignature}");
                    return new RazorpayVerificationResponse { Error = "Invalid payment signature" };
                }

                // Save payment record
                var payment = await UpsertPaymentAsync(paymentId, pnr, amount, "Razorpay", "Success");

                Log.Information($"Payment verified successfully. TransactionId: {paymentId}, PNR: {pnr}");

                return new RazorpayVerificationResponse
                {
                    IsValid = true,
                    TransactionId = paymentId,
                    Status = "Success"
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Error verifying payment: {ex.Message}");
                return new RazorpayVerificationResponse { Error = ex.Message };
            }
        }

        public async Task<RazorpayRefundResponse> CreateRefundAsync(string pnr, decimal? amount = null, string? reason = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pnr))
                    return new RazorpayRefundResponse { Error = "PNR is required" };

                var keyId = _config["Razorpay:KeyId"];
                var keySecret = _config["Razorpay:KeySecret"];

                if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                    return new RazorpayRefundResponse { Error = "Razorpay credentials not configured" };

                // Find the latest successful payment for this PNR
                var payment = await _dbContext.Payments
                    .Where(p => p.PNR == pnr && p.Status == "Success")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (payment == null)
                    return new RazorpayRefundResponse { Error = "No successful payment found for this PNR" };

                var amountPaise = amount.HasValue
                    ? (long)Math.Round(amount.Value * 100m, MidpointRounding.AwayFromZero)
                    : (long?)null;

                if (amountPaise.HasValue && amountPaise.Value <= 0)
                    return new RazorpayRefundResponse { Error = "Refund amount must be greater than zero" };

                var payload = new Dictionary<string, object?>
                {
                    ["notes"] = new Dictionary<string, string>
                    {
                        ["pnr"] = pnr,
                        ["reason"] = reason ?? "Cancellation refund"
                    }
                };

                if (amountPaise.HasValue)
                    payload["amount"] = amountPaise.Value;

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                
                var authBytes = Encoding.ASCII.GetBytes($"{keyId}:{keySecret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(authBytes));

                var json = JsonSerializer.Serialize(payload);
                var response = await client.PostAsync(
                    $"v1/payments/{payment.TransactionId}/refund",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"Razorpay refund failed for PNR {pnr}: {responseBody}");
                    return new RazorpayRefundResponse { Error = "Failed to create refund" };
                }

                payment.Status = "RefundPending";
                await _dbContext.SaveChangesAsync();

                using var doc = JsonDocument.Parse(responseBody);
                var refundId = doc.RootElement.TryGetProperty("id", out var idNode)
                    ? idNode.GetString()
                    : null;
                var refundStatus = doc.RootElement.TryGetProperty("status", out var stNode)
                    ? stNode.GetString()
                    : "pending";

                Log.Information($"Refund created successfully. RefundId: {refundId}, PNR: {pnr}");

                return new RazorpayRefundResponse
                {
                    Success = true,
                    RefundId = refundId,
                    RefundStatus = refundStatus
                };
            }
            catch (Exception ex)
            {
                Log.Error($"Error creating refund: {ex.Message}");
                return new RazorpayRefundResponse { Error = ex.Message };
            }
        }

        public async Task<bool> ValidateWebhookAsync(string signature, string body)
        {
            try
            {
                var webhookSecret = _config["Razorpay:WebhookSecret"];
                if (string.IsNullOrWhiteSpace(webhookSecret))
                {
                    Log.Warning("Razorpay webhook secret not configured");
                    return false;
                }

                var expectedSignature = ComputeHmacSha256Hex(webhookSecret, body);
                return FixedTimeEqualsHex(expectedSignature, signature);
            }
            catch (Exception ex)
            {
                Log.Error($"Error validating webhook: {ex.Message}");
                return false;
            }
        }

        public async Task<PaymentWebhookData?> ParseWebhookPayloadAsync(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;
                if (string.IsNullOrWhiteSpace(eventType))
                    return null;

                if (!root.TryGetProperty("payload", out var payload))
                    return null;

                if (eventType.StartsWith("refund.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!payload.TryGetProperty("refund", out var refundNode) || 
                        !refundNode.TryGetProperty("entity", out var refundEntity))
                        return null;

                    var paymentId = refundEntity.TryGetProperty("payment_id", out var pidNode)
                        ? pidNode.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(paymentId))
                        return null;

                    var existing = await _dbContext.Payments
                        .FirstOrDefaultAsync(p => p.TransactionId == paymentId);

                    if (existing == null)
                        return null;

                    var refundStatus = eventType switch
                    {
                        "refund.failed" => "RefundFailed",
                        "refund.processed" => "Refunded",
                        _ => "Refunding"
                    };

                    existing.Status = refundStatus;
                    await _dbContext.SaveChangesAsync();

                    return new PaymentWebhookData
                    {
                        EventType = eventType,
                        RefundId = refundEntity.TryGetProperty("id", out var ridNode) ? ridNode.GetString() : null,
                        PaymentId = paymentId,
                        Status = refundStatus,
                        PNR = existing.PNR,
                        Amount = existing.Amount
                    };
                }
                else
                {
                    if (!payload.TryGetProperty("payment", out var paymentNode) ||
                        !paymentNode.TryGetProperty("entity", out var entity))
                        return null;

                    var paymentId = entity.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                    if (string.IsNullOrWhiteSpace(paymentId))
                        return null;

                    var amountPaise = entity.TryGetProperty("amount", out var amountNode)
                        ? amountNode.GetInt64()
                        : 0L;
                    var amount = amountPaise / 100m;

                    var method = entity.TryGetProperty("method", out var methodNode)
                        ? methodNode.GetString() ?? "Razorpay"
                        : "Razorpay";

                    var status = entity.TryGetProperty("status", out var statusNode)
                        ? statusNode.GetString() ?? "Pending"
                        : "Pending";

                    var notes = entity.TryGetProperty("notes", out var notesNode) ? notesNode : default;
                    var pnr = notes.ValueKind == JsonValueKind.Object && notes.TryGetProperty("pnr", out var pnrNode)
                        ? pnrNode.GetString() ?? "UNKNOWN"
                        : "UNKNOWN";

                    var mappedStatus = eventType switch
                    {
                        "payment.captured" => "Success",
                        "payment.failed" => "Failed",
                        "payment.authorized" => "Pending",
                        _ => status.Equals("captured", StringComparison.OrdinalIgnoreCase) ? "Success"
                            : status.Equals("failed", StringComparison.OrdinalIgnoreCase) ? "Failed"
                            : "Pending"
                    };

                    var payment = await UpsertPaymentAsync(paymentId, pnr, amount, method, mappedStatus);

                    return new PaymentWebhookData
                    {
                        EventType = eventType,
                        PaymentId = paymentId,
                        Amount = amount,
                        Status = mappedStatus,
                        Method = method,
                        PNR = pnr
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error parsing webhook payload: {ex.Message}");
                return null;
            }
        }

        private static string ComputeHmacSha256Hex(string secret, string data)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool FixedTimeEqualsHex(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            
            var ab = Encoding.ASCII.GetBytes(a);
            var bb = Encoding.ASCII.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(ab, bb);
        }

        private async Task<Domain.Entities.Payment> UpsertPaymentAsync(
            string transactionId,
            string pnr,
            decimal amount,
            string method,
            string status)
        {
            var existing = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == transactionId);

            if (existing != null)
            {
                existing.PNR = string.IsNullOrWhiteSpace(existing.PNR) ? pnr : existing.PNR;
                existing.Amount = existing.Amount == 0 ? amount : existing.Amount;
                existing.PaymentMethod = string.IsNullOrWhiteSpace(existing.PaymentMethod) ? method : existing.PaymentMethod;
                existing.Status = status;
                await _dbContext.SaveChangesAsync();
                return existing;
            }

            var payment = new Domain.Entities.Payment
            {
                PNR = pnr,
                Amount = amount,
                PaymentMethod = method,
                Status = status,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Payments.Add(payment);
            await _dbContext.SaveChangesAsync();
            return payment;
        }
    }
}
