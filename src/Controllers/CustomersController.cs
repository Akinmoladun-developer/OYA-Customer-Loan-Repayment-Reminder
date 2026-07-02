using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Domain.Enums;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

[Authorize]
public class CustomersController : BaseController
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    // GET api/v1/
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerResponse>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _customerService.GetAllAsync(page, pageSize, search);
        return Ok(ApiResponse<PagedResult<CustomerResponse>>.Ok(result));
    }

    // GET api/v1/customers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetById(Guid id)
    {
        var result = await _customerService.GetByIdAsync(id);
        return Ok(ApiResponse<CustomerResponse>.Ok(result));
    }

    // GET api/v1/customers/by-phone/{phone}
    [HttpGet("by-phone/{phone}")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> GetByPhone(string phone)
    {
        var result = await _customerService.GetByPhoneAsync(phone);
        return Ok(ApiResponse<CustomerResponse>.Ok(result));
    }

    // POST api/v1/customers
    [HttpPost]
    [Authorize(Roles = "Admin,LoanOfficer")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Create(
        [FromBody] CreateCustomerRequest request)
    {
        var result = await _customerService.CreateAsync(request);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<CustomerResponse>.Ok(result, "Customer created successfully."));
    }

    // PUT api/v1/customers/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,LoanOfficer")]
    public async Task<ActionResult<ApiResponse<CustomerResponse>>> Update(
        Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _customerService.UpdateAsync(id, request);
        return Ok(ApiResponse<CustomerResponse>.Ok(result, "Customer updated successfully."));
    }

    // POST api/v1/customers/{id}/opt-out
    [HttpPost("{id:guid}/opt-out")]
    public async Task<ActionResult<ApiResponse<string>>> OptOut(
        Guid id, [FromQuery] NotificationChannel channel = NotificationChannel.Sms)
    {
        await _customerService.OptOutAsync(id, channel);
        return Ok(ApiResponse<string>.Ok(
            "Success", $"Customer opted out of {channel} notifications."));
    }

    // POST api/v1/customers/{id}/opt-in
    [HttpPost("{id:guid}/opt-in")]
    public async Task<ActionResult<ApiResponse<string>>> OptIn(
        Guid id, [FromQuery] NotificationChannel channel = NotificationChannel.Sms)
    {
        await _customerService.OptInAsync(id, channel);
        return Ok(ApiResponse<string>.Ok(
            "Success", $"Customer opted back in to {channel} notifications."));
    }

    // DELETE api/v1/customers/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(Guid id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(ApiResponse<string>.Ok("Success", "Customer deleted successfully."));
    }
}