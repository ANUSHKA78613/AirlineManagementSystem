using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using Identity.Infrastructure.Services;
using Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace Identity.API.Tests
{
    [TestFixture]
    public class JwtTokenServiceTests
    {
        private JwtTokenService _jwtTokenService = null!;
        private IConfiguration _configuration = null!;

        [SetUp]
        public void Setup()
        {
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "my_very_secret_test_key_1234567890_must_be_long_enough",
                    ["Jwt:Issuer"] = "TestIssuer",
                    ["Jwt:Audience"] = "TestAudience"
                })
                .Build();

            _jwtTokenService = new JwtTokenService(_configuration);
        }

        [Test]
        public void GenerateToken_ValidUser_ReturnsValidJwtString()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john.doe@example.com", Role = "Passenger" };

            // Act
            var token = _jwtTokenService.GenerateToken(user);

            // Assert
            Assert.That(token, Is.Not.Null.And.Not.Empty);

            var handler = new JwtSecurityTokenHandler();
            Assert.That(handler.CanReadToken(token), Is.True);

            var jwtToken = handler.ReadJwtToken(token);
            Assert.That(jwtToken.Issuer, Is.EqualTo("TestIssuer"));
            Assert.That(jwtToken.Audiences.First(), Is.EqualTo("TestAudience"));
            
            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email");
            Assert.That(emailClaim?.Value, Is.EqualTo("john.doe@example.com"));
        }

        [Test]
        public void GenerateToken_ChecksExpiration_SetsToEightHoursFromNow()
        {
            // Arrange
            var user = new User { UserId = 2, Name = "Alice", Email = "alice@example.com", Role = "Admin" };

            // Act
            var token = _jwtTokenService.GenerateToken(user);
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            var expectedExpiration = DateTime.UtcNow.AddHours(8);
            var timeDifference = Math.Abs((jwtToken.ValidTo - expectedExpiration).TotalSeconds);

            // Give a 5 seconds tolerance threshold for test execution time
            Assert.That(timeDifference, Is.LessThan(5.0));
        }

        [Test]
        public void GenerateToken_WithMissingConfig_FallsBackToDefaultValues()
        {
            // Arrange (Empty config with no Issuer or Audience set)
            var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> 
            { 
                ["Jwt:Key"] = "fallback_key_1234567890_must_be_long_enough" 
            }).Build();
            
            var fallbackService = new JwtTokenService(emptyConfig);
            var user = new User { UserId = 3, Name = "Bob", Email = "bob@example.com", Role = "Staff" };

            // Act
            var token = fallbackService.GenerateToken(user);
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Assert: Verify defaults are used as per JwtTokenService.cs implementation
            Assert.That(jwtToken.Issuer, Is.EqualTo("AirlineBookingApp"));
            Assert.That(jwtToken.Audiences.First(), Is.EqualTo("SkyHorizon"));
        }

        [Test]
        public void GenerateRefreshToken_WhenCalled_ReturnsValidBase64String()
        {
            // Act
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            // Assert
            Assert.That(refreshToken, Is.Not.Null.And.Not.Empty);

            // Verify it is a valid Base64 string
            Span<byte> buffer = new Span<byte>(new byte[refreshToken.Length]);
            bool isBase64 = Convert.TryFromBase64String(refreshToken, buffer, out int bytesParsed);
            
            Assert.That(isBase64, Is.True);
            // 64 bytes generated should yield a base64 string of length 88
            Assert.That(refreshToken.Length, Is.EqualTo(88));
        }

        [Test]
        public void GenerateRefreshToken_WhenCalledMultipleTimes_YieldsUniqueTokens()
        {
            // Act
            var token1 = _jwtTokenService.GenerateRefreshToken();
            var token2 = _jwtTokenService.GenerateRefreshToken();
            var token3 = _jwtTokenService.GenerateRefreshToken();

            // Assert
            var tokens = new HashSet<string> { token1, token2, token3 };
            Assert.That(tokens.Count, Is.EqualTo(3), "Refresh tokens must be cryptographically unique.");
        }
    }
}
