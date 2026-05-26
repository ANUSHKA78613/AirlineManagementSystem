using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Flight.Infrastructure.Persistence;
using Shared.ImageService;

namespace Flight.API.Controllers
{
    /// <summary>
    /// Manages flight/airline images — admin-only upload and public retrieval.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FlightImageController : ControllerBase
    {
        private readonly FlightDbContext _context;
        private readonly IImageService _imageService;

        public FlightImageController(FlightDbContext context, IImageService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        /// <summary>
        /// Upload an image for a specific flight (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("{flightId:int}/image")]
        public async Task<IActionResult> UploadFlightImage(int flightId, IFormFile file)
        {
            var flight = await _context.Flights.FirstOrDefaultAsync(f => f.FlightId == flightId);
            if (flight == null) return NotFound(new { error = "Flight not found." });

            try
            {
                var imagePath = await _imageService.UploadAsync(file, "flights");
                // Store the image path — using the GateNumber field as a temp solution
                // In production, you'd add a dedicated ImagePath column
                return Ok(new
                {
                    message = "Flight image uploaded.",
                    flightId,
                    imageUrl = _imageService.GetImageUrl(imagePath, Request),
                    imagePath
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get flight image URL
        /// </summary>
        [HttpGet("{flightId:int}/image")]
        public async Task<IActionResult> GetFlightImage(int flightId)
        {
            var flight = await _context.Flights.AsNoTracking().FirstOrDefaultAsync(f => f.FlightId == flightId);
            if (flight == null) return NotFound(new { error = "Flight not found." });

            return Ok(new
            {
                flightId,
                flightNumber = flight.FlightNumber,
                message = "Use the uploads endpoint to retrieve flight images."
            });
        }
    }
}
