# Przewodnik: deploy backendu na VM (krok po kroku)

Praktyczna ściągawka do codziennego użycia. Szczegóły techniczne i decyzje: [../docs/DEPLOY_BACKEND.md](../docs/DEPLOY_BACKEND.md).
Wszystkie komendy poniżej wpisujesz **na VM** po zalogowaniu:

```
ssh -i <sciezka-do-pliku-klucza> ubuntu@<ip VM>        # klucz pobrany z Oracle przy tworzeniu VM (plik .key), IP VM = api.noteez.online
```

W komendach `<repo>` oznacza katalog z repozytorium NoteEz na VM. Jeśli go nie pamiętasz:
```
find ~ -name deploy-backend.sh -path "*scripts*" 2>/dev/null     # wynik: <repo>/scripts/deploy-backend.sh
```

## 1. Jak to działa (w 30 sekund)

- Na VM chodzą 4 kontenery Dockera: `noteez-api` (backend), `noteez-postgres` (baza), `noteez-nginx` (proxy + TLS), `noteez-certbot` (certyfikat).
- **Cron co 5 minut** uruchamia `scripts/deploy-backend.sh`. Skrypt sprawdza, czy na śledzonej gałęzi (`main`, a na czas testów `develop`) jest nowy commit.
- Jest nowy commit w `NoteEz-Server/` → skrypt sam wdraża: `git` fast-forward, budowanie, start, restart nginx, sprawdzenie zdrowia.
  Jeśli po wdrożeniu API nie odpowiada, **sam wraca do poprzedniego commitu**.
- Commity, które nie ruszają `NoteEz-Server/` (frontend, firmware, dokumentacja), tylko aktualizują pliki na VM, bez budowania.
- Frontend nie ma z tym nic wspólnego: Cloudflare buduje go sam z `main`.

## 2. Zwykły deploy (nic nie robisz na VM)

1. Zmerguj zmiany do śledzonej gałęzi (po zakończeniu testów: do `main`).
2. Poczekaj do 5 minut (build na ARM potrafi trwać kilka minut).
3. Sprawdź wynik (sekcja 3). Zaloguj się w aplikacji i sprawdź zmianę.

Chcesz nie czekać na cron? Uruchom skrypt ręcznie (sekcja 4).

## 3. Jak sprawdzić, co się dzieje

**Jedno polecenie ze stanem** (gałąź, commit, zdrowie, ostatnie 10 wpisów logu):
```
<repo>/scripts/deploy-backend.sh --status
```
Przykład: `Galaz sledzona: develop | biezaca: develop @ cc1de41` / `Zdrowie: OK`.

**Logi deployu** (zapisywane są tylko zdarzenia, nie każde puste uruchomienie):
```
tail -n 30 ~/.noteez-deploy/deploy.log          # ostatnie wpisy
tail -f ~/.noteez-deploy/deploy.log             # na żywo (Ctrl+C kończy)
```

**Inne pliki w `~/.noteez-deploy/`:**

| Plik | Co zawiera |
|---|---|
| `deploy.log` | historia zdarzeń deployu (rotuje się sama przy 2000 linii) |
| `last_build.log` | wyjście ostatniego `docker compose up --build` (tu szukaj błędu budowania) |
| `last_recover.log` | wyjście ostatniej automatycznej naprawy (`up -d`) |
| `built_sha` | commit, z którego **zbudowano obecnie działające kontenery** (nie musi być równy `HEAD` po ręcznym `git pull`) |
| `blocked_sha` | commit, którego skrypt nie wdraża (zły albo z migracją) |
| `unhealthy` | istnieje, gdy API jest akurat niezdrowe |

**Stan Dockera i aplikacji:**
```
docker ps                                        # 4 kontenery, STATUS "Up ..."
docker logs noteez-api --tail 100                # logi backendu
docker logs noteez-api -f                        # na żywo
docker logs noteez-nginx --tail 50               # logi nginx
git -C <repo> log --oneline -5                   # jaki kod jest wdrożony
git -C <repo> branch --show-current              # na jakiej gałęzi stoi VM
crontab -l                                       # czy cron jest ustawiony
```

## 4. Ręczne uruchomienie skryptu

```
<repo>/scripts/deploy-backend.sh --dry-run       # pokazuje, co by zrobił, niczego nie zmienia (cisza = nie ma nic do wdrożenia)
<repo>/scripts/deploy-backend.sh                 # robi to samo co cron (wdraża, jeśli jest nowy commit)
<repo>/scripts/deploy-backend.sh --rebuild       # przebudowuje kontenery z kodu, który jest teraz w repo na VM
<repo>/scripts/deploy-backend.sh --force         # omija blokadę migracji, listę złych commitów, zakaz cofania i kontrolę "kontynuacji historii"
```
Jeśli drugi skrypt już działa (np. cron), nowy powie, żeby spróbować za chwilę.

**Uwaga o ręcznym `git pull` / `git checkout`:** zmienia tylko pliki, **nie kontenery**. Skrypt porównuje kod w repo z tym, z czego
zbudowano kontenery (`built_sha`), więc przy najbliższym cronie sam przebuduje backend, jeśli się różni (log: `START przebudowa`).
`--status` ostrzega o takim rozjeździe (`UWAGA: backend w repo rozni sie od zbudowanych kontenerow`). Gdy nie chcesz czekać na cron,
użyj `--rebuild`. Przy pierwszym uruchomieniu po wgraniu tej wersji skryptu nie ma jeszcze `built_sha` i skrypt zakłada, że kontenery
pochodzą z bieżącego commitu, więc jeśli wiesz, że jest inaczej, zrób raz `--rebuild`.

## 5. Co znaczą wpisy w `deploy.log`

| Wpis | Znaczenie | Co zrobić |
|---|---|---|
| `START deploy A -> B` | zaczyna wdrażać commit B | nic, czekaj na `OK` |
| `OK wdrozono B` | wdrożone i zdrowe | nic |
| `START przebudowa B: kontenery są zbudowane z A, a kod w repo to B` | kod w repo różnił się od kontenerów (np. po ręcznym pullu) | nic, czekaj na `OK` |
| `OK B: zmiany poza NoteEz-Server/ ...` | tylko aktualizacja plików, bez budowania | nic |
| `POMINIETO: origin/main (B) nie jest kontynuacją wdrożonego commita (A)` | śledzona gałąź to inna linia historii (np. stary commit merge'owy), więc automatyczne wdrożenie mogłoby podmienić kod na starszy | `git log --oneline --graph --all`; zmerguj poprawnie do tej gałęzi, a jeśli zmiana jest zamierzona, `--force` |
| `BLAD deploy B (...)` + `ROLLBACK ... udany` | nowy kod nie przeszedł sprawdzenia zdrowia, wrócono do starego | napraw błąd w kodzie (patrz `last_build.log` i `docker logs noteez-api`); nowy commit zostanie wdrożony automatycznie |
| `KRYTYCZNE: rollback ... tez nie przywrocil zdrowia` | API nie działa nawet po powrocie | wejdź na VM: `docker ps`, `docker logs noteez-api`, `last_build.log` |
| `ZABLOKOWANO ... nowa migracja bazy` | w commicie jest migracja EF, skrypt jej nie robi sam | sekcja 6 |
| `POMINIETO ... lokalne zmiany w repo` | na VM są niezacommitowane zmiany w śledzonych plikach | `git -C <repo> status`, potem `git diff`; cofnij je (`git checkout -- <plik>`) albo zacommituj zmianę na GitHubie |
| `POMINIETO ... malo miejsca na dysku` | mniej niż 1500 MB wolnego | `df -h`, `docker system df`, `docker image prune -f` (usuwa tylko nieużywane obrazy bez nazwy) |
| `POMINIETO: origin/main ... jest ZA biezacym commitem` | gałąź, którą śledzisz, jest starsza niż to, co wdrożono; skrypt nie cofa produkcji | zmerguj nowsze zmiany do tej gałęzi albo użyj `--force`, jeśli cofnięcie jest zamierzone |
| `PROBLEM ze zdrowiem: ...` | healthcheck wykrył awarię (kontener padł albo API nie odpowiada 401) | skrypt sam próbuje `docker compose up -d`; zobacz następny wpis |
| `ZDROWIE przywrocone ...` | po automatycznym restarcie jest OK | zobacz w `docker logs noteez-api`, czemu padło |
| `NADAL niezdrowe po 'up -d'` | automatyczna naprawa nie pomogła | ręczna diagnoza jak wyżej |
| `git fetch nieudany` | chwilowy brak sieci/GitHuba | zwykle samo przechodzi |

## 6. Migracje bazy (ręczny krok)

Skrypt celowo **nie** robi migracji (w obrazie nie ma `dotnet-ef`). Gdy zobaczysz `ZABLOKOWANO ... migracja`:

1. Wdróż kod: `<repo>/scripts/deploy-backend.sh --force`.
2. Zastosuj migrację (komenda z wcześniejszego wdrożenia; **sprawdź nazwę sieci przez `docker network ls`** - ma postać `<katalog>_web`):
   ```
   cd <repo>/NoteEz-Server
   export $(grep -v '^#' .env.prod | xargs)
   docker run --rm --network noteez-server_web -v $(pwd)/..:/src -w /src/NoteEz-Server/NoteEz-Server \
     mcr.microsoft.com/dotnet/sdk:8.0 bash -c \
     "dotnet tool install --global dotnet-ef --version 8.0.28 && export PATH=\"\$PATH:/root/.dotnet/tools\" && \
      dotnet ef database update --connection 'Host=postgres;Port=5432;Database=noteez;Username=noteez;Password=$POSTGRES_PASSWORD;'"
   ```
3. Sprawdź: `<repo>/scripts/deploy-backend.sh --status`.

Przed nową migracją zrób kopię bazy. Dotychczasowa historia migracji była raz wygenerowana od zera (jedna `InitialCreate`), więc
nie ruszaj jej, tylko dodawaj nowe migracje przyrostowo.

## 7. Ręczny powrót do starszej wersji

Skrypt wraca sam po nieudanym deployu. Jeśli sam zdecydujesz, że nowa wersja jest zła (a zdrowie jest OK):
```
cd <repo>
git log --oneline -10                                        # wybierz dobry commit
git rev-parse origin/main > ~/.noteez-deploy/blocked_sha     # skrypt nie będzie w kółko wdrażał złego (użyj origin/develop przy śledzeniu develop)
git checkout -B <gałąź> <dobry_sha>                          # np. git checkout -B main abc1234
cd NoteEz-Server
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml restart nginx
```
Gdy poprawka będzie gotowa i trafi jako **nowy** commit, skrypt wdroży go sam.

## 8. Wyłączenie i konfiguracja

```
crontab -l | grep -v deploy-backend.sh | crontab -      # wyłącza auto-deploy (zostaje ręczny)
(crontab -l 2>/dev/null; echo "*/5 * * * * <repo>/scripts/deploy-backend.sh >/dev/null 2>&1") | crontab -    # włącza z powrotem
systemctl is-active cron                                  # ma być "active"
```
Konfiguracja w `~/.noteez-deploy.conf` (zwykły plik tekstowy, może go nie być):
```
BRANCH=develop                      # śledzona gałąź; bez pliku: main
NOTIFY_URL=https://ntfy.sh/twoj-temat   # powiadomienia o deployu/awarii (opcjonalnie; bez tego nic nie wychodzi na zewnątrz)
MIN_FREE_MB=1500                    # minimum wolnego miejsca do budowania
```
**Uwaga:** przy `BRANCH=develop` każdy push na `develop` wdraża backend na produkcję w ciągu 5 minut. Po zakończeniu testów zmień na `main`
(lub usuń plik) dopiero **po** zmergowaniu `develop` do `main`.

## 9. Ściągawka

```
scripts/deploy-backend.sh --status        # stan + ostatnie logi
tail -f ~/.noteez-deploy/deploy.log       # logi na żywo
scripts/deploy-backend.sh                 # wdróż teraz
scripts/deploy-backend.sh --dry-run       # tylko pokaż
docker ps                                 # kontenery
docker logs noteez-api --tail 100         # logi backendu
```
