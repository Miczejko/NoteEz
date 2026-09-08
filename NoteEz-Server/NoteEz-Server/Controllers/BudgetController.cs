using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    [ApiController]
    [Route("api/budget")]
    [Authorize]
    public class BudgetController : ControllerBase
    {
        private readonly BudgetService _budget;

        public BudgetController(BudgetService budget)
        {
            _budget = budget;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories([FromQuery] Guid? groupId)
        {
            try
            {
                return Ok(await _budget.GetCategoriesAsync(UserId, groupId));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory(CreateBudgetCategoryRequest req)
        {
            try
            {
                var category = await _budget.CreateCategoryAsync(UserId, req);
                return CreatedAtAction(nameof(GetCategories), new { groupId = req.GroupId }, category);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("entries")]
        public async Task<IActionResult> GetEntries(
            [FromQuery] Guid? groupId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? createdByUserId)
        {
            try
            {
                return Ok(await _budget.GetEntriesAsync(UserId, groupId, from, to, createdByUserId));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("entries")]
        public async Task<IActionResult> CreateEntry(CreateBudgetEntryRequest req)
        {
            try
            {
                var entry = await _budget.CreateEntryAsync(UserId, req);
                return CreatedAtAction(nameof(GetEntries), new { groupId = req.GroupId }, entry);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPut("entries/{id}")]
        public async Task<IActionResult> UpdateEntry(Guid id, UpdateBudgetEntryRequest req)
        {
            try
            {
                return Ok(await _budget.UpdateEntryAsync(UserId, id, req));
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpDelete("entries/{id}")]
        public async Task<IActionResult> DeleteEntry(Guid id)
        {
            try
            {
                await _budget.DeleteEntryAsync(UserId, id);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(
            [FromQuery] Guid? groupId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to)
        {
            try
            {
                return Ok(await _budget.GetStatsAsync(UserId, groupId, from, to));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }
    }
}
