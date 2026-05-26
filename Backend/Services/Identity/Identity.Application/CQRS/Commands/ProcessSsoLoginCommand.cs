using MediatR;

namespace Identity.Application.CQRS.Commands
{
    public class ProcessSsoLoginCommand : IRequest<string>
    {
        public string Email { get; set; }
        public string GoogleId { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }

        public ProcessSsoLoginCommand(string email, string googleId, string name, string picture)
        {
            Email = email;
            GoogleId = googleId;
            Name = name;
            Picture = picture;
        }
    }
}
