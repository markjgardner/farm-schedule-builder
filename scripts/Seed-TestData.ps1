<#
.SYNOPSIS
  Populates the farm scheduling app with realistic test data.

.DESCRIPTION
  Creates 6 workers and 2 scheduling windows (4 weeks) of availability data
  directly in Cosmos DB via the REST API.

.PARAMETER CosmosAccountName
  The Cosmos DB account name (e.g., cosmos-farm-dev-w7mddf36mvnxm)

.PARAMETER ResourceGroupName
  The Azure resource group containing the Cosmos DB account

.EXAMPLE
  .\scripts\Seed-TestData.ps1 -CosmosAccountName cosmos-farm-dev-w7mddf36mvnxm -ResourceGroupName rg-farm-dev
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$CosmosAccountName,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName
)

$ErrorActionPreference = "Stop"

$DatabaseName = "FarmScheduler"
$WorkersContainer = "workers"
$AvailabilityContainer = "availability"

Write-Host "=== Farm Scheduler Test Data Seeder ===" -ForegroundColor Cyan
Write-Host "Cosmos DB account: $CosmosAccountName"
Write-Host "Resource group:    $ResourceGroupName"
Write-Host ""

# --- Retrieve Cosmos DB endpoint and key ---
Write-Host "Retrieving Cosmos DB connection info..."
$CosmosEndpoint = az cosmosdb show --name $CosmosAccountName --resource-group $ResourceGroupName --query documentEndpoint -o tsv
if (-not $CosmosEndpoint) { throw "Failed to retrieve Cosmos DB endpoint." }

$CosmosKey = az cosmosdb keys list --name $CosmosAccountName --resource-group $ResourceGroupName --query primaryMasterKey -o tsv
if (-not $CosmosKey) { throw "Failed to retrieve Cosmos DB primary key." }

Write-Host "  Endpoint: $CosmosEndpoint"
Write-Host ""

# --- Helper: Generate Cosmos DB REST API authorization header ---
function Get-CosmosAuthHeader {
    param(
        [string]$Verb,
        [string]$ResourceType,
        [string]$ResourceId,
        [string]$Date,
        [string]$Key
    )

    $keyBytes = [System.Convert]::FromBase64String($Key)
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $keyBytes

    $stringToSign = "$($Verb.ToLower())`n$($ResourceType.ToLower())`n$ResourceId`n$($Date.ToLower())`n`n"
    $signatureBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($stringToSign))
    $signature = [System.Convert]::ToBase64String($signatureBytes)

    $authToken = "type=master&ver=1.0&sig=$signature"
    return [System.Uri]::EscapeDataString($authToken)
}

# --- Helper: Upsert a document into a Cosmos DB container ---
function Upsert-CosmosDocument {
    param(
        [string]$ContainerName,
        [string]$PartitionKeyValue,
        [hashtable]$Document
    )

    $resourceId = "dbs/$DatabaseName/colls/$ContainerName"
    $url = "${CosmosEndpoint}dbs/$DatabaseName/colls/$ContainerName/docs"
    $date = [DateTime]::UtcNow.ToString("R")

    $authHeader = Get-CosmosAuthHeader `
        -Verb "post" `
        -ResourceType "docs" `
        -ResourceId $resourceId `
        -Date $date `
        -Key $CosmosKey

    $headers = @{
        "Authorization"                    = $authHeader
        "x-ms-date"                        = $date
        "x-ms-version"                     = "2018-12-31"
        "x-ms-documentdb-is-upsert"        = "True"
        "x-ms-documentdb-partitionkey"     = "[`"$PartitionKeyValue`"]"
    }

    $body = $Document | ConvertTo-Json -Depth 10

    Invoke-RestMethod -Uri $url -Method Post -Headers $headers -ContentType "application/json" -Body $body | Out-Null
}

# --- Workers ---
Write-Host ""
Write-Host "--- Creating Workers ---" -ForegroundColor Yellow

$Workers = @(
    @{ Id="alice-morgan";   Name="Alice Morgan";   Email="alice@example.com";   Admin=$true  }
    @{ Id="bob-chen";       Name="Bob Chen";       Email="bob@example.com";     Admin=$true  }
    @{ Id="charlie-davis";  Name="Charlie Davis";  Email="charlie@example.com"; Admin=$false }
    @{ Id="diana-patel";    Name="Diana Patel";    Email="diana@example.com";   Admin=$false }
    @{ Id="evan-santos";    Name="Evan Santos";    Email="evan@example.com";    Admin=$false }
    @{ Id="fiona-kelly";    Name="Fiona Kelly";    Email="fiona@example.com";   Admin=$false }
)

foreach ($w in $Workers) {
    Write-Host "  Creating worker: $($w.Name) ($($w.Id))"
    Upsert-CosmosDocument `
        -ContainerName $WorkersContainer `
        -PartitionKeyValue $w.Id `
        -Document @{
            id          = $w.Id
            displayName = $w.Name
            email       = $w.Email
            isActive    = $true
            isAdmin     = $w.Admin
        }
}
Write-Host "  $(([char]0x2713)) 6 workers created" -ForegroundColor Green

# --- Compute scheduling windows ---
Write-Host ""
Write-Host "--- Computing scheduling windows ---" -ForegroundColor Yellow

$Today = [DateTime]::UtcNow.Date
$DaysUntilMonday = (8 - [int]$Today.DayOfWeek) % 7
if ($DaysUntilMonday -eq 0) { $DaysUntilMonday = 7 }

$Window1Start = $Today.AddDays($DaysUntilMonday)
$Window1End = $Window1Start.AddDays(13)
$Window2Start = $Window1Start.AddDays(14)
$Window2End = $Window2Start.AddDays(13)

Write-Host "  Window 1: $($Window1Start.ToString('yyyy-MM-dd')) to $($Window1End.ToString('yyyy-MM-dd'))"
Write-Host "  Window 2: $($Window2Start.ToString('yyyy-MM-dd')) to $($Window2End.ToString('yyyy-MM-dd'))"

function Insert-Availability {
    param($WindowStart, $WorkerId, $Date, $Status)

    $ws = $WindowStart.ToString('yyyy-MM-dd')
    $ds = $Date.ToString('yyyy-MM-dd')
    $docId = "${WorkerId}_${ds}"

    Upsert-CosmosDocument `
        -ContainerName $AvailabilityContainer `
        -PartitionKeyValue $ws `
        -Document @{
            id          = $docId
            windowStart = $ws
            workerId    = $WorkerId
            date        = $ds
            status      = $Status
        }
}

function Seed-Window {
    param($Start, $End, $Label)

    Write-Host ""
    Write-Host "--- Seeding availability: $Label ($($Start.ToString('yyyy-MM-dd')) to $($End.ToString('yyyy-MM-dd'))) ---" -ForegroundColor Yellow

    $date = $Start
    while ($date -le $End) {
        $dow = [int]$date.DayOfWeek  # 0=Sun, 1=Mon ... 6=Sat

        # Alice Morgan - full-time, available every day
        Insert-Availability -WindowStart $Start -WorkerId "alice-morgan" -Date $date -Status "Available"

        # Bob Chen - full-time, Wednesdays off
        if ($dow -eq 3) {
            Insert-Availability -WindowStart $Start -WorkerId "bob-chen" -Date $date -Status "NotAvailable"
        } else {
            Insert-Availability -WindowStart $Start -WorkerId "bob-chen" -Date $date -Status "Available"
        }

        # Charlie Davis - mornings only weekdays, weekends off
        if ($dow -eq 0 -or $dow -eq 6) {
            Insert-Availability -WindowStart $Start -WorkerId "charlie-davis" -Date $date -Status "NotAvailable"
        } else {
            Insert-Availability -WindowStart $Start -WorkerId "charlie-davis" -Date $date -Status "MorningOnly"
        }

        # Diana Patel - evenings only (works another job during the day)
        Insert-Availability -WindowStart $Start -WorkerId "diana-patel" -Date $date -Status "EveningOnly"

        # Evan Santos - weekdays full, weekends morning only
        if ($dow -eq 0 -or $dow -eq 6) {
            Insert-Availability -WindowStart $Start -WorkerId "evan-santos" -Date $date -Status "MorningOnly"
        } else {
            Insert-Availability -WindowStart $Start -WorkerId "evan-santos" -Date $date -Status "Available"
        }

        # Fiona Kelly - Mon/Tue off, otherwise available
        if ($dow -eq 1 -or $dow -eq 2) {
            Insert-Availability -WindowStart $Start -WorkerId "fiona-kelly" -Date $date -Status "NotAvailable"
        } else {
            Insert-Availability -WindowStart $Start -WorkerId "fiona-kelly" -Date $date -Status "Available"
        }

        Write-Host "    $($date.ToString('yyyy-MM-dd')) $(([char]0x2713))"
        $date = $date.AddDays(1)
    }
    Write-Host "  $(([char]0x2713)) $Label seeded" -ForegroundColor Green
}

Seed-Window -Start $Window1Start -End $Window1End -Label "Window 1"
Seed-Window -Start $Window2Start -End $Window2End -Label "Window 2"

Write-Host ""
Write-Host "=== Test Data Summary ===" -ForegroundColor Cyan
Write-Host "Workers: 6"
Write-Host "  Alice Morgan   - full-time, admin, available every day"
Write-Host "  Bob Chen       - full-time, admin, Wednesdays off"
Write-Host "  Charlie Davis  - mornings only weekdays, weekends off"
Write-Host "  Diana Patel    - evenings only every day"
Write-Host "  Evan Santos    - weekdays full, weekends morning only"
Write-Host "  Fiona Kelly    - Mon/Tue off, otherwise available"
Write-Host ""
Write-Host "Availability:"
Write-Host "  Window 1: $($Window1Start.ToString('yyyy-MM-dd')) to $($Window1End.ToString('yyyy-MM-dd')) (84 records)"
Write-Host "  Window 2: $($Window2Start.ToString('yyyy-MM-dd')) to $($Window2End.ToString('yyyy-MM-dd')) (84 records)"
Write-Host ""
Write-Host "Total: 6 workers + 168 availability records"
Write-Host "Done! $(([char]0x2705))" -ForegroundColor Green
