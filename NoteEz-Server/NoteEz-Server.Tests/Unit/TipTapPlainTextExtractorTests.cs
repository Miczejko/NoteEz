using NoteEz_Server.Services;
using Xunit;

namespace NoteEz_Server.Tests.Unit
{
    public class TipTapPlainTextExtractorTests
    {
        [Fact]
        public void Extract_NullOrWhitespace_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TipTapPlainTextExtractor.Extract(null));
            Assert.Equal(string.Empty, TipTapPlainTextExtractor.Extract("   "));
        }

        [Fact]
        public void Extract_LegacyPlainTextNote_ReturnsAsIs()
        {
            // Notatki sprzed wprowadzenia TipTap nie sa poprawnym JSON-em.
            var result = TipTapPlainTextExtractor.Extract("po prostu zwykly tekst");
            Assert.Equal("po prostu zwykly tekst", result);
        }

        [Fact]
        public void Extract_SimpleParagraph_ReturnsText()
        {
            var json = """
            {
              "type": "doc",
              "content": [
                { "type": "paragraph", "content": [ { "type": "text", "text": "Hello world" } ] }
              ]
            }
            """;

            Assert.Equal("Hello world", TipTapPlainTextExtractor.Extract(json));
        }

        [Fact]
        public void Extract_MultipleParagraphs_SeparatesWithNewline()
        {
            var json = """
            {
              "type": "doc",
              "content": [
                { "type": "paragraph", "content": [ { "type": "text", "text": "Line one" } ] },
                { "type": "paragraph", "content": [ { "type": "text", "text": "Line two" } ] }
              ]
            }
            """;

            var result = TipTapPlainTextExtractor.Extract(json);
            Assert.Equal("Line one\nLine two", result);
        }

        [Fact]
        public void Extract_ValidJsonButNotTipTapShape_DoesNotThrow()
        {
            // Poprawny JSON, ale bez oczekiwanych pol "type"/"content" - nie powinno wywalic wyjatku.
            var result = TipTapPlainTextExtractor.Extract("""{ "foo": "bar", "nested": [1,2,3] }""");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Extract_DeeplyNestedContent_DoesNotStackOverflow()
        {
            // Notatka jest wlasnoscia jej wlasciciela, ale i tak warto miec pewnosc,
            // ze mocno zagniezdzony dokument nie wywala procesu (samoDoS).
            var depth = 500;
            var sb = new System.Text.StringBuilder();
            sb.Append("""{"type":"doc","content":[""");
            for (int i = 0; i < depth; i++)
                sb.Append("""{"type":"paragraph","content":[""");
            sb.Append("""{"type":"text","text":"deep"}""");
            for (int i = 0; i < depth; i++)
                sb.Append("]}");
            sb.Append("]}");

            var result = TipTapPlainTextExtractor.Extract(sb.ToString());
            Assert.Contains("deep", result);
        }
    }
}
