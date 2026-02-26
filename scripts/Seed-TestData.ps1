<#
.SYNOPSIS
  Populates the farm scheduling app with realistic test data.

.DESCRIPTION
  Creates 6 workers and 2 scheduling windows (4 weeks) of availability data
  directly in Azure Table Storage.

.PARAMETER StorageAccountName
  The Azure Storage Account name (e.g., stfarmdevw7mddf36mvnxm)

.EXAMPLE
  .\scripts\Seed-TestData.ps1 -StorageAccountName stfarmdevw7mddf36mvnxm
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName
)

$ErrorActionPreference = "Stop"

$WorkersTable = "Workers"
$AvailabilityTable = "Availability"

Write-Host "=== Farm Scheduler Test Data Seeder ===" -ForegroundColor Cyan
Write-Host "Storage account: $StorageAccountName"
Write-Host ""

# Ensure tables exist
Write-Host "Ensuring tables exist..."
az storage table create --name $WorkersTable --account-name $StorageAccountName --auth-mode login 2>$null | Out-Null
az storage table create --name $AvailabilityTable --account-name $StorageAccountName --auth-mode login 2>$null | Out-Null

# --- Workers ---
Write-Host ""
Write-Host "--- Creating Workers ---" -ForegroundColor Yellow

$Workers = @(
    @{ Id="alice-morgan";   Name="Alice Morgan";   Email="alice@example.com";   Admin="true"  }
    @{ Id="bob-chen";       Name="Bob Chen";       Email="bob@example.com";     Admin="true"  }
    @{ Id="charlie-davis";  Name="Charlie Davis";  Email="charlie@example.com"; Admin="false" }
    @{ Id="diana-patel";    Name="Diana Patel";    Email="diana@example.com";   Admin="false" }
    @{ Id="evan-santos";    Name="Evan Santos";    Email="evan@example.com";    Admin="false" }
    @{ Id="fiona-kelly";    Name="Fiona Kelly";    Email="fiona@example.com";   Admin="false" }
)

foreach ($w in $Workers) {
    Write-Host "  Creating worker: $($w.Name) ($($w.Id))"
    az storage entity insert `
        --table-name $WorkersTable `
        --account-name $StorageAccountName `
        --auth-mode login `
        --if-exists replace `
        --entity `
            "PartitionKey=worker" `
            "RowKey=$($w.Id)" `
            "DisplayName=$($w.Name)" `
            "Email=$($w.Email)" `
            "IsActive=true" "IsActive@odata.type=Edm.Boolean" `
            "IsAdmin=$($w.Admin)" "IsAdmin@odata.type=Edm.Boolean" `
        2>$null | Out-Null
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
    $rk = "${WorkerId}_${ds}"

    az storage entity insert `
        --table-name $AvailabilityTable `
        --account-name $StorageAccountName `
        --auth-mode login `
        --if-exists replace `
        --entity `
            PartitionKey=$ws `
            RowKey=$rk `
            WorkerId=$WorkerId `
            Date=$ds `
            Status=$Status `
        2>$null | Out-Null
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
