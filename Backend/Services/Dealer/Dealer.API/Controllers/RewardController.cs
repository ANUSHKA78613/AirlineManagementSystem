using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dealer.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RewardController : ControllerBase
    {
        private readonly DealerDbContext _context;
        private readonly ILogger<RewardController> _logger;

        public RewardController(DealerDbContext context, ILogger<RewardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("enroll")]
        public async Task<IActionResult> Enroll([FromQuery] int userId)
        {
            var existing = await _context.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
            if (existing != null)
                return Conflict(new { error = "User is already enrolled in the rewards program." });

            var account = new RewardAccount { UserId = userId };
            _context.RewardAccounts.Add(account);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} enrolled in loyalty program", userId);
            return Ok(new { message = "Enrolled in rewards program.", account });
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var account = await _context.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
            if (account == null)
            {
                // Return a default placeholder so the frontend doesn't get a 404
                return Ok(new { userId = userId, totalPoints = 0m, membershipTier = "Silver", enrolled = false });
            }
            return Ok(account);
        }

        [HttpPut("{userId}/add-points")]
        public async Task<IActionResult> AddPoints(int userId, [FromQuery] decimal points)
        {
            if (points <= 0) return BadRequest(new { error = "Points must be a positive value." });

            var account = await _context.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
            if (account == null) 
            {
                // Auto-enroll the user
                account = new RewardAccount { UserId = userId, TotalPoints = 0, MembershipTier = "Silver" };
                _context.RewardAccounts.Add(account);
                _logger.LogInformation("Auto-enrolled User {UserId} in loyalty program during AddPoints", userId);
            }

            account.TotalPoints += points;
            UpdateMembershipTier(account);

            var txn = new RewardTransaction
            {
                RewardId = account.RewardId, Points = points, Type = "Earn",
                Description = "Manual credit", CreatedAt = DateTime.UtcNow
            };
            _context.RewardTransactions.Add(txn);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added {Points} points to UserId {UserId}", points, userId);
            return Ok(account);
        }

        [HttpPut("{userId}/redeem")]
        public async Task<IActionResult> RedeemPoints(int userId, [FromQuery] decimal points)
        {
            if (points <= 0) return BadRequest(new { error = "Redemption amount must be positive." });

            var account = await _context.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
            if (account == null) return NotFound(new { error = "User is not enrolled." });
            if (account.TotalPoints < points)
                return BadRequest(new { error = $"Insufficient points. Available: {account.TotalPoints}" });

            account.TotalPoints -= points;
            UpdateMembershipTier(account);

            var txn = new RewardTransaction
            {
                RewardId = account.RewardId, Points = points, Type = "Redeem",
                Description = "Points redeemed by user", CreatedAt = DateTime.UtcNow
            };
            _context.RewardTransactions.Add(txn);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Successfully redeemed {points} points.", account });
        }

        [HttpGet("{userId}/transactions")]
        public async Task<IActionResult> GetTransactions(int userId)
        {
            var account = await _context.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId);
            if (account == null) return NotFound(new { error = "User is not enrolled." });

            var transactions = await _context.RewardTransactions
                .Where(t => t.RewardId == account.RewardId)
                .OrderByDescending(t => t.CreatedAt).ToListAsync();

            return Ok(new { account, transactions });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10)
        {
            var leaders = await _context.RewardAccounts
                .OrderByDescending(r => r.TotalPoints).Take(top).ToListAsync();
            return Ok(leaders);
        }

        private static void UpdateMembershipTier(RewardAccount account)
        {
            if (account.TotalPoints >= 10000) account.MembershipTier = "Diamond";
            else if (account.TotalPoints >= 5000) account.MembershipTier = "Platinum";
            else if (account.TotalPoints >= 2000) account.MembershipTier = "Gold";
            else account.MembershipTier = "Silver";
        }
    }
}
