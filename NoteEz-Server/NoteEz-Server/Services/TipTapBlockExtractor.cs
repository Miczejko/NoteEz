using System.Text;
using System.Text.Json;

namespace NoteEz_Server.Services
{
    // A single line of device-renderable text with just enough formatting for the ESP32 to draw.
    public record TextBlockDto(string Type, string Text, bool? Checked, string? Color, bool Bold);

    // Flattens TipTap document JSON into a simple block list the ESP32 firmware can render
    // (bold, a single text color, and checkbox state) without having to parse the full TipTap schema.
    public static class TipTapBlockExtractor
    {
        public static List<TextBlockDto> Extract(string? tiptapJson)
        {
            var blocks = new List<TextBlockDto>();
            if (string.IsNullOrWhiteSpace(tiptapJson))
                return blocks;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(tiptapJson);
            }
            catch (JsonException)
            {
                // Legacy plain-text note.
                blocks.Add(new TextBlockDto("paragraph", tiptapJson, null, null, false));
                return blocks;
            }

            using (doc)
            {
                if (doc.RootElement.TryGetProperty("content", out var top) && top.ValueKind == JsonValueKind.Array)
                {
                    foreach (var node in top.EnumerateArray())
                        WalkBlock(node, blocks);
                }
            }

            return blocks;
        }

        private static void WalkBlock(JsonElement node, List<TextBlockDto> blocks)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("type", out var typeEl))
                return;

            switch (typeEl.GetString())
            {
                case "paragraph":
                case "heading":
                    {
                        var (text, color, bold) = ExtractRuns(node);
                        if (text.Length > 0)
                            blocks.Add(new TextBlockDto("paragraph", text, null, color, bold || typeEl.GetString() == "heading"));
                        break;
                    }
                case "blockquote":
                    if (node.TryGetProperty("content", out var bqContent) && bqContent.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in bqContent.EnumerateArray())
                        {
                            var (text, color, bold) = ExtractRuns(child);
                            if (text.Length > 0)
                                blocks.Add(new TextBlockDto("blockquote", text, null, color, bold));
                        }
                    }
                    break;
                case "codeBlock":
                    {
                        var (text, _, _) = ExtractRuns(node);
                        if (text.Length > 0)
                            blocks.Add(new TextBlockDto("codeBlock", text, null, null, false));
                        break;
                    }
                case "bulletList":
                    if (node.TryGetProperty("content", out var blContent))
                        foreach (var item in blContent.EnumerateArray())
                            WalkListItem(item, blocks, ordered: false, number: 0);
                    break;
                case "orderedList":
                    if (node.TryGetProperty("content", out var olContent))
                    {
                        var number = 1;
                        foreach (var item in olContent.EnumerateArray())
                            WalkListItem(item, blocks, ordered: true, number: number++);
                    }
                    break;
                case "taskList":
                    if (node.TryGetProperty("content", out var tlContent))
                        foreach (var item in tlContent.EnumerateArray())
                            WalkTaskItem(item, blocks);
                    break;
                default:
                    // drawingBlock and anything else unsupported on-device: no text.
                    break;
            }
        }

        private static void WalkListItem(JsonElement item, List<TextBlockDto> blocks, bool ordered, int number)
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return;

            var first = true;
            foreach (var para in content.EnumerateArray())
            {
                var (text, color, bold) = ExtractRuns(para);
                if (text.Length == 0) continue;

                var prefix = first ? (ordered ? $"{number}. " : "- ") : "  ";
                blocks.Add(new TextBlockDto("listItem", prefix + text, null, color, bold));
                first = false;
            }
        }

        private static void WalkTaskItem(JsonElement item, List<TextBlockDto> blocks)
        {
            bool? isChecked = item.TryGetProperty("attrs", out var attrs)
                && attrs.TryGetProperty("checked", out var ch)
                && (ch.ValueKind == JsonValueKind.True || ch.ValueKind == JsonValueKind.False)
                ? ch.GetBoolean()
                : null;

            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                return;

            foreach (var para in content.EnumerateArray())
            {
                var (text, color, bold) = ExtractRuns(para);
                if (text.Length == 0) continue;
                blocks.Add(new TextBlockDto("taskItem", text, isChecked, color, bold));
            }
        }

        // Flattens a paragraph-like node's inline content into one string, plus the block's
        // overall color (first non-null textStyle color found) and bold (true if any run is bold).
        private static (string text, string? color, bool bold) ExtractRuns(JsonElement node)
        {
            var sb = new StringBuilder();
            string? color = null;
            var bold = false;

            if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in content.EnumerateArray())
                {
                    if (!child.TryGetProperty("type", out var ct)) continue;
                    var ctype = ct.GetString();

                    if (ctype == "hardBreak")
                    {
                        sb.Append(' ');
                        continue;
                    }
                    if (ctype != "text") continue;

                    if (child.TryGetProperty("text", out var textEl))
                        sb.Append(textEl.GetString());

                    if (child.TryGetProperty("marks", out var marks) && marks.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mark in marks.EnumerateArray())
                        {
                            if (!mark.TryGetProperty("type", out var mt)) continue;
                            var mtype = mt.GetString();

                            if (mtype == "bold") bold = true;

                            if (mtype == "textStyle"
                                && mark.TryGetProperty("attrs", out var markAttrs)
                                && markAttrs.TryGetProperty("color", out var colorEl)
                                && colorEl.ValueKind == JsonValueKind.String)
                            {
                                color ??= colorEl.GetString();
                            }
                        }
                    }
                }
            }

            return (sb.ToString(), color, bold);
        }
    }
}
