namespace NoteEz_Server.Services
{
    // Sprawdza "magic bytes" (naglowek pliku) zamiast ufac wylacznie naglowkowi Content-Type,
    // ktory klient moze dowolnie sfalszowac (np. wgrac plik .html oznaczony jako audio/webm).
    // Nie wymaga zewnetrznej biblioteki - rozpoznaje tylko kontenery, ktore faktycznie
    // dopuszczamy do wgrania jako glosowki.
    public static class AudioSignatureValidator
    {
        // liczba bajtow naglowka wystarczajaca do rozpoznania kazdego z ponizszych formatow
        public const int RequiredHeaderBytes = 12;

        public static bool IsRecognizedAudioContainer(ReadOnlySpan<byte> header)
        {
            if (header.Length < 4) return false;

            // WebM/Matroska (EBML): 1A 45 DF A3
            if (header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
                return true;

            // OGG (Opus/Vorbis): "OggS"
            if (header[0] == 'O' && header[1] == 'g' && header[2] == 'g' && header[3] == 'S')
                return true;

            // WAV: "RIFF" .... "WAVE"
            if (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'A' && header[10] == 'V' && header[11] == 'E')
                return true;

            // MP4/M4A: box type "ftyp" na offsecie 4
            if (header.Length >= 8 && header[4] == 'f' && header[5] == 't' && header[6] == 'y' && header[7] == 'p')
                return true;

            // MP3 z tagiem ID3v2: "ID3"
            if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
                return true;

            // MP3 bez ID3 - ramka MPEG audio zaczyna sie od 11 jedynkowych bitow (sync word)
            if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                return true;

            return false;
        }
    }
}
