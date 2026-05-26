using MediatR;
using Booking.Application.CQRS.Commands;
using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Middleware.Exceptions;

namespace Booking.API.CQRS.Handlers
{
    public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand, CancelBookingResult>
    {
        private readonly BookingDbContext _context;
        private readonly EventBus.Abstractions.IEventBus _eventBus;
        private readonly Shared.EmailService.IEmailService _emailService;

        public CancelBookingCommandHandler(BookingDbContext context, EventBus.Abstractions.IEventBus eventBus, Shared.EmailService.IEmailService emailService) 
        { 
            _context = context; 
            _eventBus = eventBus;
            _emailService = emailService;
        }

        public async Task<CancelBookingResult> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.PNR == request.PNR, cancellationToken);
            if (booking == null) throw new NotFoundException("Booking", request.PNR);
            if (booking.Status == "Cancelled") throw new DomainException("Booking is already fully cancelled.");

            var allPassengers = await _context.Passengers.Where(p => p.PNR == request.PNR).ToListAsync(cancellationToken);
            if (!allPassengers.Any()) throw new NotFoundException("No passengers found for this booking.");

            if (request.PassengerIds != null && request.PassengerIds.Any())
            {
                // EDGE CASE: Partial Cancellation
                var toCancel = allPassengers.Where(p => request.PassengerIds.Contains(p.PassengerId)).ToList();
                if (!toCancel.Any()) throw new NotFoundException("None of the specified passengers were found in this booking.");

                // Example Penalty Logic: 10% penalty per passenger
                decimal refundAmount = 0;
                var basePricePerPassenger = booking.Status == "Pending" ? 0 : booking.TotalAmount / allPassengers.Count;
                var releasedSeats = new List<string>();
                
                foreach(var p in toCancel)
                {
                    _context.Passengers.Remove(p);
                    refundAmount += basePricePerPassenger * 0.9m; // 10% penalty
                    if (!string.IsNullOrEmpty(p.SeatNo)) releasedSeats.Add(p.SeatNo);
                }

                booking.TotalAmount -= basePricePerPassenger * toCancel.Count; // Adjust total
                
                // If all passengers happen to be cancelled individually, mark booking as fully cancelled
                if (toCancel.Count == allPassengers.Count)
                {
                    booking.Status = "Cancelled";
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Publish BookingCancelledEvent
                _eventBus.Publish(new EventBus.Events.BookingCancelledEvent
                {
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    PNR = booking.PNR,
                    UserId = booking.UserId,
                    FlightId = booking.FlightId,
                    SeatsReleased = toCancel.Count,
                    RefundAmount = refundAmount,
                    ReleasedSeatNumbers = releasedSeats
                });

                // Publish BookingStatusChangedEvent to update Read Model
                _eventBus.Publish(new EventBus.Events.BookingStatusChangedEvent
                {
                    PNR = booking.PNR,
                    UserId = booking.UserId,
                    FlightId = booking.FlightId,
                    Status = booking.Status,
                    TotalAmount = booking.TotalAmount,
                    PassengerCount = allPassengers.Count - toCancel.Count,
                    FlightNumber = null,
                    Source = null,
                    Destination = null,
                    DepartureTime = null,
                    UserEmail = booking.UserEmail
                });

                // Publish SeatInventoryChangedEvent
                _eventBus.Publish(new EventBus.Events.SeatInventoryChangedEvent
                {
                    FlightId = booking.FlightId,
                    SeatsBooked = 0,
                    SeatsReleased = toCancel.Count,
                    ReleasedSeatNumbers = releasedSeats
                });

                // Send Email Notification
                if (!string.IsNullOrWhiteSpace(booking.UserEmail))
                {
                    _ = Task.Run(async () => 
                    {
                        try {
                            await _emailService.SendBookingCancellationAsync(booking.UserEmail, booking.UserName ?? "Passenger", booking.PNR, $"AG-{booking.FlightId:D4}", refundAmount);
                        } catch { /* Fire and forget */ }
                    });
                }

                return new CancelBookingResult {
                    RefundAmount = refundAmount,
                    Message = $"Partially cancelled {toCancel.Count} passengers. Refund scheduled: ₹{refundAmount:F2}. 10% penalty applied."
                };
            }

            // EDGE CASE: Full Cancellation
            var originalStatus = booking.Status;
            booking.Status = "Cancelled";
            var totalRefund = originalStatus == "Pending" ? 0 : booking.TotalAmount * 0.9m; // 10% platform penalty
            var allReleasedSeats = allPassengers.Select(p => p.SeatNo).Where(s => !string.IsNullOrEmpty(s)).ToList();
            await _context.SaveChangesAsync(cancellationToken);
            
            // Publish BookingCancelledEvent for Refund processing
            _eventBus.Publish(new EventBus.Events.BookingCancelledEvent
            {
                CorrelationId = Guid.NewGuid().ToString("N"),
                PNR = booking.PNR,
                UserId = booking.UserId,
                FlightId = booking.FlightId,
                SeatsReleased = allPassengers.Count,
                RefundAmount = totalRefund,
                ReleasedSeatNumbers = allReleasedSeats
            });

            // Publish BookingStatusChangedEvent to update Read Model
            _eventBus.Publish(new EventBus.Events.BookingStatusChangedEvent
            {
                PNR = booking.PNR,
                UserId = booking.UserId,
                FlightId = booking.FlightId,
                Status = booking.Status,
                TotalAmount = booking.TotalAmount,
                PassengerCount = 0,
                FlightNumber = null,
                Source = null,
                Destination = null,
                DepartureTime = null,
                UserEmail = booking.UserEmail
            });

            // Publish SeatInventoryChangedEvent
            _eventBus.Publish(new EventBus.Events.SeatInventoryChangedEvent
            {
                FlightId = booking.FlightId,
                SeatsBooked = 0,
                SeatsReleased = allPassengers.Count,
                ReleasedSeatNumbers = allReleasedSeats
            });

            // Send Email Notification
            if (!string.IsNullOrWhiteSpace(booking.UserEmail))
            {
                _ = Task.Run(async () => 
                {
                    try {
                        await _emailService.SendBookingCancellationAsync(booking.UserEmail, booking.UserName ?? "Passenger", booking.PNR, $"AG-{booking.FlightId:D4}", totalRefund);
                    } catch { /* Fire and forget */ }
                });
            }

            return new CancelBookingResult {
                RefundAmount = totalRefund,
                Message = $"Booking {request.PNR} fully cancelled. Refund scheduled: ₹{totalRefund:F2}. 10% penalty applied."
            };
        }
    }
}
