# NoteEz Frontend

Vue.js 3 frontend dla aplikacji NoteEz — notatnik online z obsługą tekstu, rysunków wektorowych i głosówek.

## Wymagania

- Node.js 18+
- Działający backend NoteEz-Server (`http://localhost:5112`)

## Uruchomienie

```bash
cd NoteEz-Frontend
npm install
npm run dev
```

Frontend startuje na `http://localhost:5173`. Żądania do `/api/*` są proxy'owane na backend.

## Funkcje

- **Logowanie / rejestracja** — JWT access token + refresh token (cookie)
- **Notatki tekstowe** — CRUD z auto-zapisem
- **Rysunki wektorowe** — Canvas API, dane jako JSON (strokes)
- **Głosówki** — nagrywanie WebM/Opus, upload i odtwarzanie ze streamu API

## Endpointy API

| Metoda | Ścieżka | Opis |
|--------|---------|------|
| POST | `/api/auth/register` | Rejestracja |
| POST | `/api/auth/login` | Logowanie |
| POST | `/api/auth/logout` | Wylogowanie |
| GET | `/api/notes` | Lista notatek |
| GET | `/api/notes/{id}` | Szczegóły notatki |
| POST | `/api/notes` | Nowa notatka |
| PUT | `/api/notes/{id}` | Edycja |
| DELETE | `/api/notes/{id}` | Usunięcie |
| POST | `/api/notes/{id}/drawings` | Dodaj rysunek |
| DELETE | `/api/notes/{id}/drawings/{drawingId}` | Usuń rysunek |
| POST | `/api/notes/{id}/audio` | Upload głosówki |
| GET | `/api/notes/{id}/audio/{audioId}/stream` | Stream audio |
| DELETE | `/api/notes/{id}/audio/{audioId}` | Usuń głosówkę |

## Paleta kolorów

- `#54428e` — primary (fiolet)
- `#8963ba` — secondary (lawenda)
- `#afe3c0` — accent mint
- `#90c290` — accent sage

## Build produkcyjny

```bash
npm run build
npm run preview
```

Ustaw zmienną `VITE_API_URL` na URL produkcyjnego backendu.
