# Wdrazanie backendu (Oracle VM)

Backend dziala na Oracle Cloud VM (Docker: `api`, `postgres`, `nginx`, `certbot`), frontend na Cloudflare Worker.
Backend NIE ma auto-deployu - po merge'u trzeba go wdrozyc recznie.

## Decyzja: VM sledzi gałąź `main`

- Zrodlem prawdy dla calej produkcji jest `main`: merge do `main` buduje frontend na Cloudflare, a backend
  na VM wciaga ta sama gałąź. Dzieki temu backend i frontend nie rozjezdzaja sie wersjami.
- Historycznie VM sledzil `develop` (wynik pierwszego klonowania, bez swiadomego uzasadnienia). Skutek: `develop`
  = produkcja backendu jeszcze przed merge'em do `main`. Decyzja: przelaczyc VM na `main`.
- STATUS: przelaczenie na VM do wykonania/zweryfikowania (`git branch --show-current` w katalogu repo na VM).

## Jednorazowe przelaczenie VM na `main` (i wyczyszczenie drzewa)

Na VM pliki `nginx/conf.d/noteez.conf` i `nginx/init-letsencrypt.sh` byly zmodyfikowane lokalnie (prawdziwa domena,
e-mail, `chmod +x`). Te same wartosci sa teraz w repo (branch `chore/deploy-config`), wiec po jego zmergowaniu
trzeba lokalne zmiany porzucic, inaczej `git pull` sie przerwie ("would be overwritten").

Wykonaj to dopiero gdy zmiany z `chore/deploy-config` sa w `main` (inaczej po `checkout --` plik nginx wroci do
szablonu z placeholderem domeny). Kroki od `checkout --` do `pull` wykonaj jeden po drugim bez przerwy i nie
restartuj w tym czasie nginx - dziala on na pliku z dysku (bind mount).

```
ssh -i <klucz> ubuntu@92.5.37.41
cd <katalog repo>
git status                                   # sprawdz, ze zmienione sa tylko pliki nginx (+ nieobserwowane .env.prod*)
git checkout -- NoteEz-Server/nginx          # porzuca lokalne zmiany nginx (identyczne z repo po merge'u)
git fetch origin
git checkout main
git pull origin main
git status                                   # drzewo ma byc czyste (poza ignorowanymi .env.prod*)
```

Pliki `.env.prod`, `.env.prod.save`, `.env.prod.backup-azuresql` sa teraz ignorowane przez git. Nie usuwaj ich w ciemno:
`.env.prod` w katalogu glownym repo to prawdopodobnie zbedna kopia (compose czyta `NoteEz-Server/.env.prod`),
pozostale to stare kopie. Przenies je poza repo (np. `~/backup/`) po sprawdzeniu `diff`.

Po przelaczeniu: standardowy deploy ponizej (nginx nie wymaga restartu z powodu tej zmiany - plik jest identyczny jak
dotychczas dzialajacy, ale przy `up -d --build` i tak restartujemy nginx).

## Standardowy deploy backendu (po merge'u do `main`)

```
ssh ubuntu@<ip VM>
cd <katalog repo>/NoteEz-Server
git rev-parse HEAD                       # zapisz commit - do ewentualnego powrotu
git pull origin main
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml restart nginx   # bez tego nginx moze dawac 502
```

- `--env-file .env.prod` jest wymagane (sama dyrektywa `env_file:` nie podstawia `${...}` w pliku compose).
- Migracje: tylko gdy zmienil sie schemat bazy (nowy plik w `Migrations/`). Procedura: throwaway kontener
  `mcr.microsoft.com/dotnet/sdk:8.0` w sieci `noteez-server_web` z `dotnet ef database update`
  (pelna komenda w notatkach projektu). Zmiany bez migracji (np. nowy endpoint) tego kroku nie wymagaja.
- Powrot: `git checkout <zapisany commit>`, ten sam `up -d --build` i restart nginx.

## Automatyczny deploy i healthcheck (`scripts/deploy-backend.sh`, cron na VM)

Praktyczny przewodnik krok po kroku (komendy, logi, znaczenie wpisow, migracje, powrot): [../NoteEz-Server/DEPLOY_GUIDE.md](../NoteEz-Server/DEPLOY_GUIDE.md).

Skrypt uruchamiany z crona co 5 minut (bez kluczy SSH w GitHubie i bez procesu stale dzialajacego w tle; sprawdzenie
galezi to jedno `git fetch`). Przy kazdym uruchomieniu:
- jest nowy commit na sledzonej galezi (domyslnie `main`) -> `git` fast-forward, `up -d --build`, restart nginx, kontrola
  zdrowia; **przy bledzie wraca do poprzedniego commitu** (rollback) i zapamietuje zly commit (nie ponawia go w kolko),
- commity, ktore nie dotykaja `NoteEz-Server/` (frontend, firmware, docs), tylko aktualizuja checkout - bez budowania,
- brak nowych commitow -> sam healthcheck; gdy cos nie dziala, probuje `docker compose up -d` i restart nginx.

Healthcheck: 3 kontenery dzialaja (`noteez-api`, `noteez-postgres`, `noteez-nginx`) ORAZ `GET /api/device-notes/lite`
z bledny kluczem `X-Api-Key` przez nginx zwraca **401**. To dobry sygnal, bo handler klucza odpytuje baze: 401 = nginx, API
i Postgres dzialaja; przy awarii bazy byloby 500, przy padnietym API 502/brak polaczenia.

Zabezpieczenia (skrypt nic nie robi i zapisuje powod w logu): lokalne zmiany w repo na VM; malo miejsca na dysku
(`MIN_FREE_MB`, domyslnie 1500); **nowa migracja bazy** w diffie (migracje zostaja reczne - po recznym kroku
`scripts/deploy-backend.sh --force`); **sledzona galaz jest ZA biezacym commitem** (nie cofa produkcji bez `--force`).

### Instalacja na VM (jednorazowo)

```
cd <katalog repo>
git pull                                    # skrypt musi byc w checkoucie na VM
scripts/deploy-backend.sh --status          # galaz, commit, zdrowie, ostatnie logi
scripts/deploy-backend.sh --dry-run         # co by zrobil, niczego nie zmieniajac
crontab -e                                  # dopisz linie:
*/5 * * * * <katalog repo>/scripts/deploy-backend.sh >/dev/null 2>&1
```
Uzytkownik crona musi uruchamiac `docker` bez `sudo` (sprawdz `docker ps`). Logi: `~/.noteez-deploy/deploy.log`
(zapisywane sa tylko zdarzenia). Opcjonalna konfiguracja w `~/.noteez-deploy.conf` (zwykly plik bash):
`BRANCH=main`, `NOTIFY_URL=...` (powiadomienia: POST tekstu, np. ntfy.sh; adres `discord.com/api/webhooks` tez dziala),
`HEALTH_HOST`, `MIN_FREE_MB`. Bez `NOTIFY_URL` skrypt nie wysyla nic na zewnatrz.

### Test przed merge'em do `main`

1. Na VM przejdz na `develop` (zeby miec skrypt) i ustaw sledzenie `develop`:
   `git fetch origin && git checkout develop && git pull --ff-only origin develop`, potem w `~/.noteez-deploy.conf`: `BRANCH=develop`.
2. `--status` i `--dry-run`, potem dopisz crona. **Od tej chwili kazdy push na `develop` wdraza backend na produkcje w ciagu 5 minut.**
3. Wypchnij drobna zmiane w `NoteEz-Server/` na `develop` i obserwuj `~/.noteez-deploy/deploy.log` (spodziewane: `START deploy`, `OK wdrozono`).
4. Gdy dziala: zmerguj `develop` do `main` i **dopiero wtedy** ustaw `BRANCH=main` (albo usun plik konfiguracji - domyslnie `main`).
   Jesli `main` bylby jeszcze za `develop`, skrypt nie cofnie produkcji (zabezpieczenie), tylko zapisze to w logu.

Testy skryptu na tymczasowym repo z atrapami `docker`/`curl`: `bash scripts/tests/test-deploy-backend.sh` (55 sprawdzen:
brak zmian, commit tylko we frontendzie, build, rollback po zlym zdrowiu, brak ponawiania zlego commita, migracja,
`--force`, brudne drzewo, odzyskiwanie po awarii, `--dry-run`, zmiana galezi, brak cofania, inna linia historii, rozjazd po recznym pullu, `--rebuild`). **Nie testowano na prawdziwej VM.**

## Sprawdzenie po deployu

- Zaloguj sie w aplikacji i wykonaj zmiane dotykajaca nowej funkcji.
- Firmware ESP32 nie wdraza sie przez merge - wgrywa sie je recznie z Arduino IDE (urzadzenie musi byc
  sparowane z produkcja: host `noteez.online`).

## Lokalne testy z urzadzeniem

Backend z dowolnego worktree uruchom skryptem (Git Bash, NIE WSL - w zwyklym `cmd` samo `bash` odpala WSL):

```
"C:\Program Files\Git\bin\bash.exe" scripts/run-local-backend.sh [katalog_worktree]
```

`dotnet run` sam nie ma konfiguracji (JWT, baza, CORS, Turnstile), bo w Dockerze dostarcza ja `docker-compose.yml`
ze zmiennych w `NoteEz-Server/.env`. Skrypt ustawia te same zmienne (bez wypisywania wartosci), zamienia host bazy
`postgres-noteez` na `localhost` i startuje backend na `0.0.0.0:5112`. Wymaga dzialajacego kontenera `postgres-noteez`.
`.env` jest w `.gitignore`, wiec w nowym worktree go nie ma - skrypt bierze go z glownego worktree.

Frontend: dla zmian tylko w backendzie/firmware wystarczy uruchomic Vite z glownego katalogu i nadpisac adres API
zmienna srodowiskowa (ma priorytet nad `.env`): `$env:VITE_API_URL="http://localhost:5112/api"; npm run dev` (trasy
backendu sa pod `/api`; bez tego frontend trafia przez proxy w Docker na port 8080). Po zmianie `.env` restart Vite.

Urzadzenie paruje sie wtedy z adresem `IP_komputera:5112` (sprawdz `ipconfig`, adres moze sie zmienic; Windows moze
wymagac reguly zapory dla portu 5112) i po testach trzeba je ponownie sparowac z produkcja (`noteez.online`).

## Firmware ESP32

Wgrywanie: `.\scripts\flash.ps1 -Port COMx` (szczegoly w `NoteEz-ESP32/notes/README.md`). Firmware nie wdraza sie przez merge.
