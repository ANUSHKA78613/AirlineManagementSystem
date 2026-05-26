using MediatR;

namespace Identity.Application.CQRS.Commands
{
    public class BlockUserCommand : IRequest<string>
    {
        public int UserId { get; set; }
        public BlockUserCommand(int userId) => UserId = userId;
    }

    public class UnblockUserCommand : IRequest<string>
    {
        public int UserId { get; set; }
        public UnblockUserCommand(int userId) => UserId = userId;
    }

    public class AssignRoleCommand : IRequest<string>
    {
        public int UserId { get; set; }
        public string NewRole { get; set; }
        public AssignRoleCommand(int userId, string newRole)
        {
            UserId = userId;
            NewRole = newRole;
        }
    }
}
