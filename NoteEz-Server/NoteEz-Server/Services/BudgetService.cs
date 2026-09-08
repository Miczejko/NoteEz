using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;

namespace NoteEz_Server.Services
{
    public class BudgetService
    {
        private readonly AppDbContext _db;

        public BudgetService(AppDbContext db)
        {
            _db = db;
        }

        // Grupowe wpisy widoczne dla kazdego czlonka grupy; osobiste - tylko dla wlasciciela.
        private async Task EnsureAccessAsync(Guid userId, Guid? groupId)
        {
            if (groupId is null) return;

            var isMember = await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("Nie jesteś członkiem tej grupy.");
        }

        public async Task<List<BudgetCategoryDto>> GetCategoriesAsync(Guid userId, Guid? groupId)
        {
            await EnsureAccessAsync(userId, groupId);

            var query = groupId is null
                ? _db.BudgetCategories.Where(c => c.GroupId == null && c.UserId == userId)
                : _db.BudgetCategories.Where(c => c.GroupId == groupId);

            return await query
                .OrderBy(c => c.Name)
                .Select(c => new BudgetCategoryDto(c.Id, c.Name, c.Icon, c.Color))
                .ToListAsync();
        }

        public async Task<BudgetCategoryDto> CreateCategoryAsync(Guid userId, CreateBudgetCategoryRequest req)
        {
            await EnsureAccessAsync(userId, req.GroupId);

            var category = new BudgetCategory
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                UserId = req.GroupId is null ? userId : null,
                Name = req.Name,
                Icon = req.Icon ?? "tag",
                Color = req.Color ?? "#79c7c5"
            };

            _db.BudgetCategories.Add(category);
            await _db.SaveChangesAsync();

            return new BudgetCategoryDto(category.Id, category.Name, category.Icon, category.Color);
        }

        public async Task<List<BudgetEntryDto>> GetEntriesAsync(
            Guid userId, Guid? groupId, DateOnly? from, DateOnly? to, Guid? createdByUserId)
        {
            await EnsureAccessAsync(userId, groupId);

            var query = groupId is null
                ? _db.BudgetEntries.Where(e => e.GroupId == null && e.CreatedByUserId == userId)
                : _db.BudgetEntries.Where(e => e.GroupId == groupId);

            if (from is not null) query = query.Where(e => e.Date >= from);
            if (to is not null) query = query.Where(e => e.Date <= to);
            if (createdByUserId is not null) query = query.Where(e => e.CreatedByUserId == createdByUserId);

            return await query
                .Include(e => e.Category)
                .Include(e => e.CreatedByUser)
                .OrderByDescending(e => e.Date)
                .Select(e => new BudgetEntryDto(
                    e.Id, e.GroupId, e.CategoryId, e.Category.Name, e.Type.ToString(),
                    e.Amount, e.Currency, e.Description, e.CreatedByUserId, e.CreatedByUser.Username, e.Date))
                .ToListAsync();
        }

        public async Task<BudgetEntryDto> CreateEntryAsync(Guid userId, CreateBudgetEntryRequest req)
        {
            await EnsureAccessAsync(userId, req.GroupId);
            await EnsureCategoryBelongsAsync(req.CategoryId, req.GroupId);

            var entry = new BudgetEntry
            {
                Id = Guid.NewGuid(),
                GroupId = req.GroupId,
                CategoryId = req.CategoryId,
                Type = req.Type,
                Amount = req.Amount,
                Currency = req.Currency ?? "PLN",
                Description = req.Description,
                CreatedByUserId = userId,
                Date = req.Date,
                CreatedAt = DateTime.UtcNow
            };

            _db.BudgetEntries.Add(entry);
            await _db.SaveChangesAsync();

            return await GetEntryDtoAsync(entry.Id);
        }

        public async Task<BudgetEntryDto> UpdateEntryAsync(Guid userId, Guid entryId, UpdateBudgetEntryRequest req)
        {
            var entry = await _db.BudgetEntries.FirstOrDefaultAsync(e => e.Id == entryId)
                ?? throw new KeyNotFoundException("Wpis nie znaleziony.");

            await EnsureEntryEditableAsync(userId, entry);
            await EnsureCategoryBelongsAsync(req.CategoryId, entry.GroupId);

            entry.CategoryId = req.CategoryId;
            entry.Type = req.Type;
            entry.Amount = req.Amount;
            entry.Currency = req.Currency ?? entry.Currency;
            entry.Description = req.Description;
            entry.Date = req.Date;

            await _db.SaveChangesAsync();

            return await GetEntryDtoAsync(entry.Id);
        }

        public async Task DeleteEntryAsync(Guid userId, Guid entryId)
        {
            var entry = await _db.BudgetEntries.FirstOrDefaultAsync(e => e.Id == entryId)
                ?? throw new KeyNotFoundException("Wpis nie znaleziony.");

            await EnsureEntryEditableAsync(userId, entry);

            _db.BudgetEntries.Remove(entry);
            await _db.SaveChangesAsync();
        }

        // Wpisy grupowe moze edytowac kazdy czlonek grupy (wspolny budzet); osobiste - tylko autor.
        private async Task EnsureEntryEditableAsync(Guid userId, BudgetEntry entry)
        {
            if (entry.GroupId is null)
            {
                if (entry.CreatedByUserId != userId)
                    throw new UnauthorizedAccessException("To wpis innego użytkownika.");
                return;
            }

            await EnsureAccessAsync(userId, entry.GroupId);
        }

        private async Task EnsureCategoryBelongsAsync(Guid categoryId, Guid? groupId)
        {
            var category = await _db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == categoryId)
                ?? throw new KeyNotFoundException("Kategoria nie znaleziona.");

            if (category.GroupId != groupId)
                throw new InvalidOperationException("Kategoria nie należy do tego budżetu.");
        }

        private async Task<BudgetEntryDto> GetEntryDtoAsync(Guid entryId)
        {
            return await _db.BudgetEntries
                .Where(e => e.Id == entryId)
                .Include(e => e.Category)
                .Include(e => e.CreatedByUser)
                .Select(e => new BudgetEntryDto(
                    e.Id, e.GroupId, e.CategoryId, e.Category.Name, e.Type.ToString(),
                    e.Amount, e.Currency, e.Description, e.CreatedByUserId, e.CreatedByUser.Username, e.Date))
                .FirstAsync();
        }

        public async Task<BudgetStatsDto> GetStatsAsync(Guid userId, Guid? groupId, DateOnly? from, DateOnly? to)
        {
            await EnsureAccessAsync(userId, groupId);

            var query = groupId is null
                ? _db.BudgetEntries.Where(e => e.GroupId == null && e.CreatedByUserId == userId)
                : _db.BudgetEntries.Where(e => e.GroupId == groupId);

            if (from is not null) query = query.Where(e => e.Date >= from);
            if (to is not null) query = query.Where(e => e.Date <= to);

            var entries = await query
                .Include(e => e.Category)
                .Include(e => e.CreatedByUser)
                .ToListAsync();

            var totalIncome = entries.Where(e => e.Type == BudgetEntryType.Income).Sum(e => e.Amount);
            var totalExpense = entries.Where(e => e.Type == BudgetEntryType.Expense).Sum(e => e.Amount);

            var byCategory = entries
                .GroupBy(e => new { e.CategoryId, e.Category.Name })
                .Select(g => new BudgetCategoryStatDto(
                    g.Key.CategoryId,
                    g.Key.Name,
                    g.Where(e => e.Type == BudgetEntryType.Income).Sum(e => e.Amount),
                    g.Where(e => e.Type == BudgetEntryType.Expense).Sum(e => e.Amount)))
                .ToList();

            var byPerson = entries
                .GroupBy(e => new { e.CreatedByUserId, e.CreatedByUser.Username })
                .Select(g => new BudgetPersonStatDto(
                    g.Key.CreatedByUserId,
                    g.Key.Username,
                    g.Where(e => e.Type == BudgetEntryType.Income).Sum(e => e.Amount),
                    g.Where(e => e.Type == BudgetEntryType.Expense).Sum(e => e.Amount)))
                .ToList();

            return new BudgetStatsDto(totalIncome, totalExpense, totalIncome - totalExpense, byCategory, byPerson);
        }
    }
}
