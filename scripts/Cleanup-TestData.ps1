<#
.SYNOPSIS
  Removes all test data from the farm scheduling app.

.DESCRIPTION
  Deletes and recreates the workers and availability Cosmos DB containers
  in the FarmScheduler database. This is the fastest way to purge all
  documents. The barnConfigs and blackouts containers are left untouched.

.PARAMETER CosmosAccountName
  The Cosmos DB account name (e.g., cosmos-farm-dev-w7mddf36mvnxm)

.PARAMETER ResourceGroupName
  The Azure resource group that contains the Cosmos DB account

.EXAMPLE
  .\scripts\Cleanup-TestData.ps1 -CosmosAccountName cosmos-farm-dev-w7mddf36mvnxm -ResourceGroupName rg-farm-dev
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$CosmosAccountName,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName
)

$ErrorActionPreference = "Stop"
$DatabaseName = "FarmScheduler"

Write-Host "=== Farm Scheduler Test Data Cleanup ===" -ForegroundColor Cyan
Write-Host "Cosmos account : $CosmosAccountName"
Write-Host "Resource group : $ResourceGroupName"
Write-Host "Database       : $DatabaseName"
Write-Host ""

function Reset-Container {
    param(
        [string]$ContainerName,
        [string]$PartitionKeyPath,
        [int]$DefaultTtl = -1
    )

    Write-Host "--- Cleaning container: $ContainerName ---" -ForegroundColor Yellow

    # Delete the container (ignore errors if it doesn't exist)
    Write-Host "  Deleting container..."
    az cosmosdb sql container delete `
        --account-name $CosmosAccountName `
        --resource-group $ResourceGroupName `
        --database-name $DatabaseName `
        --name $ContainerName `
        --yes 2>$null | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Container did not exist, skipping delete." -ForegroundColor DarkYellow
    }

    # Recreate the container
    Write-Host "  Recreating container (partition key: $PartitionKeyPath)..."
    $createArgs = @(
        "cosmosdb", "sql", "container", "create",
        "--account-name", $CosmosAccountName,
        "--resource-group", $ResourceGroupName,
        "--database-name", $DatabaseName,
        "--name", $ContainerName,
        "--partition-key-path", $PartitionKeyPath
    )

    if ($DefaultTtl -ge 0) {
        $createArgs += "--default-ttl"
        $createArgs += $DefaultTtl.ToString()
    }

    az @createArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to recreate container '$ContainerName'."
    }

    Write-Host "  $(([char]0x2713)) $ContainerName reset successfully" -ForegroundColor Green
}

Reset-Container -ContainerName "workers" -PartitionKeyPath "/id"
Write-Host ""
Reset-Container -ContainerName "availability" -PartitionKeyPath "/windowStart" -DefaultTtl 2592000

Write-Host ""
Write-Host "=== Cleanup Complete $(([char]0x2705)) ===" -ForegroundColor Green
Write-Host "Both workers and availability containers have been reset."
