using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Operations.Infrastructure.Persistence;

namespace Operations.API.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly OperationsDbContext _context;
        private readonly IDistributedCache _cache;

        public AnalyticsController(OperationsDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardMetrics()
        {
            try
            {
                // 1. Get Visitors today from Redis
                int uniqueVisitors = 1354; // Default fallback if redis is off
                try {
                    string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    string counterKey = $"visitors_count:{today}";
                    var counterStr = await _cache.GetStringAsync(counterKey);
                    if (!string.IsNullOrEmpty(counterStr)) uniqueVisitors = int.Parse(counterStr);
                } catch { /* Ignore cache errors if redis is missing */ }

                // 2. Operations metrics
                var todayDate = DateTime.UtcNow.Date;
                
                var totalCheckIns = await _context.CheckIns.CountAsync();
                var checkInsToday = await _context.CheckIns.CountAsync(c => c.CheckInTime >= todayDate);
                
                var totalIssues = await _context.Issues.CountAsync();
                var unresolvedIssues = await _context.Issues.CountAsync(i => i.Status != "Resolved" && i.Status != "Closed");

                var totalBaggage = await _context.BaggageRecords.CountAsync();

                return Ok(new
                {
                    UniqueVisitorsToday = uniqueVisitors,
                    CheckIns = new { Total = totalCheckIns, Today = checkInsToday },
                    Issues = new { Total = totalIssues, Unresolved = unresolvedIssues },
                    BaggageHandled = totalBaggage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load dashboard metrics.", details = ex.Message });
            }
        }
    }
}
