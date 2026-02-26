#!/usr/bin/env bash
# cleanup-test-data.sh
#
# Removes all test data seeded by seed-test-data.sh.
# Deletes all entities from the Workers and Availability tables.
#
# Usage:
#   chmod +x scripts/cleanup-test-data.sh
#   ./scripts/cleanup-test-data.sh <storage-account-name>
#
# Example:
#   ./scripts/cleanup-test-data.sh stfarmdevw7mddf36mvnxm

set -euo pipefail

STORAGE_ACCOUNT="${1:?Usage: $0 <storage-account-name>}"
WORKERS_TABLE="Workers"
AVAILABILITY_TABLE="Availability"

echo "=== Farm Scheduler Test Data Cleanup ==="
echo "Storage account: $STORAGE_ACCOUNT"
echo ""

delete_all_entities() {
  local table_name="$1"
  echo "--- Cleaning table: $table_name ---"

  local entities
  entities=$(az storage entity query \
    --table-name "$table_name" \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --query "items[].{PartitionKey:PartitionKey, RowKey:RowKey}" \
    -o json 2>/dev/null || echo "[]")

  local count
  count=$(echo "$entities" | jq length)

  if [ "$count" -eq 0 ]; then
    echo "  No entities found, table is clean."
    return
  fi

  echo "  Found $count entities to delete..."

  echo "$entities" | jq -c '.[]' | while read -r entity; do
    local pk rk
    pk=$(echo "$entity" | jq -r '.PartitionKey')
    rk=$(echo "$entity" | jq -r '.RowKey')

    az storage entity delete \
      --table-name "$table_name" \
      --account-name "$STORAGE_ACCOUNT" \
      --auth-mode login \
      --partition-key "$pk" \
      --row-key "$rk" \
      2>/dev/null

    echo "    Deleted: $pk / $rk"
  done

  echo "  ✓ $table_name cleaned ($count entities removed)"
}

delete_all_entities "$WORKERS_TABLE"
echo ""
delete_all_entities "$AVAILABILITY_TABLE"

echo ""
echo "=== Cleanup Complete ✅ ==="
echo "Both Workers and Availability tables have been emptied."
