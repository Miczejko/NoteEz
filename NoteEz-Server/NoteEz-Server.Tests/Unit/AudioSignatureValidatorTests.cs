using NoteEz_Server.Services;
using Xunit;

namespace NoteEz_Server.Tests.Unit
{
    public class AudioSignatureValidatorTests
    {
        [Theory]
        [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0 })] // WebM/Matroska (EBML)
        [InlineData(new byte[] { (byte)'O', (byte)'g', (byte)'g', (byte)'S', 0, 0, 0, 0 })] // OGG
        [InlineData(new byte[] {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'W', (byte)'A', (byte)'V', (byte)'E'
        })] // WAV
        [InlineData(new byte[] { 0, 0, 0, 0, (byte)'f', (byte)'t', (byte)'y', (byte)'p' })] // MP4/M4A
        [InlineData(new byte[] { (byte)'I', (byte)'D', (byte)'3', 0, 0, 0, 0, 0 })] // MP3 with ID3 tag
        [InlineData(new byte[] { 0xFF, 0xFB, 0, 0 })] // MP3 raw frame sync
        public void IsRecognizedAudioContainer_AcceptsRealAudioHeaders(byte[] header)
        {
            Assert.True(AudioSignatureValidator.IsRecognizedAudioContainer(header));
        }

        [Theory]
        [InlineData(new byte[] { (byte)'<', (byte)'h', (byte)'t', (byte)'m', (byte)'l' })] // spoofed HTML
        [InlineData(new byte[] { 0x4D, 0x5A, 0, 0 })] // Windows PE/EXE ("MZ")
        [InlineData(new byte[] { 0, 0, 0 })] // garbage
        public void IsRecognizedAudioContainer_RejectsNonAudioHeaders(byte[] header)
        {
            Assert.False(AudioSignatureValidator.IsRecognizedAudioContainer(header));
        }

        [Fact]
        public void IsRecognizedAudioContainer_RejectsTooShortHeader()
        {
            Assert.False(AudioSignatureValidator.IsRecognizedAudioContainer(new byte[] { 0xFF }));
        }
    }
}
