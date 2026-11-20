using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoteEz_Server.Services
{
    public enum SetTaskStatus { Ok, NotFound, InvalidContent, IndexOutOfRange }

    // Jedyne miejsce, ktore wie "jak numerowac taski w dokumencie TipTap". Uzywane zarowno przez
    // TipTapBlockExtractor (nadaje taskIndex blokom wysylanym do ESP32) jak i przez przelaczanie
    // checkboxa (PUT /api/device-notes/{id}/tasks/{index}) - dzieki temu indeksy nie moga sie rozjechac.
    //
    // Numeracja: kazdy wezel typu "taskItem" w dokumencie, w kolejnosci przejscia "w glab"
    // (pre-order): najpierw sam task, potem taski zagniezdzone w jego tresci, potem kolejny task
    // na tym samym poziomie. Schodzimy tylko po tablicach "content" (struktura TipTap).
    public static class TipTapTasks
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            // bez tego polskie znaki zostalyby zapisane jako \uXXXX (semantycznie to samo, ale
            // niepotrzebnie zmienialoby tresc notatki w bazie)
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Zwraca korzen dokumentu albo null, gdy tekst nie jest obiektem JSON (np. stara,
        // zwykla tresc notatki).
        public static JsonObject? ParseDocument(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string? TypeOf(JsonNode? node) =>
            node is JsonObject obj && obj["type"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        // Elementy tablicy "content" wezla, ktore sa obiektami.
        public static IEnumerable<JsonObject> Children(JsonNode? node)
        {
            if (node is JsonObject obj && obj["content"] is JsonArray arr)
            {
                foreach (var child in arr)
                    if (child is JsonObject c) yield return c;
            }
        }

        // Wszystkie taskItem w dokumencie, w kolejnosci numeracji (indeks na liscie == taskIndex).
        public static List<JsonObject> EnumerateTaskItems(JsonObject root)
        {
            var items = new List<JsonObject>();
            Collect(root, items);
            return items;
        }

        private static void Collect(JsonObject node, List<JsonObject> items)
        {
            if (TypeOf(node) == "taskItem") items.Add(node);
            foreach (var child in Children(node))
                Collect(child, items);
        }

        // Idempotentnie ustawia attrs.checked taska o danym indeksie; reszta dokumentu zostaje bez zmian.
        public static (SetTaskStatus Status, string? NewJson) SetChecked(string? json, int index, bool isChecked)
        {
            var root = ParseDocument(json);
            if (root is null) return (SetTaskStatus.InvalidContent, null);

            var items = EnumerateTaskItems(root);
            if (index < 0 || index >= items.Count) return (SetTaskStatus.IndexOutOfRange, null);

            var item = items[index];
            if (item["attrs"] is not JsonObject attrs)
            {
                attrs = new JsonObject();
                item["attrs"] = attrs;
            }
            attrs["checked"] = isChecked;

            return (SetTaskStatus.Ok, root.ToJsonString(WriteOptions));
        }

        // Dopisuje na koncu dokumentu wezel {"type":"drawingBlock","attrs":{"drawingId":"<guid>"}} -
        // dzieki niemu aplikacja www pokazuje rysunek (sam wiersz NoteDrawing nie wystarcza).
        // - pusta/null tresc -> nowy dokument zawierajacy tylko blok rysunku,
        // - stara tresc nie bedaca dokumentem TipTap (zwykly tekst) -> dokument z akapitem na kazda
        //   linie tekstu + blok rysunku (tekst nie ginie),
        // - dokument TipTap -> reszta bez zmian, blok dopisany na koncu tablicy "content".
        public static string AppendDrawingBlock(string? json, Guid drawingId)
        {
            var block = new JsonObject
            {
                ["type"] = "drawingBlock",
                ["attrs"] = new JsonObject { ["drawingId"] = drawingId.ToString() }
            };

            JsonObject root;
            var parsed = ParseDocument(json);
            if (string.IsNullOrWhiteSpace(json))
            {
                root = NewDocument();
            }
            else if (parsed is not null && TypeOf(parsed) == "doc")
            {
                root = parsed;
                if (root["content"] is not JsonArray)
                    root["content"] = new JsonArray();
            }
            else
            {
                root = NewDocument();
                var paragraphs = (JsonArray)root["content"]!;
                foreach (var line in json!.Replace("\r\n", "\n").Split('\n'))
                {
                    var paragraph = new JsonObject { ["type"] = "paragraph" };
                    if (line.Length > 0)
                        paragraph["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = line });
                    paragraphs.Add(paragraph);
                }
            }

            ((JsonArray)root["content"]!).Add(block);
            return root.ToJsonString(WriteOptions);
        }

        private static JsonObject NewDocument() => new()
        {
            ["type"] = "doc",
            ["content"] = new JsonArray()
        };
    }
}
