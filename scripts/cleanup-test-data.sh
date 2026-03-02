#!/usr/bin/env bash
# cleanup-test-data.sh
#
# Removes all test data seeded by seed-test-data.sh.
# Deletes and recreates the workers and availability Cosmos DB containers
# to quickly purge all seeded documents.
#
# Usage:
#   chmod +x scripts/cleanup-test-data.sh
#   ./scripts/cleanup-test-data.sh <cosmos-account-name> <resource-group-name>
#
# Example:
#   ./scripts/cleanup-test-data.sh cosmos-farm-dev rg-farm-dev

set -euo pipefail

COSMOS_ACCOUNT="${1:?Usage: $0 <cosmos-account-name> <resource-group-name>}"
RESOURCE_GROUP="${2:?Usage: $0 <cosmos-account-name> <resource-group-name>}"
DATABASE_NAME="FarmScheduler"

echo "=== Farm Scheduler Test Data Cleanup ==="
echo "Cosmos DB account: $COSMOS_ACCOUNT"
echo "Resource group:    $RESOURCE_GROUP"
echo "Database:          $DATABASE_NAME"
echo ""

# --- Clean workers container (partition key /id, no TTL) ---
echo "--- Cleaning container: workers ---"
echo "  Deleting container..."
az cosmosdb sql container delete \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "$DATABASE_NAME" \
  --name workers \
  --yes 2>/dev/null || true

echo "  Recreating container..."
az cosmosdb sql container create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "$DATABASE_NAME" \
  --name workers \
  --partition-key-path "/id" > /dev/null

echo "  ✓ workers container recreated"

echo ""

# --- Clean availability container (partition key /windowStart, 30-day TTL) ---
echo "--- Cleaning container: availability ---"
echo "  Deleting container..."
az cosmosdb sql container delete \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "$DATABASE_NAME" \
  --name availability \
  --yes 2>/dev/null || true

echo "  Recreating container..."
az cosmosdb sql container create \
  --account-name "$COSMOS_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --database-name "$DATABASE_NAME" \
  --name availability \
  --partition-key-path "/windowStart" \
  --default-ttl 2592000 > /dev/null

echo "  ✓ availability container recreated"

echo ""
echo "=== Cleanup Complete ✅ ==="
echo "Both workers and availability containers have been recreated."
