using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Dealer.API.Controllers
{
    [Authorize(Roles = "Dealer,Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class DealerController : ControllerBase
    {
        private readonly DealerDbContext _context;
        private readonly ILogger<DealerController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public DealerController(DealerDbContext context, ILogger<DealerController> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // ─── Dealer Registration & CRUD ────────────────────────────────

        /// <summary>Register a new dealer/travel agent (auto-creates wallet) — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DealerAgent agent)
        {
            if (string.IsNullOrWhiteSpace(agent.AgentName) || string.IsNullOrWhiteSpace(agent.AgencyName))
                return BadRequest(new { error = "Agent name and agency name are required." });

            var existing = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentCode == agent.AgentCode);
            if (existing != null)
            {
                if (existing.IsActive)
                    return Conflict(new { error = $"Agent code '{agent.AgentCode}' is already registered." });
                else
                {
                    existing.IsActive = true;
                    existing.AgentName = agent.AgentName;
                    existing.AgencyName = agent.AgencyName;
                    existing.Email = agent.Email;
                    existing.Phone = agent.Phone;
                    existing.CommissionRate = agent.CommissionRate;
                    existing.Status = "Active";
                    await _context.SaveChangesAsync();
                    
                    var newWallet = new DealerWallet { AgentId = existing.AgentId, Balance = 0m, CreatedAt = DateTime.UtcNow };
                    _context.Wallets.Add(newWallet);
                    await _context.SaveChangesAsync();
                    
                    return Ok(new { message = "Dealer reactivated.", agent = existing, walletId = newWallet.WalletId });
                }
            }

            _context.DealerAgents.Add(agent);
            await _context.SaveChangesAsync();

            var wallet = new DealerWallet
            {
                AgentId = agent.AgentId,
                Balance = 0m,
                CreatedAt = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Registered new dealer agent: {AgentCode} ({AgentName}) with wallet", agent.AgentCode, agent.AgentName);
            return Ok(new { message = "Dealer registered with wallet.", agent, walletId = wallet.WalletId });
        }

        /// <summary>List all registered dealers</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
        {
            var query = _context.DealerAgents.AsQueryable();
            if (activeOnly.HasValue && activeOnly.Value)
                query = query.Where(a => a.IsActive);

            return Ok(await query.OrderBy(a => a.AgencyName).ToListAsync());
        }

        /// <summary>Internal endpoint: Check if a dealer is active by email (used by Identity service during login)</summary>
        [AllowAnonymous]
        [HttpGet("internal/check-active")]
        public async Task<IActionResult> CheckDealerActive([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "Email is required." });

            var agent = await _context.DealerAgents
                .FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower());

            if (agent == null)
                return Ok(new { found = false, isActive = true }); // Not found = allow login (not a registered dealer agent)

            return Ok(new { found = true, isActive = agent.IsActive, agentCode = agent.AgentCode });
        }

        /// <summary>Get a specific dealer by ID</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });
            return Ok(agent);
        }

        /// <summary>Activate a dealer account — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            agent.IsActive = true;
            agent.Status = "Active";
            await _context.SaveChangesAsync();

            // Also unblock the user in Identity service
            await SyncDealerLoginStatus(agent.Email, block: false);

            _logger.LogInformation("Activated dealer agent: {AgentCode}", agent.AgentCode);
            return Ok(new { message = "Dealer activated.", agent });
        }

        /// <summary>Deactivate a dealer account — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            agent.IsActive = false;
            agent.Status = "Inactive";
            await _context.SaveChangesAsync();

            // Also block the user in Identity service to prevent login
            await SyncDealerLoginStatus(agent.Email, block: true);

            _logger.LogInformation("Deactivated dealer agent: {AgentCode}", agent.AgentCode);
            return Ok(new { message = "Dealer deactivated.", agent });
        }

        /// <summary>Delete a dealer account — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDealer(int id)
        {
            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            _context.DealerAgents.Remove(agent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted dealer agent: {AgentCode}", agent.AgentCode);
            return Ok(new { message = "Dealer deleted." });
        }

        /// <summary>Update a dealer agent's details — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDealer(int id, [FromBody] DealerAgent updated)
        {
            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            if (!string.IsNullOrWhiteSpace(updated.AgentName)) agent.AgentName = updated.AgentName;
            if (!string.IsNullOrWhiteSpace(updated.AgencyName)) agent.AgencyName = updated.AgencyName;
            if (!string.IsNullOrWhiteSpace(updated.Email)) agent.Email = updated.Email;
            if (!string.IsNullOrWhiteSpace(updated.Phone)) agent.Phone = updated.Phone;
            if (updated.CommissionRate > 0) agent.CommissionRate = updated.CommissionRate;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated dealer agent: {AgentCode}", agent.AgentCode);
            return Ok(new { message = "Dealer updated.", agent });
        }

        /// <summary>Update dealer commission rate — Admin only</summary>
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}/commission-rate")]
        public async Task<IActionResult> UpdateCommissionRate(int id, [FromQuery] decimal rate)
        {
            if (rate < 0 || rate > 100)
                return BadRequest(new { error = "Commission rate must be between 0 and 100 percent." });

            var agent = await _context.DealerAgents.FindAsync(id);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            agent.CommissionRate = rate;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated commission rate for {AgentCode} to {Rate}%", agent.AgentCode, rate);
            return Ok(new { message = $"Commission rate updated to {rate}%.", agent });
        }

        // ─── Agent-Customer Assignment Endpoints ─────────────────────────

        [HttpPost("{agentId}/customers")]
        public async Task<IActionResult> AssignCustomer(int agentId, [FromBody] AgentCustomer assignment)
        {
            var agent = await _context.DealerAgents.FindAsync(agentId);
            if (agent == null) return NotFound(new { error = "Dealer not found." });
            if (!agent.IsActive) return BadRequest(new { error = "Cannot assign customers to an inactive dealer." });
            if (string.IsNullOrWhiteSpace(assignment.CustomerName) || string.IsNullOrWhiteSpace(assignment.CustomerEmail))
                return BadRequest(new { error = "Customer name and email are required." });

            var existing = await _context.AgentCustomers
                .FirstOrDefaultAsync(ac => ac.AgentId == agentId && ac.CustomerId == assignment.CustomerId);
            if (existing != null)
                return Conflict(new { error = "This customer is already assigned to this agent." });

            var duplicateEmail = await _context.AgentCustomers.AnyAsync(ac =>
                ac.AgentId == agentId &&
                ac.CustomerEmail == assignment.CustomerEmail);
            if (duplicateEmail)
                return Conflict(new { error = "A customer with this email is already assigned to the selected agent." });

            assignment.AgentId = agentId;
            _context.AgentCustomers.Add(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned customer {CustomerId} to agent {AgentId}", assignment.CustomerId, agentId);
            return Ok(new { message = "Customer assigned to agent.", assignment });
        }

        [HttpGet("{agentId}/customers")]
        public async Task<IActionResult> GetAgentCustomers(int agentId)
        {
            var agent = await _context.DealerAgents.FindAsync(agentId);
            if (agent == null) return NotFound(new { error = "Dealer not found." });

            var customers = await _context.AgentCustomers
                .Where(ac => ac.AgentId == agentId && ac.IsActive)
                .OrderByDescending(ac => ac.AssignedDate)
                .ToListAsync();

            return Ok(new { agent, customers });
        }

        [HttpDelete("{agentId}/customers/{customerId}")]
        public async Task<IActionResult> RemoveCustomer(int agentId, int customerId)
        {
            var assignment = await _context.AgentCustomers
                .FirstOrDefaultAsync(ac => ac.AgentId == agentId && ac.CustomerId == customerId);
            if (assignment == null)
                return NotFound(new { error = "Customer assignment not found." });

            _context.AgentCustomers.Remove(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Permanently removed customer {CustomerId} from agent {AgentId}", customerId, agentId);
            return Ok(new { message = "Customer removed permanently from agent." });
        }

        // ─── Wallet Endpoints ──────────────────────────────────────────

        [HttpGet("{agentId}/wallet")]
        public async Task<IActionResult> GetWallet(int agentId)
        {
            if (!User.IsInRole("Admin"))
            {
                var email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
                var agentAuth = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentId == agentId);
                if (agentAuth == null || !string.Equals(agentAuth.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { error = "Access denied. You can only view your own wallet." });
                }
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.AgentId == agentId);
            if (wallet == null) return NotFound(new { error = "Wallet not found. Register the dealer first." });

            return Ok(new
            {
                walletId = wallet.WalletId,
                agentId = wallet.AgentId,
                balance = wallet.Balance,
                lastTransactionDate = wallet.LastTransactionDate,
                createdAt = wallet.CreatedAt
            });
        }

        [Authorize(Roles = "Admin,Dealer")]
        [HttpPost("{agentId}/wallet/credit")]
        public async Task<IActionResult> CreditWallet(int agentId, [FromBody] WalletOperationRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new { error = "Amount must be greater than zero." });

            if (!User.IsInRole("Admin"))
            {
                var email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
                var agentAuth = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentId == agentId);
                if (agentAuth == null || !string.Equals(agentAuth.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { error = "Access denied. You can only credit your own wallet." });
                }
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.AgentId == agentId);
            if (wallet == null) return NotFound(new { error = "Wallet not found. Register the dealer first." });

            wallet.Balance += request.Amount;
            wallet.LastTransactionDate = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = request.Amount,
                Type = "Credit",
                Description = request.Description ?? "Manual credit",
                BalanceAfter = wallet.Balance,
                CreatedAt = DateTime.UtcNow
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Credited {Amount} to wallet for agent {AgentId}. New balance: {Balance}",
                request.Amount, agentId, wallet.Balance);

            return Ok(new { message = $"Credited {request.Amount:C}.", balance = wallet.Balance, transaction });
        }

        [Authorize(Roles = "Admin,Dealer")]
        [HttpPost("{agentId}/wallet/debit")]
        public async Task<IActionResult> DebitWallet(int agentId, [FromBody] WalletOperationRequest request)
        {
            if (request.Amount <= 0)
                return BadRequest(new { error = "Amount must be greater than zero." });

            // Dealers can only debit their own wallet
            if (!User.IsInRole("Admin"))
            {
                var email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
                var agentAuth = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentId == agentId);
                if (agentAuth == null || !string.Equals(agentAuth.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { error = "Access denied. You can only debit your own wallet." });
                }
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.AgentId == agentId);
            if (wallet == null) return NotFound(new { error = "Wallet not found. Register the dealer first." });

            if (wallet.Balance < request.Amount)
                return BadRequest(new { error = $"Insufficient balance. Available: {wallet.Balance:C}, Requested: {request.Amount:C}" });

            wallet.Balance -= request.Amount;
            wallet.LastTransactionDate = DateTime.UtcNow;

            var transaction = new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = request.Amount,
                Type = "Debit",
                Description = request.Description ?? "Manual debit",
                BalanceAfter = wallet.Balance,
                CreatedAt = DateTime.UtcNow
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Debited {Amount} from wallet for agent {AgentId}. New balance: {Balance}",
                request.Amount, agentId, wallet.Balance);

            // Auto-record commission for bulk booking debits
            if (!string.IsNullOrEmpty(request.Description) && request.Description.Contains("Bulk Booking PNR:"))
            {
                try
                {
                    var pnr = request.Description.Replace("Bulk Booking PNR: ", "").Trim();
                    var agent = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentId == agentId);
                    if (agent != null)
                    {
                        // Check for duplicate
                        var exists = await _context.Commissions.AnyAsync(c => c.PNR == pnr && c.AgentId == agentId);
                        if (!exists)
                        {
                            var rate = agent.CommissionRate > 0 ? (agent.CommissionRate / 100m) : 0.05m;
                            var commission = new CommissionRecord
                            {
                                AgentId = agentId,
                                AgentCode = agent.AgentCode,
                                BookingAmount = request.Amount,
                                CommissionAmount = request.Amount * rate,
                                PNR = pnr,
                                Status = "Paid"
                            };
                            _context.Commissions.Add(commission);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Auto-recorded commission for PNR {PNR}: {Amount} ({Rate}%)",
                                pnr, commission.CommissionAmount, rate * 100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-record commission for agent {AgentId}", agentId);
                }
            }

            return Ok(new { message = $"Debited {request.Amount:C}.", balance = wallet.Balance, transaction });
        }

        [HttpGet("{agentId}/wallet/history")]
        public async Task<IActionResult> GetWalletHistory(int agentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (!User.IsInRole("Admin"))
            {
                var email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email || c.Type == "email")?.Value;
                var agentAuth = await _context.DealerAgents.FirstOrDefaultAsync(a => a.AgentId == agentId);
                if (agentAuth == null || !string.Equals(agentAuth.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(403, new { error = "Access denied. You can only view your own wallet history." });
                }
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.AgentId == agentId);
            if (wallet == null) return NotFound(new { error = "Wallet not found." });

            var totalCount = await _context.WalletTransactions.CountAsync(t => t.WalletId == wallet.WalletId);
            var transactions = await _context.WalletTransactions
                .Where(t => t.WalletId == wallet.WalletId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                balance = wallet.Balance,
                totalTransactions = totalCount,
                page,
                pageSize,
                transactions
            });
        }

        // ─── Cross-Service: Sync Dealer Login Status ──────────────────

        /// <summary>
        /// Finds the user in Identity service by email and blocks/unblocks them.
        /// This ensures deactivated dealers cannot login.
        /// </summary>
        private async Task SyncDealerLoginStatus(string? dealerEmail, bool block)
        {
            if (string.IsNullOrWhiteSpace(dealerEmail)) return;

            try
            {
                var httpClient = _httpClientFactory.CreateClient("IdentityService");

                // Forward the admin JWT token from the current request
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
                }

                // Fetch all users to find the one with matching email
                var usersResponse = await httpClient.GetAsync("api/Auth/users");
                if (!usersResponse.IsSuccessStatusCode) return;

                var usersJson = await usersResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<JsonElement>(usersJson);

                if (users.ValueKind != JsonValueKind.Array) return;

                foreach (var user in users.EnumerateArray())
                {
                    var email = user.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                    if (string.Equals(email, dealerEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        var userId = user.TryGetProperty("userId", out var idProp) ? idProp.GetInt32() : 0;
                        if (userId > 0)
                        {
                            var endpoint = block
                                ? $"api/Auth/users/{userId}/block"
                                : $"api/Auth/users/{userId}/unblock";
                            await httpClient.PutAsync(endpoint, null);
                            _logger.LogInformation("Synced Identity login status: {Action} user {Email} (ID: {UserId})",
                                block ? "Blocked" : "Unblocked", dealerEmail, userId);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync dealer login status in Identity service for {Email}", dealerEmail);
            }
        }

        // ─── Request DTOs ──────────────────────────────────────────────

        public class WalletOperationRequest
        {
            public decimal Amount { get; set; }
            public string? Description { get; set; }
        }
    }
}
