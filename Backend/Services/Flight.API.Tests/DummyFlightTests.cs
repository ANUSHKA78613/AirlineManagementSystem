using NUnit.Framework;
using FlightEntity = Flight.Domain.Entities.Flight; 
namespace Flight.API.Tests
{
    [TestFixture]
    public class FlightTests
    {
        [Test]
        public void CreateFlight_WithValidData_ShouldSetProperties()
        {
            var flight = new FlightEntity
            {
                FlightNumber = "AI101",
                Source = "DEL",
                Destination = "BOM"
            };

            Assert.That(flight.FlightNumber, Is.EqualTo("AI101"));
            Assert.That(flight.Source, Is.EqualTo("DEL"));
            Assert.That(flight.Destination, Is.EqualTo("BOM"));
        }

        [Test]
        public void Flight_ShouldHaveAvailableSeats_WhenSeatsGreaterThanZero()
        {
            var flight = new FlightEntity { AvailableSeats = 5 };

            Assert.That(flight.AvailableSeats > 0, Is.True);
        }

        [Test]
        public void Flight_ShouldNotHaveAvailableSeats_WhenSeatsZero()
        {
            var flight = new FlightEntity { AvailableSeats = 0 };

            Assert.That(flight.AvailableSeats > 0, Is.False);
        }
    }
}