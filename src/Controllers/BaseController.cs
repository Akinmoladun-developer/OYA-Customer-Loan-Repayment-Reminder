using Microsoft.AspNetCore.Mvc;

namespace OyaMicroCreditCLRRS.Controllers;


// Shared base for all API controllers tO sets the route prefix and marks all endpoints as API controllers.

[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseController : ControllerBase
{
   
    // Returns the authenticated user's ID from JWT claims.
    // Used for audit fields (TriggeredBy, CreatedBy, etc.)
   
    protected string CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? "System";

   
    // Returns the authenticated user's full name from JWT claims.
 
    protected string CurrentUserName =>
        User.FindFirst("FullName")?.Value
        ?? User.Identity?.Name
        ?? "Unknown";
}