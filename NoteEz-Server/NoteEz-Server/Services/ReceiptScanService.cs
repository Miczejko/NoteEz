using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;
using NoteEz_Server.Models;

namespace NoteEz_Server.Services
{
    public class ReceiptScanService
    {
        private readonly AnthropicClient _client;
        private readonly BudgetService _budget;

        public ReceiptScanService(IConfiguration config, BudgetService budget)
        {
            _client = new AnthropicClient { ApiKey = config["Anthropic:ApiKey"] };
            _budget = budget;
        }

        // userId pochodzi z JWT (kontroler), nigdy z requestu. Kategorie pobieramy przez BudgetService,
        // ktory sam sprawdza czlonkostwo w grupie (UnauthorizedAccessException -> kontroler zwraca 403).
        public async Task<ReceiptScanResultDto> ScanAsync(byte[] imageBytes, string mediaType, Guid userId, Guid? groupId)
        {
            var categories = await _budget.GetCategoriesAsync(userId, groupId);

            try
            {
                return await ScanCoreAsync(imageBytes, mediaType, categories);
            }
            catch (Anthropic.Exceptions.AnthropicException)
            {
                // Awaria/limit/zla konfiguracja po stronie Anthropic nie powinna wyciekac jako 500
                // ze stack trace'em - zamieniamy na czytelny blad, ktory kontroler mapuje na 400.
                throw new InvalidOperationException("Nie udało się przetworzyć paragonu - spróbuj ponownie za chwilę.");
            }
        }

        private async Task<ReceiptScanResultDto> ScanCoreAsync(
            byte[] imageBytes, string mediaType, IReadOnlyList<BudgetCategoryDto> categories)
        {
            var base64Data = Convert.ToBase64String(imageBytes);

            var schema = new Dictionary<string, JsonElement>
            {
                ["type"] = JsonSerializer.SerializeToElement("object"),
                ["properties"] = JsonSerializer.SerializeToElement(new
                {
                    merchant = new { type = new[] { "string", "null" } },
                    date = new { type = new[] { "string", "null" }, description = "Data zakupu w formacie YYYY-MM-DD, jesli widoczna na paragonie, w przeciwnym razie null." },
                    currency = new { type = new[] { "string", "null" }, description = "Trzyliterowy kod waluty ISO (np. PLN, EUR), jesli mozna go ustalic." },
                    items = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                name = new { type = "string" },
                                quantity = new { type = new[] { "number", "null" } },
                                unitPrice = new { type = new[] { "number", "null" } },
                                totalPrice = new { type = new[] { "number", "null" } },
                                categoryId = new { type = new[] { "string", "null" }, description = "Id jednej z podanych kategorii pasujacej do produktu albo null, jesli zadna nie pasuje." },
                                suggestedCategory = new { type = new[] { "string", "null" }, description = "Tylko gdy categoryId jest null: krotka, ogolna nazwa kategorii po polsku (np. Chemia domowa). Dla podobnych produktow uzywaj tej samej nazwy." },
                            },
                            required = new[] { "name" },
                            additionalProperties = false,
                        },
                    },
                    total = new { type = new[] { "number", "null" }, description = "Laczna kwota do zaplaty widoczna na paragonie." },
                }),
                ["required"] = JsonSerializer.SerializeToElement(new[] { "items" }),
                ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
            };

            var categoriesPrompt = (categories.Count == 0
                ? "\n\nUzytkownik nie ma jeszcze zadnych kategorii - zostaw categoryId = null."
                : "\n\nKazdej pozycji przypisz categoryId z ponizszej listy (uzyj dokladnie podanego id) " +
                  "albo null, jesli zadna kategoria wyraznie nie pasuje:\n" +
                  string.Join("\n", categories.Select(c => $"- {c.Id}: {c.Name}"))) +
                "\n\nGdy categoryId = null, wpisz w suggestedCategory krotka, ogolna nazwe kategorii " +
                "(1-2 slowa, np. \"Chemia domowa\", \"Transport\") i uzywaj jej spojnie dla podobnych produktow. " +
                "Nie wymyslaj osobnej kategorii dla kazdego produktu.";

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                // Sonnet 5 - Haiku 4.5 mylil linie podsumowania paragonu z pozycjami zakupowymi.
                // Sonnet 5 lepiej radzi sobie z odczytem obrazu, wciaz ~2.5x tanszy od Opus 5.
                Model = "claude-sonnet-5",
                MaxTokens = 2048,
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat { Schema = schema },
                },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ImageBlockParam { Source = new Base64ImageSource { MediaType = mediaType, Data = base64Data } },
                            new TextBlockParam
                            {
                                Text = "To zdjecie paragonu ze sklepu. Wypisz z niego dane sprzedawcy, date, walute " +
                                       "oraz liste pozycji (nazwa, ilosc, cena jednostkowa, cena laczna) i sume " +
                                       "calkowita osobno w polu total.\n\n" +
                                       "WAZNE: do listy items wpisuj WYLACZNIE faktycznie kupione produkty/uslugi - " +
                                       "nie dodawaj do niej linii podsumowania takich jak \"SUMA\", \"SUMA PLN\", " +
                                       "\"RAZEM\", \"DO ZAPLATY\", \"RESZTA\", \"GOTOWKA\", \"KARTA\", naliczonego " +
                                       "podatku VAT ani numeru paragonu/kasy - te wartosci naleza do pola total, " +
                                       "nie do items.\n\n" +
                                       "Do items wpisuj TEZ kaucje (np. \"KAUCJA\", \"OPAKOWANIE ZWROTNE\", " +
                                       "\"OPLATA ZA BUTELKE\"), oplaty za torbe oraz rabaty/promocje - rabaty i " +
                                       "zwrot kaucji jako wartosc UJEMNA. Te linie wchodza do sumy paragonu, wiec " +
                                       "suma wszystkich items powinna dawac total.\n\n" +
                                       "Jesli jakiejs wartosci nie da sie odczytac z pewnoscia, zwroc null zamiast " +
                                       "zgadywac - lepiej brakujace pole niz zmyslona kwota." + categoriesPrompt,
                            },
                        },
                    },
                ],
            });

            var text = response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .FirstOrDefault()?.Text
                ?? throw new InvalidOperationException("Claude nie zwrocil oczekiwanej odpowiedzi tekstowej.");

            var parsed = JsonSerializer.Deserialize<ParsedReceipt>(text, JsonOptions)
                ?? throw new InvalidOperationException("Nie udalo sie odczytac danych z paragonu.");

            // Siatka bezpieczenstwa - mimo instrukcji w prompcie model czasem i tak wrzuca linie
            // podsumowania (np. "SUMA PLN") do listy pozycji. Odfiltrowujemy po znanych slowach kluczowych.
            var items = (parsed.Items ?? []).Where(i => !IsSummaryLine(i.Name)).ToList();

            var itemsSum = items.Sum(ItemAmount);
            var mismatch = parsed.Total is not null && Math.Abs(itemsSum - parsed.Total.Value) > 0.05m;

            // Sumujemy w C# (model myli sie w arytmetyce). Model moze zwrocic id spoza listy -
            // przyjmujemy tylko kategorie nalezace do uzytkownika/grupy; sugestie rownajaca sie nazwie
            // istniejacej kategorii mapujemy na nia zamiast proponowac duplikat.
            var byId = categories.ToDictionary(c => c.Id);
            var byName = categories
                .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var categoryTotals = items
                .GroupBy(i =>
                {
                    if (Guid.TryParse(i.CategoryId, out var cid) && byId.ContainsKey(cid))
                        return new CategoryKey(cid, null);

                    var suggested = i.SuggestedCategory?.Trim();
                    if (string.IsNullOrEmpty(suggested)) return new CategoryKey(null, null);
                    return byName.TryGetValue(suggested, out var existing)
                        ? new CategoryKey(existing, null)
                        : new CategoryKey(null, suggested.ToLowerInvariant());
                })
                .Select(g => new ReceiptScanCategoryTotalDto(
                    g.Key.Id,
                    // Wyswietlana nazwa propozycji: pierwsza forma zapisu z paragonu (z oryginalna wielkoscia liter).
                    g.Key.Suggested is null ? null : g.First().SuggestedCategory!.Trim(),
                    g.Sum(ItemAmount),
                    g.Select(i => i.Name).ToList()))
                .OrderBy(c => c.CategoryId is null) // dopasowane kategorie najpierw, propozycje/"inne" na koncu
                .ThenByDescending(c => c.Total)
                .ToList();

            // Siatka bezpieczenstwa: gdy model pominal jakas linie (kaucja, rabat...), roznice dodajemy jako
            // osobny wiersz zamiast gubic ja po cichu - uzytkownik moze nadac jej kategorie albo odznaczyc.
            if (mismatch)
            {
                var diff = Math.Round(parsed.Total!.Value - itemsSum, 2);
                categoryTotals.Add(new ReceiptScanCategoryTotalDto(
                    null, null, diff, ["Różnica z paragonu (np. kaucja, rabat)"]));
            }

            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(parsed.Date) && DateOnly.TryParse(parsed.Date, out var parsedDate))
                date = parsedDate;

            return new ReceiptScanResultDto(parsed.Merchant, date, parsed.Currency, categoryTotals, parsed.Total, mismatch);
        }

        // Kwota pozycji: cena laczna, a gdy jej brak - cena jednostkowa * ilosc. Rabaty (ujemne linie) sumuja sie naturalnie.
        private static decimal ItemAmount(ParsedItem i)
            => i.TotalPrice ?? (i.UnitPrice is null ? 0 : i.UnitPrice.Value * (i.Quantity ?? 1));

        // Celowo bez "KARTA" - zbyt ogolne slowo, ktore trafia sie tez w nazwach produktow
        // (np. "karta podarunkowa", "karta SIM").
        private static readonly string[] SummaryLineKeywords =
        [
            "SUMA", "RAZEM", "DO ZAPLATY", "DO ZAPŁATY", "RESZTA", "GOTOWKA", "GOTÓWKA",
            "PLATNOSC KARTA", "PŁATNOŚĆ KARTĄ", "PARAGON FISKALNY", "NR PARAGONU",
        ];

        private static bool IsSummaryLine(string name)
        {
            var normalized = (name ?? "").Trim().ToUpperInvariant();
            return SummaryLineKeywords.Any(k => normalized.Contains(k));
        }

        private record CategoryKey(Guid? Id, string? Suggested);

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private class ParsedReceipt
        {
            public string? Merchant { get; set; }
            public string? Date { get; set; }
            public string? Currency { get; set; }
            public List<ParsedItem>? Items { get; set; }
            public decimal? Total { get; set; }
        }

        private class ParsedItem
        {
            public string Name { get; set; } = "";
            public decimal? Quantity { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? TotalPrice { get; set; }
            public string? CategoryId { get; set; }
            public string? SuggestedCategory { get; set; }
        }
    }
}
