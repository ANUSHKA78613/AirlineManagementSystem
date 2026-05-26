using System;

namespace Identity.Application.DTOs
{
    public class SavedPassengerDto
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? Gender { get; set; }
    }
}
