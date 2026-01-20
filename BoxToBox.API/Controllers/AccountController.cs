using BoxToBox.ApplicationService.Services;
using BoxToBox.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxToBox.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<UserModel>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var user = await _accountService.RegisterAsync(request.UserName, request.Email, request.Password, request.FirstName, request.LastName);
                return Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _accountService.LoginAsync(request.UserNameOrEmail, request.Password, request.RememberMe);
            if (result == null)
                return Unauthorized(new { message = "Invalid credentials" });

            return Ok(new { user = result.Value.user, token = result.Value.token });
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        public async Task<ActionResult<UserModel>> GetById(Guid id)
        {
            var user = await _accountService.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<ActionResult> Delete(Guid id)
        {
            var deleted = await _accountService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "User not found or could not be deleted" });

            return Ok(new { message = "User account deleted successfully" });
        }
    }

    public record RegisterRequest(string UserName, string Email, string Password, string FirstName, string LastName);

    public record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe = false);
}
