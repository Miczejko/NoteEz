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
    }
}
