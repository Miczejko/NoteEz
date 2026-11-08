using System.ComponentModel.DataAnnotations;

namespace NoteEz_Server.Models
{
    public enum BudgetEntryType
    {
        Income,
        Expense
    }

    public class BudgetCategory
    {
        [Key]
        public Guid Id { get; set; }
        public Guid? GroupId { get; set; } // null = kategoria dostepna w budzecie osobistym danego usera
        public Guid? UserId { get; set; } // wlasciciel kategorii, gdy GroupId == null
        public string Name { get; set; }
        public string Icon { get; set; } // nazwa ikony z frontendowego setu (np. "shopping-cart")
        public string Color { get; set; } // "#rrggbb"

        public Group? Group { get; set; }
        public User? User { get; set; }
    }

    public class BudgetEntry
    {
        [Key]
        public Guid Id { get; set; }
        public Guid? GroupId { get; set; } // null = wpis osobisty (widoczny tylko na koncie CreatedByUserId)
        public Guid CategoryId { get; set; }
        public BudgetEntryType Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "PLN";
        public string? Description { get; set; }
        public Guid CreatedByUserId { get; set; } // kto dodal wpis - pozwala filtrowac po osobie w grupie
        public DateOnly Date { get; set; }
        public DateTime CreatedAt { get; set; }

        public Group? Group { get; set; }
        public BudgetCategory Category { get; set; }
        public User CreatedByUser { get; set; }
    }

    public enum RecurringFrequency
    {
        Weekly,
        Monthly
    }

    public class RecurringBudgetEntry
    {
        [Key]
        public Guid Id { get; set; }
        public Guid? GroupId { get; set; } // null = cykliczny wpis osobisty
        public Guid CategoryId { get; set; }
        public BudgetEntryType Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "PLN";
        public string? Description { get; set; }
        public Guid CreatedByUserId { get; set; }
        public RecurringFrequency Frequency { get; set; }
        public DateOnly NextDueDate { get; set; }
        public bool IsActive { get; set; } = true;

        public Group? Group { get; set; }
        public BudgetCategory Category { get; set; }
        public User CreatedByUser { get; set; }
    }

    public record BudgetCategoryDto(Guid Id, string Name, string Icon, string Color);

    public record BudgetEntryDto(
        Guid Id,
        Guid? GroupId,
        Guid CategoryId,
        string CategoryName,
        string Type,
        decimal Amount,
        string Currency,
        string? Description,
        Guid CreatedByUserId,
        string CreatedByUsername,
        DateOnly Date);

    public record RecurringBudgetEntryDto(
        Guid Id,
        Guid? GroupId,
        Guid CategoryId,
        string CategoryName,
        string Type,
        decimal Amount,
        string Currency,
        string? Description,
        string Frequency,
        DateOnly NextDueDate,
        bool IsActive);

    public record BudgetCategoryStatDto(Guid CategoryId, string CategoryName, decimal TotalIncome, decimal TotalExpense);

    public record BudgetPersonStatDto(Guid UserId, string Username, decimal TotalIncome, decimal TotalExpense);

    public record BudgetStatsDto(
        decimal TotalIncome,
        decimal TotalExpense,
        decimal Balance,
        IReadOnlyList<BudgetCategoryStatDto> ByCategory,
        IReadOnlyList<BudgetPersonStatDto> ByPerson);

    public record CreateBudgetCategoryRequest(
        [Required, StringLength(50, MinimumLength = 1)]
        string Name,
        [StringLength(50)]
        string? Icon,
        [RegularExpression(@"^#[0-9a-fA-F]{6}$")]
        string? Color,
        Guid? GroupId);

    public record CreateBudgetEntryRequest(
        Guid? GroupId,
        [Required]
        Guid CategoryId,
        [Required]
        BudgetEntryType Type,
        [Range(0.01, 100_000_000)]
        decimal Amount,
        [StringLength(3, MinimumLength = 3)]
        string? Currency,
        [StringLength(500)]
        string? Description,
        [Required]
        DateOnly Date);

    public record UpdateBudgetEntryRequest(
        [Required]
        Guid CategoryId,
        [Required]
        BudgetEntryType Type,
        [Range(0.01, 100_000_000)]
        decimal Amount,
        [StringLength(3, MinimumLength = 3)]
        string? Currency,
        [StringLength(500)]
        string? Description,
        [Required]
        DateOnly Date);

    public record CreateRecurringBudgetEntryRequest(
        Guid? GroupId,
        [Required]
        Guid CategoryId,
        [Required]
        BudgetEntryType Type,
        [Range(0.01, 100_000_000)]
        decimal Amount,
        [StringLength(3, MinimumLength = 3)]
        string? Currency,
        [StringLength(500)]
        string? Description,
        [Required]
        RecurringFrequency Frequency,
        [Required]
        DateOnly NextDueDate);

    // Suma wydatkow z paragonu w jednej kategorii. CategoryId != null -> istniejaca kategoria.
    // CategoryId == null i SuggestedName != null -> propozycja nowej kategorii (nic nie jest zapisywane w bazie,
    // uzytkownik zatwierdza/zmienia ja w UI). Oba null -> pozycje bez kategorii ("Inne").
    // Items - nazwy pozycji z paragonu, ktore trafily do tej grupy (do podgladu / zmiany przypisania).
    public record ReceiptScanCategoryTotalDto(
        Guid? CategoryId,
        string? SuggestedName,
        decimal Total,
        IReadOnlyList<string> Items);

    public record ReceiptScanResultDto(
        string? Merchant,
        DateOnly? Date,
        string? Currency,
        IReadOnlyList<ReceiptScanCategoryTotalDto> Categories,
        decimal? Total,
        // true, gdy suma pozycji nie zgadza sie z odczytana suma paragonu (w granicach tolerancji) -
        // sygnal dla frontendu, zeby pokazac ostrzezenie "sprawdz kwoty" zamiast ufac im w ciemno.
        bool AmountsMismatch);

    public record ConfirmRecurringBudgetEntryRequest(
        [Range(0.01, 100_000_000)]
        decimal Amount,
        [StringLength(500)]
        string? Description,
        [Required]
        DateOnly Date);
}
