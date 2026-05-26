using MediatR;
using Operations.Application.CQRS.Commands;
using Operations.Infrastructure.Persistence;
using Operations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

using Shared.EmailService;
using Operations.API.Services;
using Shared.Middleware.Exceptions;

namespace Operations.API.CQRS.Handlers
{
    public class CheckInCommandHandler : IRequestHandler<CheckInCommand, object>
    {
        private readonly OperationsDbContext _context;
        private readonly IEmailService _emailService;

        public CheckInCommandHandler(OperationsDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<object> Handle(CheckInCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Dto;

            // EDGE CASE: Verify if already checked in
            var existingCheckIn = await _context.CheckIns.FirstOrDefaultAsync(c => c.PNR == dto.PNR && c.PassengerId == dto.PassengerId, cancellationToken);
            if (existingCheckIn != null && existingCheckIn.Status == "CheckedIn")
            {
                throw new DomainException("Passenger has already checked in.");
            }

            // EDGE CASE: Check-in after flight has departed
            if (dto.FlightDepartureTime.HasValue && dto.FlightDepartureTime.Value < DateTime.UtcNow)
            {
                throw new DomainException("Cannot check-in for a flight that has already departed.");
            }

            var checkIn = new CheckIn
            {
                PassengerId = dto.PassengerId,
                PNR = dto.PNR,
                SeatNo = dto.SeatNo,
                CheckInTime = DateTime.UtcNow,
                Status = "CheckedIn"
            };

            _context.CheckIns.Add(checkIn);

            // EDGE CASE: Generate secure cryptographic Boarding Pass Hash payload
            var unhashedPayload = $"{dto.PNR}-PASSENGER_{dto.PassengerId}-SEAT_{dto.SeatNo}-SECURE_CONFIRM";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(unhashedPayload));
            var qrCodeHash = Convert.ToBase64String(hashBytes);

            var randomGate = "G-" + new Random().Next(1, 40);

            var boardingPass = new BoardingPass
            {
                PassengerId = dto.PassengerId,
                PNR = dto.PNR,
                SeatNo = dto.SeatNo,
                Gate = randomGate,
                BoardingTime = DateTime.UtcNow.AddMinutes(45), // Boarding in 45 mins
                QRCode = qrCodeHash
            };

            _context.BoardingPasses.Add(boardingPass);
            await _context.SaveChangesAsync(cancellationToken);

            // Generate Boarding Pass PDF and send via Email (non-critical — don't block check-in)
            try
            {
                var pdfBytes = BoardingPassPdfGenerator.Generate(
                    dto.PNR,
                    dto.PassengerName,
                    dto.SeatNo,
                    randomGate,
                    qrCodeHash
                );

                if (!string.IsNullOrEmpty(dto.Email))
                {
                    var notification = new NotificationRecord
                    {
                        UserId = 0,
                        Email = dto.Email,
                        Subject = $"Boarding Pass Generated — PNR: {dto.PNR}",
                        Message = $"Your boarding pass for seat {dto.SeatNo} has been generated. Head to Gate {randomGate}.",
                        Status = "Sent"
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync(cancellationToken);

                    await _emailService.SendBoardingPassAsync(dto.Email, dto.PassengerName, dto.PNR, pdfBytes);
                }
            }
            catch (Exception)
            {
                // PDF generation failed (QuestPDF may not be installed) — check-in still valid
            }

            return new 
            {
                message = "Web Check-in successful. Cryptographic Boarding pass barcode generated.",
                boardingPassId = boardingPass.BoardingPassId,
                passengerId = dto.PassengerId,
                pnr = dto.PNR,
                seatNo = dto.SeatNo,
                gate = randomGate,
                boardingTime = boardingPass.BoardingTime,
                qrCode = boardingPass.QRCode
            };
        }
    }
}
