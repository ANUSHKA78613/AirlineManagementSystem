using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dealer.Infrastructure.Persistence;
using Dealer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dealer.API.Controllers
{
    [Authorize(Roles = "Admin,Dealer")]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly DealerDbContext _context;

        public AnalyticsController(DealerDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Record([FromBody] AnalyticsRecord record)
        {
            _context.AnalyticsRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Analytics recorded.", record });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _context.AnalyticsRecords.ToListAsync());

        [HttpGet("by-type/{eventType}")]
        public async Task<IActionResult> GetByType(string eventType) =>
            Ok(await _context.AnalyticsRecords.Where(a => a.EventType == eventType).ToListAsync());

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var total = await _context.AnalyticsRecords.CountAsync();
            var totalAmount = await _context.AnalyticsRecords.SumAsync(a => a.Amount);
            var byType = await _context.AnalyticsRecords
                .GroupBy(a => a.EventType)
                .Select(g => new { EventType = g.Key, Count = g.Count(), Total = g.Sum(x => x.Amount) })
                .ToListAsync();
            return Ok(new { totalRecords = total, totalAmount, breakdown = byType });
        }
    }
}
