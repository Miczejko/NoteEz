# API urządzenia (ESP32) — endpointy `device-notes`

Wszystkie endpointy poniżej: `DeviceNotesController` (`NoteEz-Server/NoteEz-Server/Controllers/DeviceNotesController.cs`),
uwierzytelnienie nagłówkiem **`X-Api-Key`** (schemat `DeviceApiKey`, klucz wydawany przy parowaniu urządzenia).
Bez poprawnego klucza: `401`. Notatka cudza lub nieistniejąca: `404` (backend nie ujawnia, że istnieje).
Dostęp do notatki = właściciel lub członek grupy, do której notatka należy (`NoteService.CanEditAsync`).

Urządzenie komunikuje się przez Cloudflare Worker (`noteez.online/api/*` → `api.noteez.online`), który przepuszcza
dowolną metodę HTTP. Lokalnie (surowy `IP:port`) firmware używa zwykłego HTTP, dla domeny — HTTPS.

## Odczyt

| Metoda i ścieżka | Opis |
|---|---|
| `GET /api/device-notes/lite` | lista notatek (id, tytuł, czy ma rysunek/audio, kolor) |
| `GET /api/device-notes/{id}` | pełna notatka: `title`, `textContent` (tekst), `textBlocks`, `drawings`, `audioClips`, `updatedAt` |

`textBlocks[]` to uproszczona lista bloków wyciągnięta z TipTap JSON: `{type, text, checked, color, bold, taskIndex}`.
`type`: `paragraph`, `blockquote`, `codeBlock`, `listItem`, `taskItem`. `taskIndex` ma tylko `taskItem`.

## Zmiana checkboxa — `PUT /api/device-notes/{id}/tasks/{index}`

Body: `{ "checked": true }`. **Idempotentne** (ustawia docelowy stan, nie przełącza), więc ponowienie jest bezpieczne.

- `index` = numer taska w kolejności dokumentu (0, 1, 2…), liczony „w głąb”: najpierw task, potem taski zagnieżdżone w jego
  treści, potem następny. Numerację robi jedna funkcja (`TipTapTasks.EnumerateTaskItems`), używana zarówno przez
  `TipTapBlockExtractor` (numery wysyłane do urządzenia), jak i przez endpoint — kolejność nie może się rozjechać.
- Backend zmienia tylko `attrs.checked` właściwego `taskItem` w `Note.TextContent`, ustawia `UpdatedAt` i nowy
  `RowVersion` (Postgres sam go nie generuje). Aplikacja webowa widzi zmianę po ponownym wczytaniu notatki.
- Odpowiedzi: `200 {"checked":bool}`; `400` zły indeks / brak lub nie-bool `checked` / `TextContent` nie jest dokumentem
  TipTap (stara zwykła treść); `404`; `409` przy kolizji zapisu (po jednej automatycznej powtórce).
- Notatka grupowa: dodatkowo SignalR `NoteChanged` (grupa `group-{id}` i `note-{id}`).

## Nowy rysunek — `POST /api/device-notes/{id}/drawings`

Body: `{ "strokesJson": "<string z JSON-em rysunku>" }`, limit ciała 100 KB (`RequestSizeLimit`).

Format `strokesJson` jest taki sam jak w aplikacji webowej (`DrawingCanvas.vue`):
```json
{"strokes":[{"color":"#d64545","width":3,"points":[{"x":10,"y":12},{"x":40,"y":55}]}],"canvasWidth":320,"canvasHeight":172}
```

Backend (`NoteService.AddDeviceDrawingAsync`):
1. sprawdza dostęp (`404`), waliduje `strokesJson` (`DrawingStrokesValidator`, `400`), sprawdza limit rysunków (`400`),
2. tworzy `NoteDrawing` (`SortOrder` = liczba istniejących rysunków),
3. **dopisuje na koniec treści węzeł** `{"type":"drawingBlock","attrs":{"drawingId":"<guid>"}}` do `Note.TextContent`
   — bez niego aplikacja webowa nie pokazałaby rysunku. Dla pustej treści tworzy dokument; dla starej zwykłej treści
   zamienia ją na akapity (tekst nie ginie); dla dokumentu TipTap reszta zostaje bez zmian,
4. ustawia `UpdatedAt` i nowy `RowVersion`, zapisuje wszystko jednym `SaveChanges` (jedna transakcja; jedna powtórka
   przy kolizji, potem `409`).

Odpowiedź: `201 Created` z `{id, strokesJson, sortOrder}`. Notatka grupowa: SignalR `NoteChanged`.

Limity serwera (celowo luźniejsze niż w firmware): ≤ 64 KB, ≤ 100 linii, 2…2000 punktów na linię, ≤ 5000 punktów
łącznie, kolor `#rrggbb`, `width` 0,5…50, współrzędne skończone i `|v| ≤ 100000`, `canvasWidth/Height` opcjonalne
(1…10000), **maks. 20 rysunków na notatkę**. Brak idempotencji: ponowienie po zgubionej odpowiedzi utworzy duplikat.

## Usuwanie

`DELETE /api/device-notes/{id}` — istnieje w kontrolerze (komentarz w kodzie: docelowo do zabezpieczenia kluczem
urządzenia); firmware go nie używa.

## Testy

`NoteEz-Server.Tests`: `Unit/NoteServiceTests.cs`, `Unit/TipTapBlockExtractorTests.cs`,
`Integration/DeviceNotesControllerTests.cs` (pełny przepływ: rejestracja → JWT → parowanie → klucz `X-Api-Key` → żądania).
Testy używają EF Core InMemory, więc **powtórka przy kolizji `RowVersion` nie jest sprawdzona na prawdziwym Postgresie**.
