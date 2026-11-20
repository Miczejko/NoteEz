using NoteEz_Server.Services;
using Xunit;

namespace NoteEz_Server.Tests.Unit
{
    public class TipTapBlockExtractorTests
    {
        [Fact]
        public void Extract_NullOrWhitespace_ReturnsEmptyList()
        {
            Assert.Empty(TipTapBlockExtractor.Extract(null));
            Assert.Empty(TipTapBlockExtractor.Extract("  "));
        }

        [Fact]
        public void Extract_LegacyPlainTextNote_ReturnsSingleParagraphBlock()
        {
            var blocks = TipTapBlockExtractor.Extract("stary tekst bez JSON-a");

            var block = Assert.Single(blocks);
            Assert.Equal("paragraph", block.Type);
            Assert.Equal("stary tekst bez JSON-a", block.Text);
        }

        [Fact]
        public void Extract_TaskItem_CarriesCheckedState()
        {
            var json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "taskList",
                  "content": [
                    {
                      "type": "taskItem",
                      "attrs": { "checked": true },
                      "content": [
                        { "type": "paragraph", "content": [ { "type": "text", "text": "Kup mleko" } ] }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

            var block = Assert.Single(TipTapBlockExtractor.Extract(json));
            Assert.Equal("taskItem", block.Type);
            Assert.Equal("Kup mleko", block.Text);
            Assert.True(block.Checked);
        }

        [Fact]
        public void Extract_BoldMark_SetsBoldTrue()
        {
            var json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    { "type": "text", "text": "wazne", "marks": [ { "type": "bold" } ] }
                  ]
                }
              ]
            }
            """;

            var block = Assert.Single(TipTapBlockExtractor.Extract(json));
            Assert.True(block.Bold);
            Assert.Equal("wazne", block.Text);
        }

        [Fact]
        public void Extract_TextStyleColorMark_CarriesColor()
        {
            var json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    {
                      "type": "text",
                      "text": "czerwony",
                      "marks": [ { "type": "textStyle", "attrs": { "color": "#ff0000" } } ]
                    }
                  ]
                }
              ]
            }
            """;

            var block = Assert.Single(TipTapBlockExtractor.Extract(json));
            Assert.Equal("#ff0000", block.Color);
        }

        [Fact]
        public void Extract_MalformedJson_DoesNotThrow()
        {
            var blocks = TipTapBlockExtractor.Extract("{ not: valid json ][");
            Assert.Single(blocks); // traktowane jak legacy plain-text
        }

        [Fact]
        public void Extract_UnsupportedBlockType_ProducesNoBlock()
        {
            // np. drawingBlock - urzadzenie nie potrafi tego wyrenderowac jako tekst
            var json = """
            {
              "type": "doc",
              "content": [ { "type": "drawingBlock", "attrs": { "drawingId": "abc" } } ]
            }
            """;

            Assert.Empty(TipTapBlockExtractor.Extract(json));
        }
    
        // taski: A (0), A1 zagniezdzony (1), A2 zagniezdzony (2), B (3, dwa paragrafy)
        private const string NestedTasksDoc = """
        {
          "type": "doc",
          "content": [
            { "type": "paragraph", "content": [ { "type": "text", "text": "Naglowek" } ] },
            {
              "type": "taskList",
              "content": [
                {
                  "type": "taskItem",
                  "attrs": { "checked": false },
                  "content": [
                    { "type": "paragraph", "content": [ { "type": "text", "text": "A" } ] },
                    {
                      "type": "taskList",
                      "content": [
                        {
                          "type": "taskItem",
                          "attrs": { "checked": true },
                          "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "A1" } ] } ]
                        },
                        {
                          "type": "taskItem",
                          "attrs": { "checked": false },
                          "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "A2" } ] } ]
                        }
                      ]
                    }
                  ]
                },
                {
                  "type": "taskItem",
                  "attrs": { "checked": false },
                  "content": [
                    { "type": "paragraph", "content": [ { "type": "text", "text": "B" } ] },
                    { "type": "paragraph", "content": [ { "type": "text", "text": "B dalej" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

        [Fact]
        public void Extract_NestedTaskLists_AssignsDocumentOrderTaskIndexes()
        {
            var tasks = TipTapBlockExtractor.Extract(NestedTasksDoc).Where(b => b.Type == "taskItem").ToList();

            Assert.Collection(tasks,
                t => { Assert.Equal("A", t.Text); Assert.Equal(0, t.TaskIndex); Assert.False(t.Checked); },
                t => { Assert.Equal("A1", t.Text); Assert.Equal(1, t.TaskIndex); Assert.True(t.Checked); },
                t => { Assert.Equal("A2", t.Text); Assert.Equal(2, t.TaskIndex); },
                t => { Assert.Equal("B", t.Text); Assert.Equal(3, t.TaskIndex); },
                t => { Assert.Equal("B dalej", t.Text); Assert.Equal(3, t.TaskIndex); });
        }

        [Fact]
        public void Extract_NonTaskBlocks_HaveNullTaskIndex()
        {
            var blocks = TipTapBlockExtractor.Extract(NestedTasksDoc);

            Assert.Null(blocks.First(b => b.Text == "Naglowek").TaskIndex);
            Assert.Null(TipTapBlockExtractor.Extract("stary tekst").Single().TaskIndex);
        }

        [Fact]
        public void Extract_TaskIndexes_MatchTipTapTasksNumbering()
        {
            // ten sam dokument, dwie sciezki kodu: ekstraktor (taskIndex dla ESP32) i przelaczanie
            // - indeks z ekstraktora musi wskazywac ten sam task po stronie przelaczania
            foreach (var block in TipTapBlockExtractor.Extract(NestedTasksDoc).Where(b => b.Type == "taskItem"))
            {
                var items = TipTapTasks.EnumerateTaskItems(TipTapTasks.ParseDocument(NestedTasksDoc)!);
                var firstText = items[block.TaskIndex!.Value]["content"]![0]!["content"]![0]!["text"]!.GetValue<string>();
                Assert.StartsWith(firstText, block.Text);
            }
        }
}
}
