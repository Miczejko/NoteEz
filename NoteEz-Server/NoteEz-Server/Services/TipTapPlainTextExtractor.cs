using System.Text;
using System.Text.Json;

namespace NoteEz_Server.Services
{
    // Flattens TipTap document JSON into plain text for consumers that can't render rich text (e.g. the ESP32 device).
    public static class TipTapPlainTextExtractor
    {
        public static string Extract(string? tiptapJson)
        {
            if (string.IsNullOrWhiteSpace(tiptapJson))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(tiptapJson);
                var sb = new StringBuilder();
                Walk(doc.RootElement, sb);
                return sb.ToString().Trim();
            }
            catch (JsonException)
            {
                // Not TipTap JSON (e.g. legacy plain-text note) - return as-is.
                return tiptapJson;
            }
        }

        private static void Walk(JsonElement node, StringBuilder sb)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            if (node.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "text"
                && node.TryGetProperty("text", out var textEl))
            {
                sb.Append(textEl.GetString());
            }

            if (node.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in contentEl.EnumerateArray())
                    Walk(child, sb);
            }

            if (node.TryGetProperty("type", out var blockType))
            {
                var t = blockType.GetString();
                if (t is "paragraph" or "heading" or "listItem" or "taskItem" or "hardBreak")
                    sb.Append('\n');
            }
        }
    }
}
