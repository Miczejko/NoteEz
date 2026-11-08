using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly ReceiptScanService _receiptScan;

        private const long MaxReceiptImageSizeBytes = 8_000_000; // ~8MB - zdjecie z telefonu, nie skan w 4K

        public BudgetController(BudgetService budget, ReceiptScanService receiptScan)
        {
            _budget = budget;
            _receiptScan = receiptScan;
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

        [HttpGet("recurring")]
        public async Task<IActionResult> GetRecurring([FromQuery] Guid? groupId)
        {
            try
            {
                return Ok(await _budget.GetRecurringAsync(UserId, groupId));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("recurring/due-soon")]
        public async Task<IActionResult> GetRecurringDueSoon([FromQuery] Guid? groupId, [FromQuery] int daysAhead = 3)
        {
            try
            {
                return Ok(await _budget.GetDueSoonAsync(UserId, groupId, daysAhead));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("recurring")]
        public async Task<IActionResult> CreateRecurring(CreateRecurringBudgetEntryRequest req)
        {
            try
            {
                var recurring = await _budget.CreateRecurringAsync(UserId, req);
                return CreatedAtAction(nameof(GetRecurring), new { groupId = req.GroupId }, recurring);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpDelete("recurring/{id}")]
        public async Task<IActionResult> DeleteRecurring(Guid id)
        {
            try
            {
                await _budget.DeleteRecurringAsync(UserId, id);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("receipts/scan")]
        [RequestSizeLimit(MaxReceiptImageSizeBytes)]
        [EnableRateLimiting("scan-receipt")]
        public async Task<IActionResult> ScanReceipt(IFormFile file, [FromQuery] Guid? groupId)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "Brak zdjęcia paragonu." });

            if (file.Length > MaxReceiptImageSizeBytes)
                return BadRequest(new { error = "Zdjęcie jest za duże." });

            await using var stream = file.OpenReadStream();
            var header = new byte[ImageSignatureValidator.RequiredHeaderBytes];
            var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
            stream.Position = 0;

            var mediaType = ImageSignatureValidator.DetectMediaType(header.AsSpan(0, read));
            if (mediaType is null)
                return BadRequest(new { error = "Nieobsługiwany format zdjęcia (dozwolone: JPEG, PNG, WebP)." });

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            try
            {
                var result = await _receiptScan.ScanAsync(ms.ToArray(), mediaType, UserId, groupId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("recurring/{id}/confirm")]
        public async Task<IActionResult> ConfirmRecurring(Guid id, ConfirmRecurringBudgetEntryRequest req)
        {
            try
            {
                return Ok(await _budget.ConfirmRecurringAsync(UserId, id, req));
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }
    }
}
