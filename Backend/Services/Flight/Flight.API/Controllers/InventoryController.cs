using Flight.Domain.Entities;
using Flight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flight.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly FlightDbContext _context;

        public InventoryController(FlightDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> AddItem([FromBody] InventoryItem item)
        {
            _context.InventoryItems.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Item added.", item });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _context.InventoryItems.ToListAsync());
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuantity(int id, [FromQuery] int quantity)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return NotFound(new { error = "Item not found." });

            item.Quantity = quantity;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Quantity updated.", item });
        }

        // ─── Seat Map (Class-Based) ───────────────────────────────────

        [AllowAnonymous]
        [HttpGet("{flightId:int}/seats")]
        public async Task<IActionResult> GetSeatMap(int flightId, [FromQuery] string? seatClass = null)
        {
            var seats = await EnsureSeatMapExists(flightId);

            if (!string.IsNullOrWhiteSpace(seatClass))
                seats = seats.Where(s => s.SeatClass == seatClass).ToList();

            return Ok(seats
                .OrderBy(s => s.SeatClass == "First" ? 0 : s.SeatClass == "Business" ? 1 : 2)
                .ThenBy(s => ParseRow(s.SeatNo))
                .ThenBy(s => s.SeatNo)
                .Select(s => new
                {
                    seatNo = s.SeatNo,
                    isAvailable = s.IsAvailable,
                    isBlocked = IsBlockedSeat(s.SeatNo),
                    price = s.Price,
                    seatClass = s.SeatClass,
                    category = s.Category
                }));
        }

        [AllowAnonymous]
        [HttpGet("{flightId:int}/seat-layout")]
        public async Task<IActionResult> GetSeatLayout(int flightId)
        {
            // Return the seat configuration for the UI to render different class layouts
            var configs = await _context.SeatConfigurations
                .Where(c => c.FlightId == flightId)
                .ToListAsync();

            if (!configs.Any())
            {
                // Return default configurations
                configs = GetDefaultSeatConfigs(flightId);
            }

            return Ok(configs.Select(c => new
            {
                c.Id,
                c.FlightId,
                seatClass = c.Class,
                c.TotalCapacity,
                c.TotalRows,
                c.ColumnsLayout,
                c.AisleMarkup,
                c.WindowMarkup,
                c.MiddleMarkup
            }));
        }

        [HttpPost("{flightId:int}/reserve")]
        public async Task<IActionResult> ReserveSeat(int flightId, [FromBody] SeatActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SeatNo))
                return BadRequest(new { error = "Seat number is required." });

            var seats = await EnsureSeatMapExists(flightId);
            var seat = seats.FirstOrDefault(s => s.SeatNo.Equals(request.SeatNo, StringComparison.OrdinalIgnoreCase));

            if (seat == null) return NotFound(new { error = "Seat not found." });
            if (IsBlockedSeat(seat.SeatNo)) return BadRequest(new { error = "Seat blocked by crew or emergency-exit rules." });
            if (!seat.IsAvailable) return Conflict(new { error = "Seat already taken. Please choose another seat." });

            seat.IsAvailable = false;
            await _context.SaveChangesAsync();

            return Ok(new { ok = true, seatNo = seat.SeatNo });
        }

        [HttpPost("{flightId:int}/release")]
        public async Task<IActionResult> ReleaseSeat(int flightId, [FromBody] SeatActionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SeatNo))
                return BadRequest(new { error = "Seat number is required." });

            var seats = await EnsureSeatMapExists(flightId);
            var seat = seats.FirstOrDefault(s => s.SeatNo.Equals(request.SeatNo, StringComparison.OrdinalIgnoreCase));

            if (seat == null) return NotFound(new { error = "Seat not found." });

            if (!IsBlockedSeat(seat.SeatNo))
            {
                seat.IsAvailable = true;
                await _context.SaveChangesAsync();
            }

            return Ok(new { ok = true, seatNo = seat.SeatNo });
        }

        // ─── Seat Map Generation ──────────────────────────────────────

        private async Task<List<Seat>> EnsureSeatMapExists(int flightId)
        {
            var existing = await _context.Seats
                .Where(s => s.FlightId == flightId)
                .OrderBy(s => s.SeatNo)
                .ToListAsync();

            if (existing.Count > 0)
            {
                // Sync prices of available seats with latest FlightPricings (admin may have updated prices or WindowSeatCharge)
                var currentPricings = await _context.FlightPricings.Where(p => p.FlightId == flightId && p.IsActive).ToListAsync();
                if (currentPricings.Any())
                {
                    bool pricesUpdated = false;
                    var localConfigs = await _context.SeatConfigurations.Where(c => c.FlightId == flightId).ToListAsync();

                    foreach (var seat in existing.Where(s => s.IsAvailable))
                    {
                        var pricing = currentPricings.FirstOrDefault(p => p.Class == seat.SeatClass);
                        var config = localConfigs.FirstOrDefault(c => c.Class == seat.SeatClass);
                        
                        if (pricing != null)
                        {
                            var baseFinalPrice = pricing.BasePrice * pricing.Multiplier;
                            var seatMarkup = seat.Category switch
                            {
                                "Window" => pricing.WindowSeatCharge > 0m ? pricing.WindowSeatCharge : (config?.WindowMarkup ?? 0m),
                                "Aisle" => config?.AisleMarkup ?? 0m,
                                "Middle" => config?.MiddleMarkup ?? 0m,
                                _ => 0m
                            };
                            var calculatedPrice = baseFinalPrice + seatMarkup;
                            
                            if (seat.Price != calculatedPrice)
                            {
                                seat.Price = calculatedPrice;
                                pricesUpdated = true;
                            }
                        }
                    }
                    if (pricesUpdated) await _context.SaveChangesAsync();
                }
                
                return existing;
            }

            // Check for admin-defined seat configurations
            var configs = await _context.SeatConfigurations
                .Where(c => c.FlightId == flightId)
                .ToListAsync();

            if (!configs.Any())
            {
                configs = GetDefaultSeatConfigs(flightId);
            }

            // Get pricing rules for this flight
            var pricings = await _context.FlightPricings
                .Where(p => p.FlightId == flightId && p.IsActive)
                .ToListAsync();

            var created = new List<Seat>();

            var globalRowCounter = 1;
            foreach (var config in configs)
            {
                var pricing = pricings.FirstOrDefault(p => p.Class == config.Class);
                var baseFinalPrice = pricing != null ? pricing.BasePrice * pricing.Multiplier : 0m;

                var layoutParts = config.ColumnsLayout.Split('-').Select(int.Parse).ToArray();
                var totalColumns = layoutParts.Sum();
                var columnLabels = GenerateColumnLabels(totalColumns);

                for (var row = 1; row <= config.TotalRows; row++)
                {
                    var displayRow = globalRowCounter++;
                    for (var colIdx = 0; colIdx < totalColumns; colIdx++)
                    {
                        var seatNo = $"{config.Class[0]}{displayRow}{columnLabels[colIdx]}"; // e.g. F1A, B3C, E10A
                        var category = ClassifySeatCategory(colIdx, layoutParts);
                        var seatMarkup = category switch
                        {
                            "Window" => (pricing != null && pricing.WindowSeatCharge > 0m) ? pricing.WindowSeatCharge : config.WindowMarkup,
                            "Aisle" => config.AisleMarkup,
                            "Middle" => config.MiddleMarkup,
                            _ => 0m
                        };

                        created.Add(new Seat
                        {
                            FlightId = flightId,
                            SeatNo = seatNo,
                            IsAvailable = true,
                            Price = baseFinalPrice + seatMarkup,
                            SeatClass = config.Class,
                            Category = category
                        });
                    }
                }
            }

            if (created.Any())
            {
                _context.Seats.AddRange(created);
                await _context.SaveChangesAsync();
            }

            return created;
        }

        private static List<SeatConfiguration> GetDefaultSeatConfigs(int flightId)
        {
            return new List<SeatConfiguration>
            {
                new() { FlightId = flightId, Class = "First", TotalCapacity = 8, TotalRows = 2, ColumnsLayout = "1-2-1", AisleMarkup = 0, WindowMarkup = 200, MiddleMarkup = 0 },
                new() { FlightId = flightId, Class = "Business", TotalCapacity = 24, TotalRows = 6, ColumnsLayout = "2-2", AisleMarkup = 100, WindowMarkup = 150, MiddleMarkup = 0 },
                new() { FlightId = flightId, Class = "Economy", TotalCapacity = 120, TotalRows = 20, ColumnsLayout = "3-3", AisleMarkup = 50, WindowMarkup = 100, MiddleMarkup = 0 }
            };
        }

        private static string[] GenerateColumnLabels(int totalColumns)
        {
            var labels = new string[totalColumns];
            for (int i = 0; i < totalColumns; i++)
                labels[i] = ((char)('A' + i)).ToString();
            return labels;
        }

        private static string ClassifySeatCategory(int colIdx, int[] layoutParts)
        {
            // Determine which group the column falls into
            int cumulative = 0;
            for (int g = 0; g < layoutParts.Length; g++)
            {
                int groupStart = cumulative;
                int groupEnd = cumulative + layoutParts[g] - 1;
                cumulative += layoutParts[g];

                if (colIdx >= groupStart && colIdx <= groupEnd)
                {
                    // First and last columns of the entire row are Window
                    if (colIdx == 0 || colIdx == layoutParts.Sum() - 1)
                        return "Window";

                    // First and last of each group (if not already Window) are Aisle
                    if (colIdx == groupStart || colIdx == groupEnd)
                        return "Aisle";

                    return "Middle";
                }
            }
            return "Standard";
        }

        private static bool IsBlockedSeat(string seatNo)
        {
            // Emergency exit rows or crew-blocked seats can be configured here
            return false;
        }

        private static int ParseRow(string seatNo)
        {
            var numeric = new string(seatNo.Where(char.IsDigit).ToArray());
            return int.TryParse(numeric, out var row) ? row : 0;
        }

        public class SeatActionRequest
        {
            public string SeatNo { get; set; } = string.Empty;
        }
    }
}
