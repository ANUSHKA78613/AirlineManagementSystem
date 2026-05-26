using System;
using NUnit.Framework;

namespace Booking.API.Tests
{
    [TestFixture]
    public class DummyBookingTests
    {
        [Test]
        public void BookingTest_Environment_IsSetup()
        {
            Assert.That(true, Is.True);
        }
    }
}
