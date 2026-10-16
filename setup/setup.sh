#!/usr/bin/env bash
# =============================================================================
#  Bibliotheca LMS - one-step install and run script (Linux / macOS)
#
#  Clones (or updates) the repository, switches to the chosen location branch,
#  restores packages, builds, prepares the SQLite database and runs the site.
#
#  Usage:
#    ./setup.sh --repo https://github.com/<team>/<repo>.git
#    ./setup.sh --repo <url> --branch location/palmerston --location "Palmerston Library" --reset-db
#
#  Options:
#    --repo <url>        Git URL (not needed when run from inside a clone)
#    --branch <name>     Branch to run (default: main)
#    --dir <path>        Folder to clone into (default: ~/Bibliotheca)
#    --location <name>   Library location name shown on the kiosk
#    --port <n>          Local port (default: 5226)
#    --reset-db          Delete the local database so it is recreated and re-seeded
#    --no-run            Install and build only
# =============================================================================
set -euo pipefail

REPO_URL=""
BRANCH="main"
INSTALL_DIR="$HOME/Bibliotheca"
LOCATION=""
PORT=5226
RESET_DB=0
NO_RUN=0
REQUIRED_SDK_MAJOR=10

while [[ $# -gt 0 ]]; do
  case "$1" in
    --repo) REPO_URL="$2"; shift 2 ;;
    --branch) BRANCH="$2"; shift 2 ;;
    --dir) INSTALL_DIR="$2"; shift 2 ;;
    --location) LOCATION="$2"; shift 2 ;;
    --port) PORT="$2"; shift 2 ;;
    --reset-db) RESET_DB=1; shift ;;
    --no-run) NO_RUN=1; shift ;;
    -h|--help) sed -n '2,22p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1 (use --help)"; exit 1 ;;
  esac
done

c_step="\033[36m"; c_ok="\033[32m"; c_err="\033[31m"; c_off="\033[0m"
step() { echo; echo -e "${c_step}[$1] $2${c_off}"; }
ok()   { echo -e "    ${c_ok}OK${c_off}  $1"; }
fail() { echo; echo -e "${c_err}SETUP STOPPED: $1${c_off}"; exit 1; }

echo -e "${c_step}==============================================${c_off}"
echo -e "${c_step}  Bibliotheca LMS - Install & Run${c_off}"
echo -e "${c_step}==============================================${c_off}"

# -----------------------------------------------------------------------------
step 1 "Checking required software"
command -v git >/dev/null 2>&1 || fail "Git is not installed (Ubuntu: sudo apt install git)."
ok "$(git --version)"
command -v dotnet >/dev/null 2>&1 || fail ".NET SDK is not installed (Ubuntu 24.04: sudo apt install dotnet-sdk-${REQUIRED_SDK_MAJOR}.0)."
SDK_LINE="$(dotnet --list-sdks | grep -E "^${REQUIRED_SDK_MAJOR}\." | tail -1 || true)"
[[ -n "$SDK_LINE" ]] || fail ".NET ${REQUIRED_SDK_MAJOR} SDK not found. Installed: $(dotnet --list-sdks | tr '\n' ' ')"
ok ".NET SDK ${SDK_LINE%% *}"

# -----------------------------------------------------------------------------
step 2 "Getting the source code"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -z "$REPO_URL" ]] && git -C "$SCRIPT_DIR" rev-parse --show-toplevel >/dev/null 2>&1; then
  REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
  ok "Using existing clone at $REPO_ROOT"
elif [[ -d "$INSTALL_DIR/.git" ]]; then
  REPO_ROOT="$(cd "$INSTALL_DIR" && pwd)"
  ok "Repository already cloned at $REPO_ROOT"
else
  [[ -n "$REPO_URL" ]] || fail "Please give the repository URL: ./setup.sh --repo https://github.com/<team>/<repo>.git"
  git clone "$REPO_URL" "$INSTALL_DIR"
  REPO_ROOT="$(cd "$INSTALL_DIR" && pwd)"
  ok "Cloned into $REPO_ROOT"
fi

# -----------------------------------------------------------------------------
step 3 "Selecting branch '$BRANCH'"
if [[ -n "$(git -C "$REPO_ROOT" status --porcelain --untracked-files=no)" ]]; then
  fail "You have uncommitted changes in $REPO_ROOT. Commit or stash them first (git stash)."
fi
git -C "$REPO_ROOT" fetch --all --prune
REMOTE_BRANCH="$(git -C "$REPO_ROOT" branch -r --list "origin/$BRANCH")"
LOCAL_BRANCH="$(git -C "$REPO_ROOT" branch --list "$BRANCH")"
if [[ -z "$REMOTE_BRANCH" && -z "$LOCAL_BRANCH" ]]; then
  echo "    Available branches:"; git -C "$REPO_ROOT" branch -r | sed 's/^/      /'
  fail "Branch '$BRANCH' does not exist. Choose one of the branches above."
fi
git -C "$REPO_ROOT" switch "$BRANCH"
[[ -n "$REMOTE_BRANCH" ]] && git -C "$REPO_ROOT" pull --ff-only
ok "On branch $BRANCH at commit $(git -C "$REPO_ROOT" rev-parse --short HEAD)"

# -----------------------------------------------------------------------------
step 4 "Locating the web project"
CSPROJ="$(find "$REPO_ROOT" -name Bibliotheca.csproj -not -path '*/bin/*' -not -path '*/obj/*' | head -1)"
[[ -n "$CSPROJ" ]] || fail "Bibliotheca.csproj was not found in $REPO_ROOT."
PROJECT_DIR="$(dirname "$CSPROJ")"
ok "$CSPROJ"

# -----------------------------------------------------------------------------
step 5 "Restoring NuGet packages and building"
dotnet restore "$CSPROJ"
dotnet build "$CSPROJ" --no-restore -c Debug -v minimal
ok "Build succeeded"

# -----------------------------------------------------------------------------
step 6 "Preparing the database"
shopt -s nullglob
DB_FILES=("$PROJECT_DIR"/bibliotheca.db*)
if [[ $RESET_DB -eq 1 && ${#DB_FILES[@]} -gt 0 ]]; then
  pkill -f "Bibliotheca.dll" 2>/dev/null || true
  rm -f "${DB_FILES[@]}"
  ok "Old database deleted - a fresh one will be created and seeded"
elif [[ ${#DB_FILES[@]} -gt 0 ]]; then
  ok "Existing database kept (use --reset-db for a clean, freshly seeded copy)"
else
  ok "No database yet - it will be created and seeded automatically on first start"
fi
if [[ -d "$PROJECT_DIR/Migrations" ]]; then
  echo "    EF Core migrations found - applying them ..."
  if ! dotnet tool list -g | grep -q dotnet-ef; then
    dotnet tool install --global dotnet-ef --version "${REQUIRED_SDK_MAJOR}.*"
    export PATH="$PATH:$HOME/.dotnet/tools"
  fi
  dotnet ef database update --project "$CSPROJ"
  ok "Migrations applied"
fi

# -----------------------------------------------------------------------------
step 7 "Ready"
BASE="http://localhost:$PORT"
cat <<EOF

  Public catalogue      $BASE/
  Self-service kiosk    $BASE/Kiosk
  Staff login           $BASE/Account/Login

  Staff accounts: admin@bibliotheca.com / Admin@123
                  reception@bibliotheca.com / Reception@123
                  manager@bibliotheca.com / Manager@123
  Kiosk test cards: CRD-1001 ... CRD-1004
EOF
[[ -n "$LOCATION" ]] && echo "  Kiosk location name: $LOCATION"
echo

if [[ $NO_RUN -eq 1 ]]; then
  echo "Setup finished. Start the site later with:"
  echo "  cd \"$PROJECT_DIR\" && dotnet run --launch-profile http -- --urls $BASE"
  exit 0
fi

echo -e "${c_ok}Starting the website - press Ctrl+C to stop it.${c_off}"
cd "$PROJECT_DIR"   # the SQLite file lives next to the project
RUN_ARGS=(run --no-build --launch-profile http -- --urls "$BASE")
[[ -n "$LOCATION" ]] && RUN_ARGS+=("--Kiosk:BranchName=$LOCATION")
exec dotnet "${RUN_ARGS[@]}"
