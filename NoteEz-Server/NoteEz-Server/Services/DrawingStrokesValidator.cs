using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoteEz_Server.Services
{
    // Walidacja strokesJson przysylanego przez urzadzenie (POST /api/device-notes/{id}/drawings).
    // Format (ten sam co zapisuje edytor www, patrz DrawingCanvas.vue):
    //   {"strokes":[{"color":"#rrggbb","width":3,"points":[{"x":1,"y":2},...]}],"canvasWidth":300,"canvasHeight":180}
    // Limity sa celowo hojniejsze niz twarde limity firmware (40 linii / 600 punktow) - serwer ma
    // chronic baze przed nadmiarem, a nie powtarzac ograniczen pamieci ESP32.
    public static class DrawingStrokesValidator
    {
        public const int MaxBytes = 64 * 1024;
        public const int MaxStrokes = 100;
        public const int MaxPointsPerStroke = 2000;
        public const int MaxTotalPoints = 5000;
        public const double MinWidth = 0.5;
        public const double MaxWidth = 50;
        public const double MaxAbsCoordinate = 100_000;
        public const double MaxCanvasSize = 10_000;

        private static readonly Regex ColorRegex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        // Zwraca null gdy poprawne, w przeciwnym razie krotki opis bledu (po polsku, bez diakrytykow).
        public static string? Validate(string? strokesJson)
        {
            if (string.IsNullOrWhiteSpace(strokesJson)) return "Pole 'strokesJson' jest wymagane.";
            if (Encoding.UTF8.GetByteCount(strokesJson) > MaxBytes)
                return $"Rysunek jest za duzy (maks. {MaxBytes / 1024} KB).";

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(strokesJson, new JsonDocumentOptions { MaxDepth = 8 });
            }
            catch (JsonException)
            {
                return "strokesJson nie jest poprawnym JSON-em.";
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return "strokesJson musi byc obiektem JSON.";

                if (!root.TryGetProperty("strokes", out var strokes) || strokes.ValueKind != JsonValueKind.Array)
                    return "Pole 'strokes' musi byc tablica.";

                var strokeCount = strokes.GetArrayLength();
                if (strokeCount == 0) return "Rysunek nie zawiera zadnej linii.";
                if (strokeCount > MaxStrokes) return $"Za duzo linii (maks. {MaxStrokes}).";

                var totalPoints = 0;
                foreach (var stroke in strokes.EnumerateArray())
                {
                    if (stroke.ValueKind != JsonValueKind.Object) return "Kazda linia musi byc obiektem.";

                    if (!stroke.TryGetProperty("color", out var color)
                        || color.ValueKind != JsonValueKind.String
                        || !ColorRegex.IsMatch(color.GetString()!))
                        return "Kolor linii musi miec format #rrggbb.";

                    if (!stroke.TryGetProperty("width", out var width)
                        || !TryNumber(width, out var w)
                        || w < MinWidth || w > MaxWidth)
                        return $"Grubosc linii musi byc liczba z zakresu {MinWidth}-{MaxWidth}.";

                    if (!stroke.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array)
                        return "Pole 'points' linii musi byc tablica.";

                    var pointCount = points.GetArrayLength();
                    if (pointCount < 2) return "Kazda linia musi miec co najmniej 2 punkty.";
                    if (pointCount > MaxPointsPerStroke) return $"Za duzo punktow w linii (maks. {MaxPointsPerStroke}).";

                    totalPoints += pointCount;
                    if (totalPoints > MaxTotalPoints) return $"Za duzo punktow w rysunku (maks. {MaxTotalPoints}).";

                    foreach (var point in points.EnumerateArray())
                    {
                        if (point.ValueKind != JsonValueKind.Object
                            || !point.TryGetProperty("x", out var x) || !TryNumber(x, out var px)
                            || !point.TryGetProperty("y", out var y) || !TryNumber(y, out var py))
                            return "Kazdy punkt musi miec liczbowe pola 'x' i 'y'.";
                        if (Math.Abs(px) > MaxAbsCoordinate || Math.Abs(py) > MaxAbsCoordinate)
                            return "Wspolrzedne punktu sa poza dozwolonym zakresem.";
                    }
                }

                // canvasWidth/canvasHeight sa opcjonalne, ale jesli sa - musza byc rozsadnymi liczbami
                foreach (var name in new[] { "canvasWidth", "canvasHeight" })
                {
                    if (root.TryGetProperty(name, out var size)
                        && (!TryNumber(size, out var s) || s < 1 || s > MaxCanvasSize))
                        return $"Pole '{name}' musi byc liczba z zakresu 1-{MaxCanvasSize}.";
                }
            }

            return null;
        }

        private static bool TryNumber(JsonElement el, out double value)
        {
            value = 0;
            return el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value) && double.IsFinite(value);
        }
    }
}
