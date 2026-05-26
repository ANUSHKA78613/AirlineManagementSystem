using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Booking.Application.DTOs;
using Booking.Application.CQRS.Commands;
using Booking.Application.CQRS.Queries;
using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shared.Middleware.Exceptions;

namespace Booking.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly BookingDbContext _context;
        private readonly EventBus.Abstractions.IEventBus _eventBus;

        public BookingController(IMediator mediator, BookingDbContext context, EventBus.Abstractions.IEventBus eventBus)
        {
            _mediator = mediator;
            _context = context;
            _eventBus = eventBus;
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdClaim) && int.TryParse(userIdClaim, out var tokenUserId))
            {
                if (dto.UserId != tokenUserId && !User.IsInRole("Admin") && !User.IsInRole("Staff") && !User.IsInRole("Dealer"))
                {
                    throw new ForbiddenException("You can only book tickets for your own account.");
                }
            }

            ValidatePassengers(dto.Passengers);

            var result = await _mediator.Send(new CreateBookingCommand(dto));
            return Ok(result);
        }

        [Authorize(Roles = "Dealer,Admin")]
        [HttpPost("bulk")]
        public async Task<IActionResult> BulkCreateBookings([FromBody] List<CreateBookingDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                throw new BadRequestException("At least one booking is required.");
            if (dtos.Count > 20)
                throw new BadRequestException("Maximum 20 bookings per bulk request.");

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                var results = new List<object>();
                var errors = new List<object>();

                using var transaction = await _context.Database.BeginTransactionAsync();

                for (int i = 0; i < dtos.Count; i++)
                {
                    var dto = dtos[i];
                    try
                    {
                        ValidatePassengers(dto.Passengers);
                        var result = await _mediator.Send(new CreateBookingCommand(dto));
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        // Bulk operations collect per-item errors instead of failing entirely
                        errors.Add(new { index = i, error = ex.Message });
                    }
                }

                if (errors.Count > 0 && results.Count == 0)
                {
                    await transaction.RollbackAsync();
                    throw new BadRequestException("All bookings failed. Check individual errors.");
                }

                await transaction.CommitAsync();
                return Ok(new
                {
                    message = $"{results.Count} booking(s) created successfully.",
                    created = results.Count,
                    failed = errors.Count,
                    bookings = results,
                    errors = errors.Count > 0 ? errors : null
                });
            });
        }

        [HttpGet("{pnr}")]
        public async Task<IActionResult> GetBooking(string pnr)
        {
            pnr = pnr.Trim();
            var result = await _mediator.Send(new GetBookingQuery(pnr));
            return Ok(result);
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public async Task<IActionResult> GetAllBookings()
        {
            var bookings = await _context.Bookings.AsNoTracking()
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            var pnrs = bookings.Select(b => b.PNR).ToList();
            var passengers = await _context.Passengers
                .AsNoTracking()
                .Where(p => pnrs.Contains(p.PNR))
                .OrderBy(p => p.PassengerId)
                .ToListAsync();

            var result = bookings.Select(booking => new
            {
                pnr = booking.PNR,
                userId = booking.UserId,
                flightId = booking.FlightId,
                bookingDate = booking.BookingDate,
                status = booking.Status,
                totalAmount = booking.TotalAmount,
                passengers = passengers
                    .Where(p => p.PNR == booking.PNR)
                    .Select(p => new
                    {
                        passengerId = p.PassengerId,
                        name = p.Name,
                        age = p.Age,
                        gender = p.Gender,
                        seatNo = p.SeatNo
                    })
                    .ToList()
            });

            return Ok(result);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyBookings([FromQuery] int userId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdClaim) && int.TryParse(userIdClaim, out var tokenUserId))
            {
                if (userId != tokenUserId && !User.IsInRole("Admin") && !User.IsInRole("Staff"))
                {
                    throw new ForbiddenException("You can only view your own bookings.");
                }
            }

            var bookings = await _context.Bookings.AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();

            var pnrs = bookings.Select(b => b.PNR).ToList();
            var passengers = await _context.Passengers
                .AsNoTracking()
                .Where(p => pnrs.Contains(p.PNR))
                .OrderBy(p => p.PassengerId)
                .ToListAsync();

            var result = bookings.Select(booking => new
            {
                pnr = booking.PNR,
                userId = booking.UserId,
                flightId = booking.FlightId,
                bookingDate = booking.BookingDate,
                status = booking.Status,
                totalAmount = booking.TotalAmount,
                passengers = passengers
                    .Where(p => p.PNR == booking.PNR)
                    .Select(p => new
                    {
                        passengerId = p.PassengerId,
                        name = p.Name,
                        age = p.Age,
                        gender = p.Gender,
                        seatNo = p.SeatNo
                    })
                    .ToList()
            });

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("flight/{flightId:int}/booked-seats")]
        public async Task<IActionResult> GetBookedSeats(int flightId)
        {
            // Return seats that are locked by Confirmed or recent Pending bookings (<15 min)
            var staleCutoff = DateTime.UtcNow.AddMinutes(-15);
            var bookedSeats = await _context.Bookings
                .Join(_context.Passengers, b => b.PNR, p => p.PNR, (b, p) => new { b.FlightId, p.SeatNo, b.Status, b.BookingDate })
                .Where(x => x.FlightId == flightId
                    && !string.IsNullOrEmpty(x.SeatNo)
                    && x.Status != "Cancelled"
                    && (x.Status == "Confirmed" || x.BookingDate >= staleCutoff))
                .Select(x => x.SeatNo)
                .Distinct()
                .ToListAsync();

            return Ok(bookedSeats);
        }

        [HttpPut("{pnr}/cancel")]
        public async Task<IActionResult> CancelBooking(string pnr, [FromBody] System.Collections.Generic.List<int>? passengerIds = null)
        {
            var result = await _mediator.Send(new CancelBookingCommand(pnr, passengerIds));
            return Ok(new { message = result.Message, refundAmount = result.RefundAmount });
        }

        [HttpPost("{pnr}/cancel")]
        public async Task<IActionResult> CancelBookingPost(string pnr, [FromBody] CancelBookingRequest? request = null)
        {
            return await CancelBooking(pnr, request?.PassengerIds);
        }

        [HttpPost("{pnr}/reschedule")]
        public async Task<IActionResult> RescheduleBooking(string pnr, [FromBody] RescheduleBookingRequest request)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IActionResult>(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.PNR == pnr);
                if (booking == null) throw new NotFoundException("Booking", pnr);
                if (booking.Status == "Cancelled") throw new BadRequestException("Cancelled bookings cannot be rescheduled.");
                if (request.NewFlightId <= 0) throw new BadRequestException("A valid new flight ID is required.");

                int oldFlightId = booking.FlightId;
                var passengers = await _context.Passengers.Where(p => p.PNR == pnr).ToListAsync();
                var oldSeats = passengers.Select(p => p.SeatNo).Where(s => !string.IsNullOrEmpty(s)).ToList();

                booking.FlightId = request.NewFlightId;
                if (request.NewTotalAmount.HasValue)
                {
                    booking.TotalAmount = request.NewTotalAmount.Value;
                }
                booking.Status = "Confirmed";
                
                var newSeats = new List<string>();
                if (request.Passengers != null && request.Passengers.Count == passengers.Count)
                {
                    for (int i = 0; i < passengers.Count; i++)
                    {
                        passengers[i].SeatNo = request.Passengers[i].SeatNo;
                        if (!string.IsNullOrEmpty(request.Passengers[i].SeatNo)) {
                            newSeats.Add(request.Passengers[i].SeatNo!);
                        }
                    }
                }
                else
                {
                    // Default to TBD if no seat selected
                    foreach (var p in passengers) p.SeatNo = "TBD";
                }

                await _context.SaveChangesAsync();

                var eventBus = HttpContext.RequestServices.GetService<EventBus.Abstractions.IEventBus>();
                if (eventBus != null)
                {
                    // 1. Release Old Seats
                    eventBus.Publish(new EventBus.Events.SeatInventoryChangedEvent
                    {
                        FlightId = oldFlightId,
                        SeatsBooked = 0,
                        SeatsReleased = passengers.Count,
                        ReleasedSeatNumbers = oldSeats
                    });

                    // 2. Book New Seats
                    eventBus.Publish(new EventBus.Events.SeatInventoryChangedEvent
                    {
                        FlightId = booking.FlightId,
                        SeatsBooked = passengers.Count,
                        SeatsReleased = 0,
                        BookedSeatNumbers = newSeats
                    });
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    ok = true,
                    pnr = booking.PNR,
                    flightId = booking.FlightId,
                    totalAmount = booking.TotalAmount,
                    status = booking.Status,
                    message = "Booking rescheduled successfully."
                });
            });
        }

        [HttpPost("{pnr}/confirm")]
        public async Task<IActionResult> ConfirmBooking(string pnr)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.PNR == pnr);
            if (booking == null) throw new NotFoundException("Booking", pnr);
            if (booking.Status == "Cancelled") throw new BadRequestException("Cancelled booking cannot be confirmed.");

            booking.Status = "Confirmed";
            
            var passengersCount = await _context.Passengers.CountAsync(p => p.PNR == pnr);

            await _context.SaveChangesAsync();

            _eventBus.Publish(new EventBus.Events.BookingStatusChangedEvent
            {
                PNR = booking.PNR,
                UserId = booking.UserId,
                FlightId = booking.FlightId,
                Status = booking.Status,
                TotalAmount = booking.TotalAmount,
                PassengerCount = passengersCount,
                UserEmail = booking.UserEmail
            });

            return Ok(new { ok = true, pnr = booking.PNR, status = booking.Status, message = "Booking confirmed after payment." });
        }

        // ─── Helpers ───────────────────────────────────────────────────

        private static void ValidatePassengers(List<PassengerDto>? passengers)
        {
            if (passengers == null || passengers.Count == 0)
                throw new BadRequestException("At least one passenger is required.");

            bool hasAdult = false;
            foreach (var p in passengers)
            {
                if (p.Age >= 18) hasAdult = true;
            }

            if (!hasAdult && passengers.Any(p => p.Age < 12))
            {
                throw new BadRequestException("At least one adult passenger (age 18+) must accompany children/infants.");
            }

            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                if (string.IsNullOrWhiteSpace(p.Name))
                    throw new BadRequestException($"Passenger {i + 1}: Name is required.");
                if (p.Age <= 0)
                    throw new BadRequestException($"Passenger {i + 1}: Age must be greater than 0.");
                if (string.IsNullOrWhiteSpace(p.Gender))
                    throw new BadRequestException($"Passenger {i + 1}: Gender is required.");
            }
        }

        public class CancelBookingRequest
        {
            public List<int>? PassengerIds { get; set; }
        }

        public class RescheduleBookingRequest
        {
            public int NewFlightId { get; set; }
            public decimal? NewTotalAmount { get; set; }
            public List<PassengerDto>? Passengers { get; set; }
        }
    }
}
