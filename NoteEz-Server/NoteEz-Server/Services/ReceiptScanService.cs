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

        public ReceiptScanService(IConfiguration config)
        {
            _client = new AnthropicClient { ApiKey = config["Anthropic:ApiKey"] };
        }

        public async Task<ReceiptScanResultDto> ScanAsync(byte[] imageBytes, string mediaType)
        {
            try
            {
                return await ScanCoreAsync(imageBytes, mediaType);
            }
            catch (Anthropic.Exceptions.AnthropicException)
            {
                // Awaria/limit/zla konfiguracja po stronie Anthropic nie powinna wyciekac jako 500
                // ze stack trace'em - zamieniamy na czytelny blad, ktory kontroler mapuje na 400.
                throw new InvalidOperationException("Nie udało się przetworzyć paragonu - spróbuj ponownie za chwilę.");
            }
        }

        private async Task<ReceiptScanResultDto> ScanCoreAsync(byte[] imageBytes, string mediaType)
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

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                // Haiku 4.5 zamiast Opus 5 - proste odczytanie paragonu (strukturalna ekstrakcja
                // z wyraznego zdjecia) nie wymaga najdrozszego modelu; ~5x tansze per token.
                // Uwaga: Haiku 4.5 nie wspiera OutputConfig.Effort (zwraca blad), wiec go pomijamy.
                Model = "claude-haiku-4-5",
                MaxTokens = 2048,
                OutputConfig = new OutputConfig
                {
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
                                Text = "To zdjecie paragonu ze sklepu. Wypisz z niego dane sprzedawcy, date, " +
                                       "waluty oraz liste pozycji (nazwa, ilosc, cena jednostkowa, cena laczna) " +
                                       "i sume calkowita. Jesli jakejs wartosci nie da sie odczytac z pewnoscia, " +
                                       "zwroc null zamiast zgadywac - lepiej brakujace pole niz zmyslona kwota.",
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

            var items = (parsed.Items ?? []).Select(i => new ReceiptScanItemDto(i.Name, i.Quantity, i.UnitPrice, i.TotalPrice)).ToList();

            var itemsSum = items.Sum(i => i.TotalPrice ?? 0);
            var mismatch = parsed.Total is not null && Math.Abs(itemsSum - parsed.Total.Value) > 0.05m;

            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(parsed.Date) && DateOnly.TryParse(parsed.Date, out var parsedDate))
                date = parsedDate;

            return new ReceiptScanResultDto(parsed.Merchant, date, parsed.Currency, items, parsed.Total, mismatch);
        }

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
        }
    }
}
