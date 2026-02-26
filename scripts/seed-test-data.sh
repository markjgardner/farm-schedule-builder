#!/usr/bin/env bash
# seed-test-data.sh
#
# Populates the farm scheduling app with realistic test data:
#   - 6 workers (2 full-time, 2 part-time, 2 with mixed schedules)
#   - Availability for 2 scheduling windows (4 weeks of data)
#
# Prerequisites:
#   - Azure CLI logged in (az login)
#   - jq installed
#
# Usage:
#   chmod +x scripts/seed-test-data.sh
#   ./scripts/seed-test-data.sh <storage-account-name>
#
# Example:
#   ./scripts/seed-test-data.sh stfarmdevw7mddf36mvnxm

set -euo pipefail

STORAGE_ACCOUNT="${1:?Usage: $0 <storage-account-name>}"
WORKERS_TABLE="Workers"
AVAILABILITY_TABLE="Availability"

echo "=== Farm Scheduler Test Data Seeder ==="
echo "Storage account: $STORAGE_ACCOUNT"
echo ""

# Ensure tables exist
echo "Ensuring tables exist..."
az storage table create --name "$WORKERS_TABLE" --account-name "$STORAGE_ACCOUNT" --auth-mode login 2>/dev/null || true
az storage table create --name "$AVAILABILITY_TABLE" --account-name "$STORAGE_ACCOUNT" --auth-mode login 2>/dev/null || true

# --- Workers ---
echo ""
echo "--- Creating Workers ---"

declare -A WORKER_NAMES=(
  ["alice-morgan"]="Alice Morgan"
  ["bob-chen"]="Bob Chen"
  ["charlie-davis"]="Charlie Davis"
  ["diana-patel"]="Diana Patel"
  ["evan-santos"]="Evan Santos"
  ["fiona-kelly"]="Fiona Kelly"
)

declare -A WORKER_EMAILS=(
  ["alice-morgan"]="alice@example.com"
  ["bob-chen"]="bob@example.com"
  ["charlie-davis"]="charlie@example.com"
  ["diana-patel"]="diana@example.com"
  ["evan-santos"]="evan@example.com"
  ["fiona-kelly"]="fiona@example.com"
)

# Alice and Bob are admins (full-time leads)
declare -A WORKER_ADMIN=(
  ["alice-morgan"]="true"
  ["bob-chen"]="true"
  ["charlie-davis"]="false"
  ["diana-patel"]="false"
  ["evan-santos"]="false"
  ["fiona-kelly"]="false"
)

for id in alice-morgan bob-chen charlie-davis diana-patel evan-santos fiona-kelly; do
  echo "  Creating worker: ${WORKER_NAMES[$id]} ($id)"
  az storage entity insert \
    --table-name "$WORKERS_TABLE" \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --if-exists replace \
    --entity \
      PartitionKey=worker \
      RowKey="$id" \
      DisplayName="${WORKER_NAMES[$id]}" \
      Email="${WORKER_EMAILS[$id]}" \
      IsActive=true@odata.type=Edm.Boolean \
      IsAdmin="${WORKER_ADMIN[$id]}"@odata.type=Edm.Boolean \
    2>/dev/null
done
echo "  ✓ 6 workers created"

# --- Availability ---
# Compute next two Monday-start 2-week windows from today
echo ""
echo "--- Computing scheduling windows ---"

# Find next Monday
TODAY=$(date -u +%Y-%m-%d)
DOW=$(date -u +%u) # 1=Monday ... 7=Sunday
DAYS_UNTIL_MONDAY=$(( (8 - DOW) % 7 ))
if [ "$DAYS_UNTIL_MONDAY" -eq 0 ]; then
  DAYS_UNTIL_MONDAY=7
fi

WINDOW1_START=$(date -u -d "$TODAY + $DAYS_UNTIL_MONDAY days" +%Y-%m-%d 2>/dev/null || date -u -v+"${DAYS_UNTIL_MONDAY}d" +%Y-%m-%d)
WINDOW1_END=$(date -u -d "$WINDOW1_START + 13 days" +%Y-%m-%d 2>/dev/null || date -u -j -f "%Y-%m-%d" "$WINDOW1_START" -v+13d +%Y-%m-%d)
WINDOW2_START=$(date -u -d "$WINDOW1_START + 14 days" +%Y-%m-%d 2>/dev/null || date -u -j -f "%Y-%m-%d" "$WINDOW1_START" -v+14d +%Y-%m-%d)
WINDOW2_END=$(date -u -d "$WINDOW2_START + 13 days" +%Y-%m-%d 2>/dev/null || date -u -j -f "%Y-%m-%d" "$WINDOW2_START" -v+13d +%Y-%m-%d)

echo "  Window 1: $WINDOW1_START to $WINDOW1_END"
echo "  Window 2: $WINDOW2_START to $WINDOW2_END"

insert_availability() {
  local window_start="$1"
  local worker_id="$2"
  local date="$3"
  local status="$4"

  az storage entity insert \
    --table-name "$AVAILABILITY_TABLE" \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --if-exists replace \
    --entity \
      PartitionKey="$window_start" \
      RowKey="${worker_id}_${date}" \
      WorkerId="$worker_id" \
      Date="$date" \
      Status="$status" \
    2>/dev/null
}

generate_dates() {
  local start="$1"
  local end="$2"
  local current="$start"
  while [[ "$current" < "$end" ]] || [[ "$current" == "$end" ]]; do
    echo "$current"
    current=$(date -u -d "$current + 1 day" +%Y-%m-%d 2>/dev/null || date -u -j -f "%Y-%m-%d" "$current" -v+1d +%Y-%m-%d)
  done
}

seed_window() {
  local window_start="$1"
  local window_end="$2"
  local window_label="$3"

  echo ""
  echo "--- Seeding availability: $window_label ($window_start to $window_end) ---"

  local dates=()
  while IFS= read -r d; do dates+=("$d"); done < <(generate_dates "$window_start" "$window_end")

  for date in "${dates[@]}"; do
    local dow
    dow=$(date -u -d "$date" +%u 2>/dev/null || date -u -j -f "%Y-%m-%d" "$date" +%u)

    # Alice Morgan — full-time, available every day
    insert_availability "$window_start" "alice-morgan" "$date" "Available"

    # Bob Chen — full-time lead, takes Wednesdays off
    if [ "$dow" -eq 3 ]; then
      insert_availability "$window_start" "bob-chen" "$date" "NotAvailable"
    else
      insert_availability "$window_start" "bob-chen" "$date" "Available"
    fi

    # Charlie Davis — mornings only on weekdays, not available weekends
    if [ "$dow" -ge 6 ]; then
      insert_availability "$window_start" "charlie-davis" "$date" "NotAvailable"
    else
      insert_availability "$window_start" "charlie-davis" "$date" "MorningOnly"
    fi

    # Diana Patel — evenings only (works another job during the day)
    insert_availability "$window_start" "diana-patel" "$date" "EveningOnly"

    # Evan Santos — available weekdays, mornings only on weekends
    if [ "$dow" -ge 6 ]; then
      insert_availability "$window_start" "evan-santos" "$date" "MorningOnly"
    else
      insert_availability "$window_start" "evan-santos" "$date" "Available"
    fi

    # Fiona Kelly — not available Mon/Tue, available rest of week
    if [ "$dow" -le 2 ]; then
      insert_availability "$window_start" "fiona-kelly" "$date" "NotAvailable"
    else
      insert_availability "$window_start" "fiona-kelly" "$date" "Available"
    fi

    echo "    $date ✓"
  done
  echo "  ✓ Window $window_label seeded"
}

seed_window "$WINDOW1_START" "$WINDOW1_END" "Window 1"
seed_window "$WINDOW2_START" "$WINDOW2_END" "Window 2"

echo ""
echo "=== Test Data Summary ==="
echo "Workers: 6"
echo "  Alice Morgan   — full-time, admin, available every day"
echo "  Bob Chen       — full-time, admin, Wednesdays off"
echo "  Charlie Davis  — mornings only weekdays, weekends off"
echo "  Diana Patel    — evenings only every day"
echo "  Evan Santos    — weekdays full, weekends morning only"
echo "  Fiona Kelly    — Mon/Tue off, otherwise available"
echo ""
echo "Availability:"
echo "  Window 1: $WINDOW1_START to $WINDOW1_END (14 days × 6 workers = 84 records)"
echo "  Window 2: $WINDOW2_START to $WINDOW2_END (14 days × 6 workers = 84 records)"
echo ""
echo "Total: 6 workers + 168 availability records"
echo "Done! ✅"
