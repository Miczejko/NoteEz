# NoteEz

Notatnik online z klientem Vue.js (PWA) i planowanym urządzeniem ESP32. Szczegółowy opis projektu i architektury: [opis_projektu_notatnik.md](opis_projektu_notatnik.md).

## Wymagania

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (backend)
- [Node.js 20+](https://nodejs.org/) (frontend)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (baza SQL Server + opcjonalne uruchomienie API w kontenerze)
- Konto Azure ze skonfigurowanym Blob Storage (na potrzeby uploadu głosówek) — do developmentu wystarczy connection string do istniejącego kontenera

## 1. Backend (`NoteEz-Server`)

### 1.1 Konfiguracja `.env`

Utwórz plik `NoteEz-Server/.env` (jest w `.gitignore`, nie commituj go) na podstawie poniższego wzoru:

```env
DB_PASSWORD=TwojeHaslo123!
DB_CONNECTION=Server=sql-noteez,1433;Database=NoteEz;User Id=sa;Password=TwojeHaslo123!;TrustServerCertificate=True;
JWT_KEY=dlugi-losowy-klucz-min-32-znaki
JWT_ISSUER=NoteEzApi
JWT_AUDIENCE=NoteEzApi

AZURE_BLOB_CONNECTION_STRING=<connection string z Azure Portal>
AZURE_BLOB_CONTAINER_NAME=audio-notes

CORS_ALLOWED_ORIGIN=http://localhost:5173
```

### 1.2 Uruchomienie przez Docker Compose (zalecane)

Stawia jednocześnie SQL Server i API w kontenerach:

```bash
cd NoteEz-Server
docker compose up -d --build
```

API będzie dostępne pod `http://localhost:8080`, SQL Server pod `localhost:1433`.

Zatrzymanie: `docker compose down` (dane bazy zostają w wolumenie `sql_data`, `docker compose down -v` usunie je całkowicie).

### 1.3 Migracje bazy danych

Migracje EF Core nie są aplikowane automatycznie przy starcie aplikacji — trzeba je uruchomić ręcznie po pierwszym postawieniu bazy:

```bash
dotnet tool install --global dotnet-ef   # jednorazowo, jeśli nie masz
cd NoteEz-Server/NoteEz-Server
dotnet ef database update --connection "Server=localhost,1433;Database=NoteEz;User Id=sa;Password=<DB_PASSWORD z .env>;TrustServerCertificate=True;"
```

### 1.4 Uruchomienie lokalnie bez Dockera (opcjonalnie)

Jeśli wolisz odpalić samo API poza kontenerem (np. do debugowania w IDE), potrzebujesz działającego SQL Servera (można zostawić tylko `sql-noteez` z docker-compose: `docker compose up -d sql-noteez`) i zmiennych z `.env` ustawionych jako zmienne środowiskowe lub w `appsettings.Development.json`:

```bash
cd NoteEz-Server/NoteEz-Server
dotnet run
```

Swagger UI (tryb Development): `http://localhost:5112/swagger` (profil `http` z `launchSettings.json`; profil `https` dodatkowo wystawia `https://localhost:7070`).

## 2. Frontend (`NoteEz-Frontend`)

```bash
cd NoteEz-Frontend
npm install
npm run dev
```

Aplikacja wystartuje pod `http://localhost:5173`. Dev-server proxy'uje `/api` na `http://localhost:8080` (patrz `vite.config.js`), więc backend musi już działać.

Plik `NoteEz-Frontend/.env` (domyślnie już ustawiony):

```env
VITE_API_URL=/api
```

Build produkcyjny: `npm run build` (output w `dist/`).

## 3. Szybki test end-to-end

1. Backend + baza działają (`docker compose up -d --build` w `NoteEz-Server`, migracje zaaplikowane).
2. Frontend działa (`npm run dev` w `NoteEz-Frontend`).
3. Wejdź na `http://localhost:5173`, zarejestruj konto (`/register`), zaloguj się.
4. Utwórz notatkę, dodaj rysunek/głosówkę — sprawdza to pełny przepływ API + Blob Storage.

## Struktura repo

- `NoteEz-Server/` — backend ASP.NET Core (REST API, EF Core, Azure Blob Storage)
- `NoteEz-Frontend/` — frontend Vue.js (PWA)
- `opis_projektu_notatnik.md` — pełny opis architektury i planu projektu (w tym integracja z ESP32)
