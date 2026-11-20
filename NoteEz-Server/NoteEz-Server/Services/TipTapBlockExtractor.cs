using System.Text;
using System.Text.Json.Nodes;

namespace NoteEz_Server.Services
{
    // A single line of device-renderable text with just enough formatting for the ESP32 to draw.
    // TaskIndex is set only for taskItem blocks: the task's position in document order (see
    // TipTapTasks). All paragraphs of one task share the same TaskIndex.
    public record TextBlockDto(string Type, string Text, bool? Checked, string? Color, bool Bold, int? TaskIndex = null);

    // Flattens TipTap document JSON into a simple block list the ESP32 firmware can render
    // (bold, a single text color, and checkbox state) without having to parse the full TipTap schema.
    public static class TipTapBlockExtractor
    {
        public static List<TextBlockDto> Extract(string? tiptapJson)
        {
            var blocks = new List<TextBlockDto>();
            if (string.IsNullOrWhiteSpace(tiptapJson))
                return blocks;

            var root = TipTapTasks.ParseDocument(tiptapJson);
            if (root is null)
            {
                // Legacy plain-text note.
                blocks.Add(new TextBlockDto("paragraph", tiptapJson, null, null, false));
                return blocks;
            }

            // Numeracja taskow pochodzi ze wspolnej funkcji (ta sama, ktorej uzywa endpoint
            // przelaczania), wiec indeks wyslany do ESP32 zawsze wskazuje ten sam task.
            var taskIndexes = new Dictionary<JsonObject, int>(ReferenceEqualityComparer.Instance);
            var items = TipTapTasks.EnumerateTaskItems(root);
            for (var i = 0; i < items.Count; i++) taskIndexes[items[i]] = i;

            foreach (var node in TipTapTasks.Children(root))
                WalkBlock(node, blocks, taskIndexes);

            return blocks;
        }

        private static void WalkBlock(JsonObject node, List<TextBlockDto> blocks, Dictionary<JsonObject, int> taskIndexes)
        {
            var type = TipTapTasks.TypeOf(node);
            switch (type)
            {
                case "paragraph":
                case "heading":
                    {
                        var (text, color, bold) = ExtractRuns(node);
                        if (text.Length > 0)
                            blocks.Add(new TextBlockDto("paragraph", text, null, color, bold || type == "heading"));
                        break;
                    }
                case "blockquote":
                    foreach (var child in TipTapTasks.Children(node))
                    {
                        var (text, color, bold) = ExtractRuns(child);
                        if (text.Length > 0)
                            blocks.Add(new TextBlockDto("blockquote", text, null, color, bold));
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
                    foreach (var item in TipTapTasks.Children(node))
                        WalkListItem(item, blocks, ordered: false, number: 0);
                    break;
                case "orderedList":
                    {
                        var number = 1;
                        foreach (var item in TipTapTasks.Children(node))
                            WalkListItem(item, blocks, ordered: true, number: number++);
                        break;
                    }
                case "taskList":
                    WalkTaskList(node, blocks, taskIndexes);
                    break;
                default:
                    // drawingBlock and anything else unsupported on-device: no text.
                    break;
            }
        }

        private static void WalkListItem(JsonObject item, List<TextBlockDto> blocks, bool ordered, int number)
        {
            var first = true;
            foreach (var para in TipTapTasks.Children(item))
            {
                var (text, color, bold) = ExtractRuns(para);
                if (text.Length == 0) continue;

                var prefix = first ? (ordered ? $"{number}. " : "- ") : "  ";
                blocks.Add(new TextBlockDto("listItem", prefix + text, null, color, bold));
                first = false;
            }
        }

        private static void WalkTaskList(JsonObject taskList, List<TextBlockDto> blocks, Dictionary<JsonObject, int> taskIndexes)
        {
            foreach (var item in TipTapTasks.Children(taskList))
            {
                if (TipTapTasks.TypeOf(item) == "taskItem")
                    WalkTaskItem(item, blocks, taskIndexes);
            }
        }

        private static void WalkTaskItem(JsonObject item, List<TextBlockDto> blocks, Dictionary<JsonObject, int> taskIndexes)
        {
            bool? isChecked = item["attrs"]?["checked"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;
            int? taskIndex = taskIndexes.TryGetValue(item, out var idx) ? idx : null;

            // Tresc taska to paragrafy (wszystkie dziela ten sam taskIndex) oraz - przy
            // TaskItem nested:true w edytorze - zagniezdzone taskList, ktore dostaja wlasne indeksy.
            foreach (var child in TipTapTasks.Children(item))
            {
                if (TipTapTasks.TypeOf(child) == "taskList")
                {
                    WalkTaskList(child, blocks, taskIndexes);
                    continue;
                }

                var (text, color, bold) = ExtractRuns(child);
                if (text.Length == 0) continue;
                blocks.Add(new TextBlockDto("taskItem", text, isChecked, color, bold, taskIndex));
            }
        }

        // Flattens a paragraph-like node's inline content into one string, plus the block's
        // overall color (first non-null textStyle color found) and bold (true if any run is bold).
        private static (string text, string? color, bool bold) ExtractRuns(JsonObject node)
        {
            var sb = new StringBuilder();
            string? color = null;
            var bold = false;

            foreach (var child in TipTapTasks.Children(node))
            {
                var ctype = TipTapTasks.TypeOf(child);

                if (ctype == "hardBreak")
                {
                    sb.Append(' ');
                    continue;
                }
                if (ctype != "text") continue;

                if (child["text"] is JsonValue tv && tv.TryGetValue<string>(out var t))
                    sb.Append(t);

                if (child["marks"] is JsonArray marks)
                {
                    foreach (var mark in marks)
                    {
                        var mtype = TipTapTasks.TypeOf(mark);

                        if (mtype == "bold") bold = true;

                        if (mtype == "textStyle"
                            && mark?["attrs"]?["color"] is JsonValue cv
                            && cv.TryGetValue<string>(out var c))
                        {
                            color ??= c;
                        }
                    }
                }
            }

            return (sb.ToString(), color, bold);
        }
    }
}
