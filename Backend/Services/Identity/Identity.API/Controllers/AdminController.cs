using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using Identity.Application.CQRS.Queries;
using Identity.Application.CQRS.Commands;

namespace Identity.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _mediator.Send(new GetAllUsersQuery());
            return Ok(users);
        }

        [HttpPost("users/{userId}/block")]
        public async Task<IActionResult> BlockUser(int userId)
        {
            var result = await _mediator.Send(new BlockUserCommand(userId));
            return Ok(new { message = result });
        }

        [HttpPost("users/{userId}/unblock")]
        public async Task<IActionResult> UnblockUser(int userId)
        {
            var result = await _mediator.Send(new UnblockUserCommand(userId));
            return Ok(new { message = result });
        }

        [HttpPost("users/{userId}/role")]
        public async Task<IActionResult> AssignRole(int userId, [FromQuery] string newRole)
        {
            var result = await _mediator.Send(new AssignRoleCommand(userId, newRole));
            return Ok(new { message = result });
        }
    }
}
