#!/usr/bin/env bash
# Test scripts/deploy-backend.sh na tymczasowym repo z atrapami docker/curl/flock (Git Bash lub Linux).
# Nie dotyka prawdziwego repo ani Dockera. Uzycie: bash scripts/tests/test-deploy-backend.sh
set -uo pipefail

SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/deploy-backend.sh"
T="$(mktemp -d)"
export T
mkdir -p "$T/stubs" "$T/home"
PASS=0; FAIL=0

# --- atrapy ---
cat > "$T/stubs/docker" <<'EOF'
#!/usr/bin/env bash
echo "docker $*" >> "$T/calls.log"
case "$1" in
  inspect) cat "$T/inspect_state" 2>/dev/null || echo true ;;
  compose)
    if [[ " $* " == *" up "* ]]; then
      sha="$(git -C "$T/vm" rev-parse HEAD)"
      if [ "$sha" = "$(cat "$T/bad_sha" 2>/dev/null)" ]; then echo 500 > "$T/http_code"; else echo 401 > "$T/http_code"; fi
    fi
    exit 0 ;;
  *) exit 0 ;;
esac
EOF
cat > "$T/stubs/curl" <<'EOF'
#!/usr/bin/env bash
if [[ " $* " == *" -w "* ]]; then cat "$T/http_code" 2>/dev/null || echo 401; else echo "curl $*" >> "$T/notify.log"; fi
EOF
printf '#!/usr/bin/env bash\nexit 0\n' > "$T/stubs/flock"
chmod +x "$T/stubs/"*

# --- repo: origin (bare), seed, vm ---
git init -q --bare "$T/origin.git"
git -C "$T/origin.git" symbolic-ref HEAD refs/heads/main
git -c core.autocrlf=false clone -q "$T/origin.git" "$T/seed" 2>/dev/null
for r in seed; do git -C "$T/$r" config user.email t@t; git -C "$T/$r" config user.name t; git -C "$T/$r" config core.autocrlf false; done
mkdir -p "$T/seed/NoteEz-Server/NoteEz-Server/Migrations" "$T/seed/NoteEz-Frontend" "$T/seed/scripts"
echo a > "$T/seed/NoteEz-Server/NoteEz-Server/app.cs"
echo a > "$T/seed/NoteEz-Server/docker-compose.prod.yml"
echo a > "$T/seed/NoteEz-Server/NoteEz-Server/Migrations/m1.cs"
echo a > "$T/seed/NoteEz-Frontend/f.js"
cp "$SRC" "$T/seed/scripts/deploy-backend.sh"
git -C "$T/seed" add -A && git -C "$T/seed" commit -q -m init && git -C "$T/seed" branch -M main && git -C "$T/seed" push -q origin main 2>/dev/null
git -c core.autocrlf=false clone -q "$T/origin.git" "$T/vm" 2>/dev/null
git -C "$T/vm" config user.email t@t; git -C "$T/vm" config user.name t; git -C "$T/vm" config core.autocrlf false

cat > "$T/home/conf" <<EOF
BRANCH=main
NOTIFY_URL=https://example.invalid/topic
STATE_DIR=$T/home/state
MIN_FREE_MB=1
EOF

run() {
  HOME="$T/home" DEPLOY_CONF="${CONF_FILE:-$T/home/conf}" PATH="$T/stubs:$PATH" WAIT_INTERVAL=0 WAIT_TRIES=2 \
    bash "$T/vm/scripts/deploy-backend.sh" "$@" > "$T/last_out.txt" 2>&1
}
push_commit() { # plik tresc
  mkdir -p "$(dirname "$T/seed/$1")"; echo "$2" > "$T/seed/$1"
  git -C "$T/seed" add -A && git -C "$T/seed" commit -q -m "c $1 $2" && git -C "$T/seed" push -q origin main 2>/dev/null
}
LOG="$T/home/state/deploy.log"
count() { grep -c -- "$1" "$2" 2>/dev/null || true; }
check() { # opis warunek(exit)
  if [ "$2" = 0 ]; then echo "  PASS  $1"; PASS=$((PASS+1)); else echo "  FAIL  $1"; FAIL=$((FAIL+1)); fi
}
vm_head() { git -C "$T/vm" rev-parse HEAD; }
origin_head() { git -C "$T/origin.git" rev-parse main; }
builds() { grep -c -- "--build" "$T/calls.log" 2>/dev/null || true; }
: > "$T/calls.log"

echo "S1: brak zmian, zdrowo -> cisza, bez budowania"
run; check "log pusty lub brak" "$([ ! -s "$LOG" ]; echo $?)"; check "bez --build" "$([ "$(builds)" = 0 ]; echo $?)"

echo "S2: commit tylko we frontendzie -> checkout bez budowania"
push_commit NoteEz-Frontend/f.js b; run
check "HEAD == origin" "$([ "$(vm_head)" = "$(origin_head)" ]; echo $?)"
check "log: bez budowania" "$(grep -q 'bez budowania' "$LOG"; echo $?)"
check "brak --build" "$([ "$(builds)" = 0 ]; echo $?)"

echo "S3: commit w backendzie -> build + restart nginx + OK"
push_commit NoteEz-Server/NoteEz-Server/app.cs b; run
check "HEAD == origin" "$([ "$(vm_head)" = "$(origin_head)" ]; echo $?)"
check "up --build wywolane" "$([ "$(builds)" = 1 ]; echo $?)"
check "restart nginx" "$(grep -q 'restart nginx' "$T/calls.log"; echo $?)"
check "log OK wdrozono" "$(grep -q 'OK wdrozono' "$LOG"; echo $?)"
GOOD="$(vm_head)"

echo "S4: zepsuty commit (health 500) -> rollback do poprzedniego, commit zablokowany"
push_commit NoteEz-Server/NoteEz-Server/app.cs bad; echo "$(origin_head)" > "$T/bad_sha"; BAD="$(origin_head)"; run
check "HEAD wrocil do dobrego" "$([ "$(vm_head)" = "$GOOD" ]; echo $?)"
check "blocked_sha == zepsuty" "$([ "$(cat "$T/home/state/blocked_sha")" = "$BAD" ]; echo $?)"
check "log ROLLBACK udany" "$(grep -q 'ROLLBACK do .* udany' "$LOG"; echo $?)"
check "powiadomienie wyslane" "$(grep -q 'NIEUDANY' "$T/notify.log"; echo $?)"
B4="$(builds)"

echo "S5: kolejne uruchomienie -> zepsuty commit nie jest ponawiany, ale healthcheck dziala"
INSP="$(grep -c 'docker inspect' "$T/calls.log")"; run
check "bez nowego --build" "$([ "$(builds)" = "$B4" ]; echo $?)"
check "healthcheck wykonany" "$([ "$(grep -c 'docker inspect' "$T/calls.log")" -gt "$INSP" ]; echo $?)"
check "HEAD nadal dobry" "$([ "$(vm_head)" = "$GOOD" ]; echo $?)"

echo "S6: commit z migracja -> zablokowany, bez budowania"
rm -f "$T/bad_sha"; push_commit NoteEz-Server/NoteEz-Server/Migrations/m2.cs m2; MIG="$(origin_head)"; B6="$(builds)"; run
check "HEAD bez zmian" "$([ "$(vm_head)" = "$GOOD" ]; echo $?)"
check "log ZABLOKOWANO" "$(grep -q 'ZABLOKOWANO' "$LOG"; echo $?)"
check "bez --build" "$([ "$(builds)" = "$B6" ]; echo $?)"
check "blocked_sha == migracja" "$([ "$(cat "$T/home/state/blocked_sha")" = "$MIG" ]; echo $?)"

echo "S7: --force -> wdraza commit z migracja"
run --force
check "HEAD == origin" "$([ "$(vm_head)" = "$(origin_head)" ]; echo $?)"
check "blocked_sha usuniety" "$([ ! -f "$T/home/state/blocked_sha" ]; echo $?)"

echo "S8: brudne drzewo na VM -> deploy pominiety"
push_commit NoteEz-Server/NoteEz-Server/app.cs c; echo dirty >> "$T/vm/NoteEz-Server/NoteEz-Server/app.cs"; H8="$(vm_head)"; run
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H8" ]; echo $?)"
check "log POMINIETO" "$(grep -q 'POMINIETO.*lokalne zmiany' "$LOG"; echo $?)"
git -C "$T/vm" checkout -q -- . ; run
check "po wyczyszczeniu drzewa wdrozone" "$([ "$(vm_head)" = "$(origin_head)" ]; echo $?)"

echo "S9: brak nowych commitow, API padlo -> automatyczny up -d i odzyskanie"
echo 500 > "$T/http_code"; run
check "log PROBLEM" "$(grep -q 'PROBLEM ze zdrowiem' "$LOG"; echo $?)"
check "log przywrocone po up -d" "$(grep -q 'przywrocone po automatycznym' "$LOG"; echo $?)"

echo "S10: --dry-run -> niczego nie zmienia"
push_commit NoteEz-Server/NoteEz-Server/app.cs d; H10="$(vm_head)"; run --dry-run
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H10" ]; echo $?)"
check "log DRY-RUN" "$(grep -q 'DRY-RUN' "$LOG"; echo $?)"

echo "S11: VM na innej galezi niz BRANCH -> przelacza na main"
git -C "$T/vm" checkout -q -b develop
run
check "galaz main" "$([ "$(git -C "$T/vm" branch --show-current)" = "main" ]; echo $?)"
check "HEAD == origin" "$([ "$(vm_head)" = "$(origin_head)" ]; echo $?)"

echo "S12: sledzona galaz jest ZA biezacym commitem -> nie cofa produkcji (bez --force)"
git -C "$T/seed" checkout -q -b develop
echo ahead > "$T/seed/NoteEz-Server/NoteEz-Server/app.cs"
git -C "$T/seed" add -A && git -C "$T/seed" commit -q -m ahead && git -C "$T/seed" push -q origin develop 2>/dev/null
git -C "$T/vm" fetch -q origin && git -C "$T/vm" checkout -q -B develop origin/develop
H12="$(vm_head)"; B12="$(builds)"; run
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H12" ]; echo $?)"
check "nadal na develop" "$([ "$(git -C "$T/vm" branch --show-current)" = "develop" ]; echo $?)"
check "log: nie cofam produkcji" "$(grep -q 'ZA biezacym commitem' "$LOG"; echo $?)"
check "bez --build" "$([ "$(builds)" = "$B12" ]; echo $?)"
run --force
check "--force pozwala (przechodzi na main)" "$([ "$(git -C "$T/vm" branch --show-current)" = "main" ]; echo $?)"

echo "S13: galaz sledzona nie zawiera skryptu deploy -> nie przelacza (skrypt zostaje na VM)"
git -C "$T/seed" checkout -q main 2>/dev/null; git -C "$T/seed" checkout -q -b noscript
git -C "$T/seed" rm -q scripts/deploy-backend.sh && git -C "$T/seed" commit -q -m "no script" && git -C "$T/seed" push -q origin noscript 2>/dev/null
sed 's/^BRANCH=.*/BRANCH=noscript/' "$T/home/conf" > "$T/home/conf_noscript"
git -C "$T/vm" checkout -q main; H13="$(vm_head)"; CONF_FILE="$T/home/conf_noscript" run
check "galaz bez zmian (main)" "$([ "$(git -C "$T/vm" branch --show-current)" = "main" ]; echo $?)"
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H13" ]; echo $?)"
check "skrypt nadal istnieje na VM" "$([ -f "$T/vm/scripts/deploy-backend.sh" ]; echo $?)"
check "log: nie zawiera skryptu" "$(grep -q 'nie zawiera scripts/deploy-backend.sh' "$LOG"; echo $?)"

echo "S14: origin/BRANCH to INNA linia historii (nie kontynuacja wdrozonego) -> nie wdraza automatycznie"
git -C "$T/vm" checkout -q main
git -C "$T/vm" commit -q --allow-empty -m "local only"            # lokalny main rozjedzie sie z origin/main
git -C "$T/seed" checkout -q main && push_commit NoteEz-Frontend/f.js diverge
git -C "$T/vm" checkout -q -b other; H14="$(vm_head)"; B14="$(builds)"; run
check "zostaje na galezi other" "$([ "$(git -C "$T/vm" branch --show-current)" = "other" ]; echo $?)"
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H14" ]; echo $?)"
check "skrypt nadal istnieje na VM" "$([ -f "$T/vm/scripts/deploy-backend.sh" ]; echo $?)"
check "log: nie jest kontynuacja" "$(grep -q 'nie jest kontynuacja' "$LOG"; echo $?)"
check "bez --build" "$([ "$(builds)" = "$B14" ]; echo $?)"

echo "S15: --force przy rozjechanej historii -> fast-forward sie nie uda, wraca na poprzednia galaz"
run --force
check "wrocono na galaz other" "$([ "$(git -C "$T/vm" branch --show-current)" = "other" ]; echo $?)"
check "HEAD bez zmian" "$([ "$(vm_head)" = "$H14" ]; echo $?)"
check "skrypt nadal istnieje na VM" "$([ -f "$T/vm/scripts/deploy-backend.sh" ]; echo $?)"
check "log: wracam na poprzednia galaz" "$(grep -q 'wracam na poprzednia galaz' "$LOG"; echo $?)"

echo "S16: reczny pull zmienil backend, kontenery zbudowane ze starszego kodu (DRIFT) -> przebudowa"
git -C "$T/vm" fetch -q origin; git -C "$T/vm" checkout -q -B main origin/main; rm -f "$T/home/state/blocked_sha"
BUILT_FILE="$T/home/state/built_sha"; git -C "$T/vm" rev-parse HEAD > "$BUILT_FILE"
push_commit NoteEz-Server/NoteEz-Server/app.cs "manual-pull"
git -C "$T/vm" fetch -q origin && git -C "$T/vm" merge -q --ff-only origin/main        # reczny pull: kontenery NIE sa przebudowane
B16="$(builds)"
HOME="$T/home" DEPLOY_CONF="$T/home/conf" PATH="$T/stubs:$PATH" bash "$T/vm/scripts/deploy-backend.sh" --status > "$T/status.txt" 2>&1
check "--status ostrzega o rozjezdzie" "$(grep -q 'UWAGA: backend w repo rozni sie' "$T/status.txt"; echo $?)"
run
check "przebudowano (--build)" "$([ "$(builds)" -gt "$B16" ]; echo $?)"
check "log: START przebudowa" "$(grep -q 'START przebudowa' "$LOG"; echo $?)"
check "built_sha == HEAD" "$([ "$(cat "$BUILT_FILE")" = "$(vm_head)" ]; echo $?)"

echo "S17: po przebudowie kolejne uruchomienie nic nie buduje; --rebuild wymusza przebudowe"
B17="$(builds)"; run
check "bez nowego --build" "$([ "$(builds)" = "$B17" ]; echo $?)"
run --rebuild
check "--rebuild buduje" "$([ "$(builds)" -gt "$B17" ]; echo $?)"


echo; echo "WYNIK: $PASS ok, $FAIL niepowodzen"
echo "--- log deploy.log:"; cat "$LOG"
rm -rf "$T"
[ "$FAIL" = 0 ]
