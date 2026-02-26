<#
.SYNOPSIS
  Removes all test data from the farm scheduling app.

.DESCRIPTION
  Deletes all entities from the Workers and Availability Azure Tables.

.PARAMETER StorageAccountName
  The Azure Storage Account name (e.g., stfarmdevw7mddf36mvnxm)

.EXAMPLE
  .\scripts\Cleanup-TestData.ps1 -StorageAccountName stfarmdevw7mddf36mvnxm
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName
)

$ErrorActionPreference = "Stop"

Write-Host "=== Farm Scheduler Test Data Cleanup ===" -ForegroundColor Cyan
Write-Host "Storage account: $StorageAccountName"
Write-Host ""

function Remove-AllEntities {
    param([string]$TableName)

    Write-Host "--- Cleaning table: $TableName ---" -ForegroundColor Yellow

    $entitiesJson = az storage entity query `
        --table-name $TableName `
        --account-name $StorageAccountName `
        --auth-mode login `
        --query "items[].{PartitionKey:PartitionKey, RowKey:RowKey}" `
        -o json 2>$null

    if (-not $entitiesJson -or $entitiesJson -eq "[]") {
        Write-Host "  No entities found, table is clean."
        return
    }

    $entities = $entitiesJson | ConvertFrom-Json
    $count = $entities.Count

    Write-Host "  Found $count entities to delete..."

    foreach ($entity in $entities) {
        az storage entity delete `
            --table-name $TableName `
            --account-name $StorageAccountName `
            --auth-mode login `
            --partition-key $entity.PartitionKey `
            --row-key $entity.RowKey `
            2>$null | Out-Null

        Write-Host "    Deleted: $($entity.PartitionKey) / $($entity.RowKey)"
    }

    Write-Host "  $(([char]0x2713)) $TableName cleaned ($count entities removed)" -ForegroundColor Green
}

Remove-AllEntities -TableName "Workers"
Write-Host ""
Remove-AllEntities -TableName "Availability"

Write-Host ""
Write-Host "=== Cleanup Complete $(([char]0x2705)) ===" -ForegroundColor Green
Write-Host "Both Workers and Availability tables have been emptied."
