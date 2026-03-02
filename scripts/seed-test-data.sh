#!/usr/bin/env bash
# seed-test-data.sh
#
# Populates the farm scheduling app with realistic test data via the
# Cosmos DB REST API:
#   - 6 workers (2 full-time, 2 part-time, 2 with mixed schedules)
#   - Availability for 2 scheduling windows (4 weeks of data)
#
# Prerequisites:
#   - Azure CLI logged in (az login)
#   - openssl, base64, xxd
#
# Usage:
#   chmod +x scripts/seed-test-data.sh
#   ./scripts/seed-test-data.sh <cosmos-account-name> <resource-group-name>
#
# Example:
#   ./scripts/seed-test-data.sh cosmos-farmdev-w7m rg-farmdev

set -euo pipefail

COSMOS_ACCOUNT="${1:?Usage: $0 <cosmos-account-name> <resource-group-name>}"
RESOURCE_GROUP="${2:?Usage: $0 <cosmos-account-name> <resource-group-name>}"
DATABASE="FarmScheduler"
WORKERS_CONTAINER="workers"
AVAILABILITY_CONTAINER="availability"

echo "=== Farm Scheduler Test Data Seeder ==="
echo "Cosmos account: $COSMOS_ACCOUNT"
echo "Resource group: $RESOURCE_GROUP"
echo ""

# Fetch Cosmos DB endpoint and primary key
echo "Fetching Cosmos DB credentials..."
COSMOS_ENDPOINT=$(az cosmosdb show --name "$COSMOS_ACCOUNT" --resource-group "$RESOURCE_GROUP" --query documentEndpoint -o tsv)
COSMOS_KEY=$(az cosmosdb keys list --name "$COSMOS_ACCOUNT" --resource-group "$RESOURCE_GROUP" --query primaryMasterKey -o tsv)
echo "  Endpoint: $COSMOS_ENDPOINT"

# Generate HMAC-SHA256 authorization header for the Cosmos DB REST API
cosmos_auth_header() {
  local verb="${1,,}" resource_type="${2,,}" resource_id="$3" date="${4,,}"
  local key_hex
  key_hex=$(echo -n "$COSMOS_KEY" | base64 -d | xxd -p -c 256)
  local payload
  payload=$(printf '%s\n%s\n%s\n%s\n\n' "$verb" "$resource_type" "$resource_id" "$date")
  local sig
  sig=$(printf '%s' "$payload" | openssl dgst -sha256 -mac HMAC -macopt "hexkey:${key_hex}" -binary | base64 -w 0)
  local token="type=master&ver=1.0&sig=${sig}"
  printf '%s' "$token" | sed 's/=/%3D/g; s/&/%26/g; s/+/%2B/g; s/\//%2F/g'
}

# Upsert a JSON document into a Cosmos DB container
upsert_document() {
  local container="$1" partition_key_value="$2" json_body="$3"
  local resource_id="dbs/${DATABASE}/colls/${container}"
  local url="${COSMOS_ENDPOINT}dbs/${DATABASE}/colls/${container}/docs"
  local ms_date
  ms_date=$(date -u "+%a, %d %b %Y %H:%M:%S GMT")
  local auth
  auth=$(cosmos_auth_header "POST" "docs" "$resource_id" "$ms_date")
  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "$url" \
    -H "Authorization: ${auth}" \
    -H "x-ms-date: ${ms_date}" \
    -H "x-ms-version: 2018-12-31" \
    -H "x-ms-documentdb-is-upsert: True" \
    -H "x-ms-documentdb-partitionkey: [\"${partition_key_value}\"]" \
    -H "Content-Type: application/json" \
    -d "$json_body")
  if [[ "$http_code" -lt 200 || "$http_code" -ge 300 ]]; then
    echo "    ✗ Upsert failed (HTTP $http_code) for $container" >&2
    return 1
  fi
}

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
  upsert_document "$WORKERS_CONTAINER" "$id" \
    "{\"id\":\"${id}\",\"displayName\":\"${WORKER_NAMES[$id]}\",\"email\":\"${WORKER_EMAILS[$id]}\",\"isActive\":true,\"isAdmin\":${WORKER_ADMIN[$id]}}"
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

  upsert_document "$AVAILABILITY_CONTAINER" "$window_start" \
    "{\"id\":\"${worker_id}_${date}\",\"windowStart\":\"${window_start}\",\"workerId\":\"${worker_id}\",\"date\":\"${date}\",\"status\":\"${status}\"}"
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
