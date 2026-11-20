#!/usr/bin/env bash
# Automatyczny deploy backendu na VM + healthcheck. Uruchamiany z crona co kilka minut (albo recznie).
#
# Co robi przy kazdym uruchomieniu:
#   1. sprawdza (git fetch), czy na sledzonej galezi (domyslnie main) pojawil sie nowy commit,
#   2. jesli tak - wdraza (git ff, docker compose up -d --build, restart nginx), sprawdza zdrowie API
#      i przy bledzie WRACA do poprzedniego commitu (rollback). Commit, ktory sie nie udal, jest zapamietany
#      i nie jest probowany w kolko,
#   3. jesli nie - tylko healthcheck (kontenery + odpowiedz API + baza); gdy cos nie dziala, probuje `up -d`.
# Zabezpieczenia: blokada (flock), brudne drzewo git, brak miejsca na dysku, NOWE MIGRACJE (te zostaja reczne).
# Commity, ktore nie dotykaja NoteEz-Server/ (frontend, firmware, docs), tylko aktualizuja checkout - bez budowania.
#
# Uzycie:   scripts/deploy-backend.sh [--status] [--dry-run] [--force]
#   --status   pokazuje stan (galaz, commit, zdrowie, ostatnie logi) i konczy
#   --dry-run  pokazuje, co by zrobil, niczego nie zmieniajac
#   --force    omija blokade migracji, liste commitow nieudanych i zabezpieczenia przed cofnieciem/rozjechana historia
#   --rebuild  przebudowuje kontenery z biezacego kodu w repo (np. po recznym `git pull`, ktory zmienil kod backendu)
#
# Skrypt pamieta, z jakiego commita ZBUDOWANO dzialajace kontenery (~/.noteez-deploy/built_sha) i porownuje z tym kod
# w repo - dzieki temu reczny `git pull` nie ukrywa rozjazdu. Wdraza automatycznie TYLKO prawdziwa kontynuacje
# wdrozonego commita (fast-forward); inna linia historii (np. stary commit merge'owy) jest pomijana i zgloszona w logu.
#
# Konfiguracja (opcjonalnie) w ~/.noteez-deploy.conf (zwykly plik bash z przypisaniami, np.):
#   BRANCH=main                      # sledzona galaz (domyslnie main)
#   NOTIFY_URL=https://ntfy.sh/xxx   # opcjonalne powiadomienia (POST tekstu; adres discord.com/api/webhooks tez dziala)
#   HEALTH_HOST=api.noteez.online    # nazwa hosta dla healthchecku (rozwiazywana na 127.0.0.1 -> nginx)
#   MIN_FREE_MB=1500                 # nizej niz to pomijamy build (brak miejsca na dysku)
#
# Cron (crontab -e jako uzytkownik z dostepem do dockera):
#   */5 * * * * /sciezka/do/repo/scripts/deploy-backend.sh >/dev/null 2>&1
# Logi: ~/.noteez-deploy/deploy.log (zapisywane sa tylko zdarzenia, nie kazde puste uruchomienie).

# Cala logika w funkcji main wywolanej na koncu: bash wczytuje plik w calosci przed wykonaniem, wiec
# aktualizacja tego pliku przez git w trakcie dzialania nie psuje biezacego uruchomienia.
main() {
  set -uo pipefail
  # cron ma minimalny PATH; dolaczamy typowe katalogi NA KONCU (nie psuje to nadpisywania w testach)
  export PATH="$PATH:/usr/local/bin:/usr/bin:/bin"

  local DRY_RUN=0 FORCE=0 STATUS=0 REBUILD=0 arg
  for arg in "$@"; do
    case "$arg" in
      --dry-run) DRY_RUN=1 ;;
      --force) FORCE=1 ;;
      --rebuild) REBUILD=1 ;;
      --status) STATUS=1 ;;
      *) echo "Nieznany argument: $arg" >&2; return 2 ;;
    esac
  done

  local SCRIPT_DIR REPO_ROOT COMPOSE_DIR
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
  COMPOSE_DIR="$REPO_ROOT/NoteEz-Server"

  local CONF="${DEPLOY_CONF:-$HOME/.noteez-deploy.conf}"
  # shellcheck disable=SC1090
  [ -f "$CONF" ] && . "$CONF"
  local BRANCH="${BRANCH:-main}"
  local NOTIFY_URL="${NOTIFY_URL:-}"
  local HEALTH_HOST="${HEALTH_HOST:-api.noteez.online}"
  local HEALTH_URL="${HEALTH_URL:-https://$HEALTH_HOST/api/device-notes/lite}"
  local MIN_FREE_MB="${MIN_FREE_MB:-1500}"
  local STATE_DIR="${STATE_DIR:-$HOME/.noteez-deploy}"
  local MAX_LOG_LINES=2000

  mkdir -p "$STATE_DIR"
  local LOG_FILE="$STATE_DIR/deploy.log"
  local BLOCKED_FILE="$STATE_DIR/blocked_sha"
  local NOTIFIED_FILE="$STATE_DIR/last_notified"
  local UNHEALTHY_FILE="$STATE_DIR/unhealthy"
  # commit, z ktorego ZBUDOWANO obecnie dzialajace kontenery (nie to samo co HEAD po recznym `git pull`!)
  local BUILT_FILE="$STATE_DIR/built_sha"

  log() {
    local line
    line="$(date '+%Y-%m-%d %H:%M:%S') $*"
    echo "$line" >> "$LOG_FILE"
    [ -t 1 ] && echo "$line"
    return 0
  }

  # powiadomienie tylko gdy NOTIFY_URL ustawiony; ta sama tresc nie jest wysylana dwa razy z rzedu
  notify() {
    [ -n "$NOTIFY_URL" ] || return 0
    local msg
    msg="$(printf '%s' "$1" | tr -d '"\\')"
    if [ "$(cat "$NOTIFIED_FILE" 2>/dev/null)" = "$msg" ]; then return 0; fi
    printf '%s' "$msg" > "$NOTIFIED_FILE"
    if printf '%s' "$NOTIFY_URL" | grep -q 'discord.com/api/webhooks'; then
      curl -fsS -m 10 -H 'Content-Type: application/json' -d "{\"content\":\"NoteEz deploy: $msg\"}" "$NOTIFY_URL" >/dev/null 2>&1 || true
    else
      curl -fsS -m 10 -H 'Title: NoteEz deploy' -d "$msg" "$NOTIFY_URL" >/dev/null 2>&1 || true
    fi
    return 0
  }

  rotate_log() {
    if [ -f "$LOG_FILE" ] && [ "$(wc -l < "$LOG_FILE")" -gt "$MAX_LOG_LINES" ]; then
      tail -n $((MAX_LOG_LINES / 2)) "$LOG_FILE" > "$LOG_FILE.tmp" && mv "$LOG_FILE.tmp" "$LOG_FILE"
    fi
  }

  local HEALTH_MSG=""
  # zdrowe = 3 kontenery dzialaja ORAZ API przez nginx odpowiada 401 na bledny klucz urzadzenia.
  # 401 (a nie 500/502/000) oznacza, ze nginx, API i Postgres dzialaja - handler klucza odpytuje baze.
  health_check() {
    local c code
    for c in noteez-api noteez-postgres noteez-nginx; do
      if [ "$(docker inspect -f '{{.State.Running}}' "$c" 2>/dev/null)" != "true" ]; then
        HEALTH_MSG="kontener $c nie dziala"
        return 1
      fi
    done
    code="$(curl -sk -m 10 -o /dev/null -w '%{http_code}' --resolve "$HEALTH_HOST:443:127.0.0.1" \
      -H 'X-Api-Key: healthcheck-invalid' "$HEALTH_URL" 2>/dev/null || true)"
    if [ "$code" != "401" ]; then
      HEALTH_MSG="API odpowiedzialo HTTP ${code:-brak} (oczekiwano 401)"
      return 1
    fi
    HEALTH_MSG=""
    return 0
  }

  wait_healthy() {
    local i
    for i in $(seq 1 "${WAIT_TRIES:-12}"); do
      health_check && return 0
      sleep "${WAIT_INTERVAL:-5}"
    done
    return 1
  }

  compose() { (cd "$COMPOSE_DIR" && docker compose --env-file .env.prod -f docker-compose.prod.yml "$@"); }

  if [ "$STATUS" = 1 ]; then
    cd "$REPO_ROOT" || return 1
    echo "Galaz sledzona: $BRANCH | biezaca: $(git branch --show-current) @ $(git rev-parse --short HEAD)"
    local ST_BUILT
    ST_BUILT="$(cat "$BUILT_FILE" 2>/dev/null || true)"
    if [ -z "$ST_BUILT" ]; then
      echo "Kontenery zbudowane z: (brak zapisu - zakladam biezacy commit; po recznym pullu uzyj --rebuild)"
    else
      echo "Kontenery zbudowane z: $(git rev-parse --short "$ST_BUILT" 2>/dev/null || echo "$ST_BUILT")"
      git diff --quiet "$ST_BUILT" HEAD -- NoteEz-Server 2>/dev/null || echo "UWAGA: backend w repo rozni sie od zbudowanych kontenerow - nastepny przebieg crona je przebuduje (albo --rebuild)"
    fi
    [ -f "$BLOCKED_FILE" ] && echo "Zablokowany commit: $(cat "$BLOCKED_FILE")"
    if health_check; then echo "Zdrowie: OK"; else echo "Zdrowie: PROBLEM - $HEALTH_MSG"; fi
    echo "--- ostatnie wpisy logu:"; tail -n 10 "$LOG_FILE" 2>/dev/null
    return 0
  fi

  # blokada: jedno uruchomienie naraz (cron + reczny deploy)
  exec 9>"$STATE_DIR/lock"
  if ! flock -n 9; then
    [ -t 1 ] && echo "Inny deploy/healthcheck jest w toku - sprobuj za chwile."
    return 0
  fi

  rotate_log
  cd "$REPO_ROOT" || { log "BLAD: nie moge wejsc do $REPO_ROOT"; return 1; }

  if ! git fetch -q origin "$BRANCH" 2>/dev/null; then
    log "git fetch nieudany (siec/GitHub?) - pomijam sprawdzanie nowych commitow"
  fi

  local OLD NEW OLD_BRANCH NEW_SHORT OLD_SHORT
  OLD="$(git rev-parse HEAD)"
  OLD_BRANCH="$(git branch --show-current)"
  NEW="$(git rev-parse "origin/$BRANCH" 2>/dev/null || echo "$OLD")"
  NEW_SHORT="$(git rev-parse --short "$NEW" 2>/dev/null || echo "$NEW")"
  OLD_SHORT="$(git rev-parse --short "$OLD")"

  local BUILT BUILT_SHORT
  BUILT="$(cat "$BUILT_FILE" 2>/dev/null || true)"
  # brak (lub nieznany) zapis = zakladamy, ze kontenery pochodza z biezacego commitu
  git cat-file -e "${BUILT:-none}^{commit}" 2>/dev/null || BUILT="$OLD"
  BUILT_SHORT="$(git rev-parse --short "$BUILT")"
  # DRIFT: kod w repo (po recznym pullu itp.) rozni sie od tego, z czego zbudowano kontenery
  local DRIFT=0
  if [ "$OLD_BRANCH" = "$BRANCH" ] && [ "$NEW" = "$OLD" ]; then
    if [ "$REBUILD" = 1 ] || ! git diff --quiet "$BUILT" "$OLD" -- NoteEz-Server 2>/dev/null; then DRIFT=1; fi
  fi

  local ATTEMPTED=0 # 1 = podjeto probe wdrozenia w tym uruchomieniu (wtedy zdrowie sprawdzone w jej trakcie)
  if [ "$NEW" != "$OLD" ] || [ "$OLD_BRANCH" != "$BRANCH" ] || [ "$DRIFT" = 1 ]; then
    # --- jest co wdrozyc ---
    if [ "$FORCE" = 0 ] && [ "$REBUILD" = 0 ] && [ "$(cat "$BLOCKED_FILE" 2>/dev/null)" = "$NEW" ]; then
      : # ten commit juz sie nie udal / jest zablokowany - czekamy na nowy (albo --force)
    elif [ "$FORCE" = 0 ] && [ "$NEW" != "$OLD" ] && git merge-base --is-ancestor "$NEW" "$OLD" 2>/dev/null; then
      # sledzona galaz jest ZA biezacym commitem (np. VM stoi na nowszym develop, a BRANCH=main nie ma jeszcze tych zmian):
      # wdrozenie cofnelo by produkcje do starszego kodu, wiec nic nie robimy bez --force
      log "POMINIETO: origin/$BRANCH ($NEW_SHORT) jest ZA biezacym commitem ($OLD_SHORT) - nie cofam produkcji (zmerguj zmiany do $BRANCH albo uzyj --force)"
      notify "Pominieto: $BRANCH ($NEW_SHORT) jest starszy niz wdrozony kod ($OLD_SHORT)"
    elif [ "$FORCE" = 0 ] && [ "$NEW" != "$OLD" ] && ! git merge-base --is-ancestor "$OLD" "$NEW" 2>/dev/null; then
      # NEW nie zawiera wdrozonego commita: to inna linia historii (np. stary commit merge'owy main), nie nastepny krok.
      # Automatyczne wdrozenie moglo by po cichu zamienic kod na starszy albo pozbawic go funkcji - decyzja nalezy do czlowieka.
      log "POMINIETO: origin/$BRANCH ($NEW_SHORT) nie jest kontynuacja wdrozonego commita ($OLD_SHORT) - rozne linie historii, nie wdrazam automatycznie (git log; --force wymusza)"
      notify "Pominieto: $BRANCH ($NEW_SHORT) nie jest kontynuacja wdrozonego kodu ($OLD_SHORT)"
    elif ! git cat-file -e "origin/$BRANCH:scripts/deploy-backend.sh" 2>/dev/null; then
      # przelaczenie na galaz bez tego skryptu usunelo by go z VM razem z cronem (checkout podmienia pliki)
      log "POMINIETO: origin/$BRANCH nie zawiera scripts/deploy-backend.sh - nie przelaczam (zmerguj skrypt do $BRANCH)"
      notify "Pominieto: $BRANCH nie zawiera skryptu deploy, nie przelaczam galezi"
    elif [ -n "$(git status --porcelain --untracked-files=no)" ]; then
      log "POMINIETO deploy $NEW_SHORT: drzewo git na VM ma lokalne zmiany (git status)"
      notify "Deploy $NEW_SHORT pominiety: lokalne zmiany w repo na VM"
    elif [ "$(df -Pm "$REPO_ROOT" | awk 'NR==2{print $4}')" -lt "$MIN_FREE_MB" ]; then
      log "POMINIETO deploy $NEW_SHORT: malo miejsca na dysku (< ${MIN_FREE_MB} MB)"
      notify "Deploy $NEW_SHORT pominiety: malo miejsca na dysku"
    elif [ "$FORCE" = 0 ] && [ -n "$(git diff --name-only "$BUILT" "$NEW" -- NoteEz-Server/NoteEz-Server/Migrations 2>/dev/null)" ]; then
      log "ZABLOKOWANO deploy $NEW_SHORT: nowa migracja bazy wymaga recznego kroku (patrz docs/DEPLOY_BACKEND.md); potem --force"
      printf '%s' "$NEW" > "$BLOCKED_FILE"
      notify "Deploy $NEW_SHORT zablokowany: nowa migracja bazy, wymagany reczny krok"
    else
      # czy backend rozni sie od tego, z czego zbudowano dzialajace kontenery (a nie od HEAD - ten moze byc po recznym pullu)
      local BACKEND_CHANGED=1
      if [ "$REBUILD" = 0 ] && git diff --quiet "$BUILT" "$NEW" -- NoteEz-Server 2>/dev/null; then BACKEND_CHANGED=0; fi
      if [ "$DRY_RUN" = 1 ]; then
        log "DRY-RUN: wdrozylbym $OLD_SHORT -> $NEW_SHORT na galezi $BRANCH (kontenery z $BUILT_SHORT; build backendu: $([ $BACKEND_CHANGED = 1 ] && echo TAK || echo NIE))"
        return 0
      fi
      ATTEMPTED=1
      if [ "$DRIFT" = 1 ]; then
        log "START przebudowa $NEW_SHORT: kontenery sa zbudowane z $BUILT_SHORT, a kod w repo to $OLD_SHORT"
      else
        log "START deploy $OLD_SHORT -> $NEW_SHORT (galaz $BRANCH)"
      fi
      if { git checkout -q "$BRANCH" 2>/dev/null || git checkout -q -b "$BRANCH" "origin/$BRANCH"; } \
         && git merge -q --ff-only "origin/$BRANCH"; then
        if [ "$BACKEND_CHANGED" = 0 ]; then
          log "OK $NEW_SHORT: zmiany poza NoteEz-Server/ - tylko aktualizacja checkoutu, bez budowania"
        else
          local OUT_FILE="$STATE_DIR/last_build.log" ok=0
          if compose up -d --build > "$OUT_FILE" 2>&1; then
            compose restart nginx >> "$OUT_FILE" 2>&1 || true
            wait_healthy && ok=1
          fi
          if [ "$ok" = 1 ]; then
            docker image prune -f >/dev/null 2>&1 || true
            rm -f "$BLOCKED_FILE"
            printf '%s' "$NEW" > "$BUILT_FILE"
            log "OK wdrozono $NEW_SHORT"
            notify "Wdrozono $NEW_SHORT na $BRANCH"
          else
            log "BLAD deploy $NEW_SHORT (${HEALTH_MSG:-build nieudany}); koniec logu budowania:"
            tail -n 8 "$OUT_FILE" 2>/dev/null | while IFS= read -r l; do log "  | $l"; done
            printf '%s' "$NEW" > "$BLOCKED_FILE"
            # rollback do poprzedniego commitu
            if [ -n "$OLD_BRANCH" ]; then git checkout -q -B "$OLD_BRANCH" "$OLD"; else git checkout -q --detach "$OLD"; fi
            compose up -d --build > "$OUT_FILE" 2>&1
            compose restart nginx >> "$OUT_FILE" 2>&1 || true
            if wait_healthy; then
              printf '%s' "$OLD" > "$BUILT_FILE"
              log "ROLLBACK do $OLD_SHORT udany; $NEW_SHORT oznaczony jako nieudany (nie bedzie ponawiany)"
              notify "Deploy $NEW_SHORT NIEUDANY, wrocono do $OLD_SHORT"
            else
              log "KRYTYCZNE: rollback do $OLD_SHORT tez nie przywrocil zdrowia (${HEALTH_MSG}) - wymagana interwencja reczna"
              notify "KRYTYCZNE: deploy $NEW_SHORT i rollback nieudane, API nie dziala"
            fi
          fi
        fi
      else
        log "BLAD: nie mozna przelaczyc na $BRANCH / fast-forward do $NEW_SHORT (rozjechana historia?) - wracam na poprzednia galaz"
        # nie zostawiamy VM na innej galezi (mogla by nie miec tego skryptu)
        if [ -n "$OLD_BRANCH" ]; then git checkout -q -B "$OLD_BRANCH" "$OLD" 2>/dev/null; else git checkout -q --detach "$OLD" 2>/dev/null; fi
        printf '%s' "$NEW" > "$BLOCKED_FILE"
        notify "Deploy $NEW_SHORT nieudany: git nie moze zrobic fast-forward"
      fi
    fi
  fi
  # po probie wdrozenia zdrowie bylo juz sprawdzone; w pozostalych przypadkach (brak nowych commitow albo
  # wdrozenie pominiete/zablokowane) robimy zwykly healthcheck, zeby monitoring dzialal zawsze
  [ "$ATTEMPTED" = 1 ] && return 0

  # --- healthcheck ---
  if health_check; then
    if [ -f "$UNHEALTHY_FILE" ]; then
      rm -f "$UNHEALTHY_FILE"
      log "ZDROWIE przywrocone"
      notify "API znow dziala"
    fi
    return 0
  fi

  log "PROBLEM ze zdrowiem: $HEALTH_MSG"
  if [ "$DRY_RUN" = 1 ]; then return 0; fi
  : > "$UNHEALTHY_FILE"
  notify "API nie dziala: $HEALTH_MSG"
  compose up -d > "$STATE_DIR/last_recover.log" 2>&1
  compose restart nginx >> "$STATE_DIR/last_recover.log" 2>&1 || true
  if wait_healthy; then
    rm -f "$UNHEALTHY_FILE"
    log "ZDROWIE przywrocone po automatycznym 'up -d'"
    notify "API znow dziala (po automatycznym restarcie kontenerow)"
  else
    log "NADAL niezdrowe po 'up -d': $HEALTH_MSG - wymagana interwencja reczna"
  fi
  return 0
}

main "$@"
exit $?
