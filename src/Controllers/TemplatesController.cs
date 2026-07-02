using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyaMicroCreditCLRRS.Models.Requests;
using OyaMicroCreditCLRRS.Models.Responses;
using OyaMicroCreditCLRRS.Services.Interfaces;

namespace OyaMicroCreditCLRRS.Controllers;

[Authorize(Roles = "Admin,Operations")]
public class TemplatesController : BaseController
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    // GET api/v1/templates
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<TemplateResponse>>>> GetAll()
    {
        var result = await _templateService.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<TemplateResponse>>.Ok(result));
    }

    // GET api/v1/templates/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetById(Guid id)
    {
        var result = await _templateService.GetByIdAsync(id);
        return Ok(ApiResponse<TemplateResponse>.Ok(result));
    }

    // POST api/v1/templates
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<TemplateResponse>>> Create(
        [FromBody] CreateTemplateRequest request)
    {
        var result = await _templateService.CreateAsync(request, CurrentUserId);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<TemplateResponse>.Ok(result, "Template created successfully."));
    }

    // PUT api/v1/templates/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<TemplateResponse>>> Update(
        Guid id, [FromBody] UpdateTemplateRequest request)
    {
        var result = await _templateService.UpdateAsync(id, request, CurrentUserId);
        return Ok(ApiResponse<TemplateResponse>.Ok(result, "Template updated successfully."));
    }

    // POST api/v1/templates/{id}/activate
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> Activate(Guid id)
    {
        await _templateService.ActivateAsync(id, CurrentUserId);
        return Ok(ApiResponse<string>.Ok("Success", "Template activated successfully."));
    }

    // POST api/v1/templates/{id}/preview
    [HttpPost("{id:guid}/preview")]
    public async Task<ActionResult<ApiResponse<string>>> Preview(
        Guid id, [FromBody] Dictionary<string, string> sampleValues)
    {
        var result = await _templateService.PreviewAsync(id, sampleValues);
        return Ok(ApiResponse<string>.Ok(result));
    }

    // POST api/v1/templates/{id}/test-send
    [HttpPost("{id:guid}/test-send")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> TestSend(
        Guid id,
        [FromQuery] string toPhone,
        [FromBody] Dictionary<string, string> sampleValues)
    {
        var sent = await _templateService.TestSendAsync(id, toPhone, sampleValues);
        return Ok(ApiResponse<string>.Ok(
            sent ? "Delivered" : "Failed",
            sent ? "Test message sent successfully." : "Test message failed to send."));
    }
}