using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payment.Application.DTOs;
using Payment.Application.CQRS.Commands;
using Payment.Application.CQRS.Queries;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Payment.Infrastructure.Persistence;
using EventBus.Abstractions;
using EventBus.Events;
using Shared.Middleware.Exceptions;

namespace Payment.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PaymentDbContext _db;
        private readonly IEventBus _eventBus;

        public PaymentController(IMediator mediator, IConfiguration config, IHttpClientFactory httpClientFactory, PaymentDbContext db, IEventBus eventBus)
        {
            _mediator = mediator;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _db = db;
            _eventBus = eventBus;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllPayments()
        {
            var payments = await _db.Payments
                .AsNoTracking()
                .OrderByDescending(payment => payment.CreatedAt)
                .ToListAsync();
            return Ok(payments);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("details/{paymentId:int}")]
        public async Task<IActionResult> GetPaymentById(int paymentId)
        {
            var payment = await _db.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PaymentId == paymentId);
            if (payment == null) throw new NotFoundException("Payment", paymentId);
            return Ok(payment);
        }

        [HttpPost]
        public Task<IActionResult> CreatePayment([FromBody] ProcessPaymentDto dto) => ProcessPayment(dto);

        [HttpPost("pay")]
        public Task<IActionResult> Pay([FromBody] ProcessPaymentDto dto) => ProcessPayment(dto);

        [HttpPost("process")]
        public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentDto dto)
        {
            var result = await _mediator.Send(new ProcessPaymentCommand(dto));
            return Ok(result);
        }

        [HttpGet("{pnr}")]
        public async Task<IActionResult> GetPaymentsByPnr(string pnr)
        {
            var payments = await _mediator.Send(new GetPaymentByPnrQuery(pnr));
            if (!payments.Any()) return NotFound(new { message = "No payments found for this PNR." });
            return Ok(payments);
        }

        [HttpGet("refund-status/{pnr}")]
        public async Task<IActionResult> GetRefundStatus(string pnr)
        {
            var payment = await _db.Payments
                .Where(p => p.PNR == pnr)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment == null) return NotFound(new { error = "No payment found for this PNR." });

            var isRefunded = payment.Status == "Refunded" || payment.Status == "RefundPending" || payment.Status == "RefundFailed";
            return Ok(new
            {
                pnr,
                paymentStatus = payment.Status,
                isRefunded,
                refundId = payment.RefundId,
                refundAmount = payment.RefundAmount,
                refundedAt = payment.RefundedAt,
                originalAmount = payment.Amount,
                transactionId = payment.TransactionId,
                // In Razorpay test mode, refunds are instant
                testModeNote = "In Razorpay test mode, refunds are processed instantly. In production, refunds take 5-7 business days."
            });
        }

        public class RazorpayOrderRequest
        {
            public string PNR { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "INR";
        }

        public class RazorpayVerifyRequest
        {
            public string PNR { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Method { get; set; } = "Razorpay";
            public string razorpay_order_id { get; set; } = string.Empty;
            public string razorpay_payment_id { get; set; } = string.Empty;
            public string razorpay_signature { get; set; } = string.Empty;
        }

        public class RazorpayRefundRequest
        {
            public string PNR { get; set; } = string.Empty;
            public decimal? Amount { get; set; }
            public string? Reason { get; set; }
        }

        [HttpPost("razorpay/order")]
        public async Task<IActionResult> CreateRazorpayOrder([FromBody] RazorpayOrderRequest req)
        {
            if (req.Amount <= 0) throw new BadRequestException("Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(req.PNR)) throw new BadRequestException("PNR is required.");

            var keyId = _config["Razorpay:KeyId"];
            var keySecret = _config["Razorpay:KeySecret"];
            if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                throw new BadRequestException("Razorpay keys not configured.");

                var amountPaise = (long)Math.Round(req.Amount * 100m, MidpointRounding.AwayFromZero);

                var payload = new
                {
                    amount = amountPaise,
                    currency = req.Currency ?? "INR",
                    receipt = req.PNR,
                    payment_capture = 1,
                    notes = new { pnr = req.PNR }
                };

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                var authBytes = Encoding.ASCII.GetBytes($"{keyId}:{keySecret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var json = JsonSerializer.Serialize(payload);
                var resp = await client.PostAsync("v1/orders", new StringContent(json, Encoding.UTF8, "application/json"));
                var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new BadRequestException($"Razorpay order creation failed. Details: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var orderId = doc.RootElement.GetProperty("id").GetString();

            return Ok(new
            {
                keyId,
                orderId,
                amount = amountPaise,
                currency = req.Currency ?? "INR",
                receipt = req.PNR
            });
        }

        [HttpPost("razorpay/verify")]
        public async Task<IActionResult> VerifyRazorpay([FromBody] RazorpayVerifyRequest req)
        {
            var keySecret = _config["Razorpay:KeySecret"];
            if (string.IsNullOrWhiteSpace(keySecret))
                throw new BadRequestException("Razorpay secret not configured.");

            if (string.IsNullOrWhiteSpace(req.PNR)) throw new BadRequestException("PNR is required.");
            if (req.Amount <= 0) throw new BadRequestException("Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(req.razorpay_order_id) ||
                string.IsNullOrWhiteSpace(req.razorpay_payment_id) ||
                string.IsNullOrWhiteSpace(req.razorpay_signature))
            {
                throw new BadRequestException("Missing Razorpay verification fields.");
            }

            var data = $"{req.razorpay_order_id}|{req.razorpay_payment_id}";
            var expected = ComputeHmacSha256Hex(keySecret, data);
            if (!FixedTimeEqualsHex(expected, req.razorpay_signature))
            {
                throw new BadRequestException("Invalid payment signature.");
            }

            var payment = await UpsertPaymentByTransactionId(
                req.razorpay_payment_id,
                req.PNR,
                req.Amount,
                req.Method ?? "Razorpay",
                "Success"
            );

            return Ok(new { ok = true, transactionId = payment.TransactionId, status = payment.Status });
        }

        [AllowAnonymous]
        [HttpPost("razorpay/webhook")]
        public async Task<IActionResult> RazorpayWebhook()
        {
            var webhookSecret = _config["Razorpay:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
                throw new BadRequestException("Razorpay webhook secret not configured.");

            if (!Request.Headers.TryGetValue("X-Razorpay-Signature", out var signatureValues))
                throw new UnauthorizedException("Missing Razorpay signature header.");

                var providedSignature = signatureValues.ToString();
                Request.EnableBuffering();
                string body;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
                {
                    body = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                }

            var expected = ComputeHmacSha256Hex(webhookSecret, body);
            if (!FixedTimeEqualsHex(expected, providedSignature))
                throw new UnauthorizedException("Invalid webhook signature.");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? string.Empty : string.Empty;
            if (!root.TryGetProperty("payload", out var payload))
                throw new BadRequestException("Invalid webhook payload.");
            if (evt.StartsWith("refund.", StringComparison.OrdinalIgnoreCase))
            {
                if (!payload.TryGetProperty("refund", out var refundNode) || !refundNode.TryGetProperty("entity", out var refundEntity))
                    throw new BadRequestException("Missing refund entity.");

                var paymentId = refundEntity.TryGetProperty("payment_id", out var pidNode) ? pidNode.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(paymentId))
                    throw new BadRequestException("Missing payment id in refund event.");

                    var existing = await _db.Payments.FirstOrDefaultAsync(p => p.TransactionId == paymentId);
                    if (existing == null)
                    {
                        return Ok(new { ok = true, ignored = true, reason = "Payment not found yet for refund webhook.", eventName = evt });
                    }

                    existing.Status = evt switch
                    {
                        "refund.failed" => "RefundFailed",
                        _ => "Refunded"
                    };
                    await _db.SaveChangesAsync();
                    return Ok(new { ok = true, transactionId = existing.TransactionId, status = existing.Status, eventName = evt });
                }
            else
            {
                if (!payload.TryGetProperty("payment", out var paymentNode) || !paymentNode.TryGetProperty("entity", out var entity))
                    throw new BadRequestException("Missing payment entity.");

                var paymentId = entity.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(paymentId))
                    throw new BadRequestException("Missing payment id.");

                    var amountPaise = entity.TryGetProperty("amount", out var amountNode) ? amountNode.GetInt64() : 0L;
                    var amount = amountPaise / 100m;
                    var method = entity.TryGetProperty("method", out var methodNode) ? methodNode.GetString() ?? "Razorpay" : "Razorpay";
                    var status = entity.TryGetProperty("status", out var statusNode) ? statusNode.GetString() ?? "Pending" : "Pending";

                    var notes = entity.TryGetProperty("notes", out var notesNode) ? notesNode : default;
                    var pnr = notes.ValueKind == JsonValueKind.Object && notes.TryGetProperty("pnr", out var pnrNode)
                        ? pnrNode.GetString() ?? "UNKNOWN"
                        : "UNKNOWN";

                var mappedStatus = evt switch
                {
                    "payment.captured" => "Success",
                    "payment.failed" => "Failed",
                    "payment.authorized" => "Pending",
                    _ => status.Equals("captured", StringComparison.OrdinalIgnoreCase) ? "Success"
                        : status.Equals("failed", StringComparison.OrdinalIgnoreCase) ? "Failed"
                        : "Pending"
                };

                var payment = await UpsertPaymentByTransactionId(paymentId, pnr, amount, method, mappedStatus);
                return Ok(new { ok = true, transactionId = payment.TransactionId, status = payment.Status, eventName = evt });
            }
        }

        [Authorize(Roles = "Admin,Staff,Passenger,Dealer")]
        [HttpPost("razorpay/refund")]
        public async Task<IActionResult> CreateRazorpayRefund([FromBody] RazorpayRefundRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PNR)) throw new BadRequestException("PNR is required.");

            var keyId = _config["Razorpay:KeyId"];
            var keySecret = _config["Razorpay:KeySecret"];
            if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                throw new BadRequestException("Razorpay keys not configured.");

            var payment = await _db.Payments
                .Where(p => p.PNR == req.PNR)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
            if (payment == null) throw new NotFoundException("Payment", req.PNR);

            if (!string.Equals(payment.Status, "Success", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException($"Payment is not refundable in current state: {payment.Status}");

            var amountPaise = req.Amount.HasValue ? (long)Math.Round(req.Amount.Value * 100m, MidpointRounding.AwayFromZero) : (long?)null;
            if (amountPaise.HasValue && amountPaise.Value <= 0)
                throw new BadRequestException("Refund amount must be greater than zero.");

                var payload = new Dictionary<string, object?>
                {
                    ["notes"] = new Dictionary<string, string>
                    {
                        ["pnr"] = req.PNR,
                        ["reason"] = req.Reason ?? "Cancellation refund"
                    }
                };
                if (amountPaise.HasValue) payload["amount"] = amountPaise.Value;

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.razorpay.com/");
                var authBytes = Encoding.ASCII.GetBytes($"{keyId}:{keySecret}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var jsonBody = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonBody, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                var resp = await client.PostAsync($"v1/payments/{payment.TransactionId}/refund", content);
            var responseBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new BadRequestException($"Razorpay refund request failed. Details: {responseBody}");

            payment.Status = "RefundPending";
            var refundAmountActual = req.Amount ?? payment.Amount;
            payment.RefundAmount = refundAmountActual;
            payment.RefundedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            using var doc = JsonDocument.Parse(responseBody);
            var refundId = doc.RootElement.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
            var refundStatus = doc.RootElement.TryGetProperty("status", out var stNode) ? stNode.GetString() : "pending";

            // Store the Razorpay refund ID
            payment.RefundId = refundId;
            await _db.SaveChangesAsync();

            return Ok(new { ok = true, pnr = req.PNR, transactionId = payment.TransactionId, refundId, refundAmount = refundAmountActual, status = refundStatus });
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

        private async Task<Payment.Domain.Entities.Payment> UpsertPaymentByTransactionId(
            string transactionId, string pnr, decimal amount, string method, string status)
        {
            var isNewSuccess = false;

            var existing = await _db.Payments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
            if (existing != null)
            {
                if (existing.Status != "Success" && status == "Success") isNewSuccess = true;

                existing.PNR = string.IsNullOrWhiteSpace(existing.PNR) ? pnr : existing.PNR;
                existing.Amount = existing.Amount == 0 ? amount : existing.Amount;
                existing.PaymentMethod = string.IsNullOrWhiteSpace(existing.PaymentMethod) ? method : existing.PaymentMethod;
                existing.Status = status;
                await _db.SaveChangesAsync();

                if (isNewSuccess)
                {
                    _eventBus.Publish(new PaymentCompletedEvent { PNR = existing.PNR, TransactionId = existing.TransactionId, UserId = 0, Amount = existing.Amount });
                }
                return existing;
            }

            if (status == "Success") isNewSuccess = true;

            var payment = new Payment.Domain.Entities.Payment
            {
                PNR = pnr,
                Amount = amount,
                PaymentMethod = method,
                Status = status,
                TransactionId = transactionId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            if (isNewSuccess)
            {
                _eventBus.Publish(new PaymentCompletedEvent { CorrelationId = Guid.NewGuid().ToString("N"), PNR = payment.PNR, TransactionId = payment.TransactionId, UserId = 0, Amount = payment.Amount });
            }
            else if (status == "Failed")
            {
                _eventBus.Publish(new PaymentFailedEvent { CorrelationId = Guid.NewGuid().ToString("N"), PNR = payment.PNR, Reason = "Razorpay payment failed verification or webhook", UserId = 0, FlightId = 0 });
            }

            return payment;
        }
    }
}
