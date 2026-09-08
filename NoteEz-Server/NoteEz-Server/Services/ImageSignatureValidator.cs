namespace NoteEz_Server.Services
{
    // Magic-bytes check dla zdjec paragonow (patrz AudioSignatureValidator - ten sam powod:
    // Content-Type z formularza to tylko deklaracja klienta, nie dowod rzeczywistej zawartosci).
    public static class ImageSignatureValidator
    {
        public const int RequiredHeaderBytes = 12;

        public static bool IsRecognizedImage(ReadOnlySpan<byte> header)
        {
            if (header.Length < 4) return false;

            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 'P' && header[2] == 'N' && header[3] == 'G'
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                return true;

            // WebP: "RIFF" .... "WEBP"
            if (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                return true;

            return false;
        }

        // Media type Claude Vision oczekuje w Base64ImageSource - wnioskowany z tych samych
        // magic bytes, nie z deklaracji klienta.
        public static string? DetectMediaType(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return "image/jpeg";

            if (header.Length >= 8 && header[0] == 0x89 && header[1] == 'P' && header[2] == 'N' && header[3] == 'G')
                return "image/png";

            if (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                return "image/webp";

            return null;
        }
    }
}
