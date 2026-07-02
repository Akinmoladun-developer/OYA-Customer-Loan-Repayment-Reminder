using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST api/v1/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Login successful."));
    }

    // POST api/v1/auth/refresh
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(
        [FromQuery] string refreshToken)
    {
        var result = await _authService.RefreshTokenAsync(refreshToken);
        return Ok(ApiResponse<AuthResponse>.Ok(result, "Token refreshed."));
    }

    // GET api/v1/auth/staff
    [HttpGet("staff")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<StaffUserResponse>>>> GetAllStaff()
    {
        var result = await _authService.GetAllStaffAsync();
        return Ok(ApiResponse<IEnumerable<StaffUserResponse>>.Ok(result));
    }

    // POST api/v1/auth/staff
    [HttpPost("staff")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<StaffUserResponse>>> CreateStaff(
        [FromBody] CreateStaffUserRequest request)
    {
        var result = await _authService.CreateStaffUserAsync(request);
        return CreatedAtAction(
            nameof(GetAllStaff),
            ApiResponse<StaffUserResponse>.Ok(result, "Staff user created successfully."));
    }

    // DELETE api/v1/auth/staff/{userId}
    [HttpDelete("staff/{userId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> DeactivateStaff(string userId)
    {
        await _authService.DeactivateStaffAsync(userId);
        return Ok(ApiResponse<string>.Ok("Success", "Staff user deactivated."));
    }

    // GET api/v1/auth/me
    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<object>> Me()
    {
        var user = new
        {
            Id = CurrentUserId,
            Name = CurrentUserName,
            Claims = User.Claims.Select(c => new { c.Type, c.Value })
        };
        return Ok(ApiResponse<object>.Ok(user));
    }
}