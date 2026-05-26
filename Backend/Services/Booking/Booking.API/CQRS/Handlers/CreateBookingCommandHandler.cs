using MediatR;
using Booking.Application.CQRS.Commands;
using Booking.Infrastructure.Persistence;
using Booking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EventBus.Abstractions;
using EventBus.Events;
using Shared.EmailService;
using Shared.Middleware.Exceptions;

namespace Booking.API.CQRS.Handlers
{
    public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, object>
    {
        private readonly BookingDbContext _context;
        private readonly IEventBus _eventBus;
        private readonly IEmailService _emailService;

        public CreateBookingCommandHandler(BookingDbContext context, IEventBus eventBus, IEmailService emailService)
        {
            _context = context;
            _eventBus = eventBus;
            _emailService = emailService;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> _flightLocks = new();

        public async Task<object> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            if (dto.Passengers == null || !dto.Passengers.Any())
                throw new BadRequestException("At least one passenger is required.");

            // Edge Case: Validate total amount against tampered frontend price payloads
            if (dto.TotalAmount <= 0)
                throw new BadRequestException("Total amount must be greater than zero.");

            var flightLock = _flightLocks.GetOrAdd(dto.FlightId, _ => new SemaphoreSlim(1, 1));
            await flightLock.WaitAsync(cancellationToken);

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

                    try
                    {
                        // Prevent duplicate bookings: same user cannot book the same flight twice
                        var hasDuplicate = await _context.Bookings.AnyAsync(
                            b => b.UserId == dto.UserId && b.FlightId == dto.FlightId && b.Status != "Cancelled",
                            cancellationToken);

                        if (hasDuplicate)
                            throw new ConflictException("You already have an active booking on this flight. Please cancel it before rebooking.");

                        var requestedSeats = dto.Passengers.Select(p => p.SeatNo).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

                        if (requestedSeats.Any())
                        {
                            // Auto-cancel stale Pending bookings (> 15 min old) that lock these seats
                            var staleCutoff = DateTime.UtcNow.AddMinutes(-15);
                            var staleBookings = await _context.Bookings
                                .Join(_context.Passengers, b => b.PNR, p => p.PNR, (b, p) => new { Booking = b, Passenger = p })
                                .Where(x => x.Booking.FlightId == dto.FlightId 
                                    && requestedSeats.Contains(x.Passenger.SeatNo) 
                                    && x.Booking.Status == "Pending"
                                    && x.Booking.BookingDate < staleCutoff)
                                .Select(x => x.Booking)
                                .Distinct()
                                .ToListAsync(cancellationToken);

                            foreach (var stale in staleBookings)
                            {
                                stale.Status = "Cancelled";
                                Console.WriteLine($"[CreateBooking] Auto-cancelled stale Pending booking {stale.PNR} (created {stale.BookingDate:u})");
                            }
                            if (staleBookings.Any()) await _context.SaveChangesAsync(cancellationToken);

                            // Now check for real conflicts: Confirmed bookings or recent Pending (<15 min)
                            var existingSeats = await _context.Bookings
                                .Join(_context.Passengers, b => b.PNR, p => p.PNR, (b, p) => new { b.FlightId, p.SeatNo, b.Status, b.BookingDate })
                                .Where(x => x.FlightId == dto.FlightId 
                                    && requestedSeats.Contains(x.SeatNo) 
                                    && x.Status != "Cancelled"
                                    && (x.Status == "Confirmed" || x.BookingDate >= staleCutoff))
                                .Select(x => x.SeatNo)
                                .ToListAsync(cancellationToken);

                            if (existingSeats.Any())
                            {
                                throw new ConflictException($"Seat(s) {string.Join(", ", existingSeats)} are currently locked by another booking process or already sold.");
                            }
                        }

                        var booking = new Booking.Domain.Entities.Booking
                        {
                            UserId = dto.UserId,
                            FlightId = dto.FlightId,
                            TotalAmount = dto.TotalAmount,
                            Status = "Pending",
                            UserEmail = dto.Email,
                            UserName = dto.UserName ?? dto.Passengers.FirstOrDefault()?.Name
                        };

                        _context.Bookings.Add(booking);

                        var passengers = new List<Passenger>();
                        foreach (var p in dto.Passengers)
                        {
                            var passenger = new Passenger
                            {
                                PNR = booking.PNR,
                                Name = p.Name,
                                Age = p.Age,
                                Gender = p.Gender,
                                SeatNo = p.SeatNo,
                                SeatClass = p.SeatClass ?? "Economy"
                            };
                            _context.Passengers.Add(passenger);
                            passengers.Add(passenger);
                        }

                        await _context.SaveChangesAsync(cancellationToken);

                        // Saga Step 1: Publish BookingCreatedEvent to trigger payment
                        _eventBus.Publish(new BookingCreatedEvent
                        {
                            PNR = booking.PNR,
                            UserId = booking.UserId,
                            FlightId = booking.FlightId,
                            TotalAmount = booking.TotalAmount
                        });

                        // Eventual Consistency: Publish events for read-model projections
                        _eventBus.Publish(new BookingStatusChangedEvent
                        {
                            PNR = booking.PNR,
                            UserId = booking.UserId,
                            FlightId = booking.FlightId,
                            Status = booking.Status,
                            TotalAmount = booking.TotalAmount,
                            PassengerCount = passengers.Count,
                            FlightNumber = $"AG-{booking.FlightId:D4}",
                            Source = dto.Source,
                            Destination = dto.Destination,
                            DepartureTime = dto.DepartureTime,
                            UserEmail = booking.UserEmail
                        });

                        _eventBus.Publish(new SeatInventoryChangedEvent
                        {
                            FlightId = booking.FlightId,
                            SeatsBooked = passengers.Count,
                            SeatsReleased = 0,
                            BookedSeatNumbers = passengers.Select(p => p.SeatNo).Where(s => !string.IsNullOrEmpty(s)).ToList()
                        });

                        Console.WriteLine($"[CreateBookingCommand] Received CouponCode: '{dto.CouponCode}'");
                        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
                        {
                            _eventBus.Publish(new CouponUsedEvent
                            {
                                CouponCode = dto.CouponCode,
                                PNR = booking.PNR,
                                UserId = booking.UserId
                            });
                        }

                        await transaction.CommitAsync(cancellationToken);

                        // Send booking confirmation email (fire-and-forget, won't break booking)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Try to get user email — if we have it in the request context
                                var userEmail = dto.Email;
                                var userName = dto.UserName ?? passengers.FirstOrDefault()?.Name ?? "Traveller";

                                if (!string.IsNullOrWhiteSpace(userEmail))
                                {
                                    var emailModel = new BookingEmailModel
                                    {
                                        PNR = booking.PNR,
                                        FlightNumber = $"AG-{booking.FlightId:D4}",
                                        Source = dto.Source ?? "DEP",
                                        Destination = dto.Destination ?? "ARR",
                                        DepartureTime = dto.DepartureTime ?? DateTime.UtcNow.AddHours(24),
                                        ArrivalTime = dto.ArrivalTime ?? DateTime.UtcNow.AddHours(27),
                                        TotalAmount = booking.TotalAmount,
                                        Status = "Confirmed",
                                        Passengers = passengers.Select(p => new PassengerEmailModel
                                        {
                                            Name = p.Name,
                                            Age = p.Age,
                                            SeatNo = p.SeatNo ?? "TBD"
                                        }).ToList()
                                    };

                                    await _emailService.SendBookingConfirmationAsync(userEmail, userName, emailModel);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log but don't fail the booking
                                Serilog.Log.Warning(ex, "Failed to send booking confirmation email for PNR {PNR}", booking.PNR);
                            }
                        });

                        return new { pnr = booking.PNR, status = booking.Status, message = "Booking confirmed. Confirmation email sent." };
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            finally
            {
                flightLock.Release();
            }
        }
    }
}
