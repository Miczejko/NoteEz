#!/usr/bin/env bash
# Uruchamia backend (NoteEz-Server) jako zwykly proces z dowolnego worktree - nie w Dockerze.
#
# Po co: `dotnet run` sam nie ma konfiguracji (JWT, baza, CORS, Turnstile...), ktora docker-compose.yml
# mapuje ze zmiennych w NoteEz-Server/.env. Ten skrypt ustawia te same zmienne (bez wypisywania wartosci
# sekretow), podmienia host bazy postgres-noteez na localhost i startuje backend na 0.0.0.0:5112
# (dostepny z ESP32 w tej samej sieci).
#
# Uzycie (Git Bash, NIE WSL):  bash scripts/run-local-backend.sh [katalog_worktree]
#   - katalog_worktree: domyslnie katalog repo, w ktorym lezy ten skrypt
#   - ENV_FILE=<sciezka>: nadpisuje plik .env (domyslnie NoteEz-Server/.env z worktree, a gdy go tam nie ma -
#     z glownego worktree repo, bo .env jest w .gitignore i nie jest kopiowany do nowych worktree)
# Wymaga dzialajacego kontenera postgres-noteez (docker compose up -d postgres-noteez w NoteEz-Server).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKTREE="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"
PROJECT_DIR="$WORKTREE/NoteEz-Server/NoteEz-Server"

if [ -z "${ENV_FILE:-}" ]; then
  ENV_FILE="$WORKTREE/NoteEz-Server/.env"
  if [ ! -f "$ENV_FILE" ]; then
    MAIN_REPO="$(cd "$WORKTREE" && dirname "$(git rev-parse --path-format=absolute --git-common-dir)")"
    ENV_FILE="$MAIN_REPO/NoteEz-Server/.env"
  fi
fi
[ -f "$ENV_FILE" ] || { echo "Nie znaleziono pliku .env: $ENV_FILE (ustaw ENV_FILE=...)" >&2; exit 1; }
[ -d "$PROJECT_DIR" ] || { echo "Nie znaleziono projektu: $PROJECT_DIR" >&2; exit 1; }

# wartosc klucza z .env (bez sourcowania pliku - wartosci moga miec znaki specjalne)
get() {
  local line
  line=$(grep -m1 "^$1=" "$ENV_FILE" || true)
  line="${line#*=}"
  printf '%s' "${line%$'\r'}"
}

export ASPNETCORE_ENVIRONMENT=Development
# Host z sieci dockera -> localhost (Postgres ma opublikowany port 5432 na hoscie)
export ConnectionStrings__DefaultConnection="$(get DB_CONNECTION | sed 's/Host=postgres-noteez/Host=localhost/')"
export Jwt__Key="$(get JWT_KEY)"
export Jwt__Issuer="$(get JWT_ISSUER)"
export Jwt__Audience="$(get JWT_AUDIENCE)"
export ConnectionStrings__BlobStorage="$(get AZURE_BLOB_CONNECTION_STRING)"
export BlobStorage__ContainerName="$(get AZURE_BLOB_CONTAINER_NAME)"
export Cors__AllowedOrigins__0="http://localhost:5173"
export Cors__AllowedOrigins__1="$(get CORS_ALLOWED_ORIGIN_LAN)"
export Resend__ApiKey="$(get MAIL_KEY)"
export Resend__FromEmail="$(get MAIL_FROM_EMAIL)"
export Resend__FromName="$(get MAIL_FROM_NAME)"
export App__FrontendBaseUrl="http://localhost:5173"
export App__BackendBaseUrl="http://localhost:5112"
export Turnstile__SecretKey="$(get TURNSTILE_SECRET_KEY)"

echo "Backend z: $PROJECT_DIR"
echo "Konfiguracja z: $ENV_FILE (wartosci nie sa wypisywane)"
cd "$PROJECT_DIR"
exec dotnet run --no-launch-profile --urls http://0.0.0.0:5112
