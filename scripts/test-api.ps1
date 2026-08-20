# FamLedger API smoke test (uses curl + cookie jar)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root
$Base = "http://localhost:8080"
$CookieJar = Join-Path $env:TEMP "famledger-smoke-cookies.txt"
$BodyFile = Join-Path $env:TEMP "famledger-smoke-body.json"
Remove-Item $CookieJar -ErrorAction SilentlyContinue
$results = @()

function Test-Endpoint {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [hashtable]$Body = $null,
        [int[]]$ExpectStatus = @(200)
    )

    if ($Body) {
        $Body | ConvertTo-Json -Compress | Set-Content -Path $BodyFile -Encoding UTF8 -NoNewline
        $raw = curl.exe -s -w "`n%{http_code}" -X $Method "$Base$Path" -H "Accept: application/json" -H "Content-Type: application/json" -d "@$BodyFile" -b $CookieJar -c $CookieJar
    }
    else {
        $raw = curl.exe -s -w "`n%{http_code}" -X $Method "$Base$Path" -H "Accept: application/json" -b $CookieJar -c $CookieJar
    }

    $lines = $raw -split "`n"
    $code = [int]$lines[-1]
    $content = ($lines[0..($lines.Length - 2)] -join "`n")
    $ok = $ExpectStatus -contains $code
    Write-Host ("[{0}] {1} {2} -> {3}" -f $(if ($ok) { "OK" } else { "FAIL" }), $Method, $Path, $code)
    if (-not $ok) {
        $preview = if ($content.Length -gt 350) { $content.Substring(0, 350) + "..." } else { $content }
        if ($preview) { Write-Host "  $preview" }
    }
    $script:results += [PSCustomObject]@{ Ok = $ok; Method = $Method; Path = $Path; Code = $code; Body = $content }
    return $content
}

Write-Host "=== Auth ==="
$token = [guid]::NewGuid().ToString("N")
docker compose exec -T redis redis-cli SET "login:bot:$token" 861420063 EX 600 | Out-Null
Test-Endpoint -Method POST -Path "/api/auth/bot" -Body @{ token = $token } | Out-Null
if (-not $results[-1].Ok) { exit 1 }

Write-Host "`n=== Read endpoints ==="
Test-Endpoint -Path "/api/me" | Out-Null
Test-Endpoint -Path "/api/auth/me" | Out-Null
Test-Endpoint -Path "/api/contexts" | Out-Null
Test-Endpoint -Path "/api/settings" | Out-Null
Test-Endpoint -Path "/api/family" | Out-Null
Test-Endpoint -Path "/api/family/settings" | Out-Null
Test-Endpoint -Path "/api/dashboard/summary" | Out-Null
Test-Endpoint -Path "/api/transactions" | Out-Null
Test-Endpoint -Path "/api/categories" | Out-Null
Test-Endpoint -Path "/api/plan/recurring" | Out-Null
Test-Endpoint -Path "/api/plan/one-off" | Out-Null
Test-Endpoint -Path "/api/plan/incomes" | Out-Null
Test-Endpoint -Path "/api/debts" | Out-Null
Test-Endpoint -Path "/api/savings" | Out-Null
Test-Endpoint -Path "/api/webhooks" | Out-Null

Write-Host "`n=== Mutations ==="
Test-Endpoint -Method PATCH -Path "/api/users/me" -Body @{ displayName = "Mikhail" } | Out-Null
$cat = Test-Endpoint -Method POST -Path "/api/categories" -Body @{ name = "ApiTestCat" }
$catId = ($cat | ConvertFrom-Json).id
$tx = Test-Endpoint -Method POST -Path "/api/transactions" -Body @{ amount = 100; currency = "RSD"; categoryId = $catId; note = "smoke" }
$txId = ($tx | ConvertFrom-Json).id
Test-Endpoint -Method POST -Path "/api/plan/recurring" -Body @{ name = "Rent"; amount = 500; currency = "RSD"; chargeDay = 1 } | Out-Null
Test-Endpoint -Method POST -Path "/api/plan/one-off" -Body @{ name = "Gift"; amount = 50; currency = "RSD" } | Out-Null
Test-Endpoint -Method POST -Path "/api/plan/incomes" -Body @{ name = "Salary"; amount = 1000; currency = "RSD" } | Out-Null
$debt = Test-Endpoint -Method POST -Path "/api/debts" -Body @{ counterpartyName = "Bob"; direction = 0 }
$debtId = ($debt | ConvertFrom-Json).id
Test-Endpoint -Method POST -Path "/api/debts/$debtId/entries" -Body @{ amount = 100; currency = "RSD"; description = "loan" } | Out-Null
Test-Endpoint -Method POST -Path "/api/savings/deposit" -Body @{ amount = 10 } | Out-Null
Test-Endpoint -Method POST -Path "/api/savings/plan" -Body @{ plannedAmount = 100 } | Out-Null
$goal = Test-Endpoint -Method POST -Path "/api/savings/goals" -Body @{ name = "Vacation"; targetAmount = 500 }
$goalId = ($goal | ConvertFrom-Json).id
Test-Endpoint -Method POST -Path "/api/savings/goals/$goalId/contribute" -Body @{ amount = 50 } | Out-Null

Write-Host "`n=== Delete / update ==="
Test-Endpoint -Method DELETE -Path "/api/transactions/$txId" | Out-Null
Test-Endpoint -Method PATCH -Path "/api/categories/$catId" -Body @{ name = "ApiTestCatRenamed" } | Out-Null
$recurring = Test-Endpoint -Path "/api/plan/recurring"
$recurringItem = ($recurring | ConvertFrom-Json)[0]
if ($recurringItem) {
    Test-Endpoint -Method PATCH -Path "/api/plan/recurring/$($recurringItem.id)/toggle-paid" | Out-Null
    $recurringExpenseId = if ($recurringItem.recurringExpenseId) { $recurringItem.recurringExpenseId } else { $recurringItem.id }
    Test-Endpoint -Method PATCH -Path "/api/plan/recurring/expenses/$recurringExpenseId" -Body @{ name = "RentUpdated"; amount = 550; currency = "RSD"; chargeDay = 2 } | Out-Null
    Test-Endpoint -Method DELETE -Path "/api/plan/recurring/expenses/$recurringExpenseId" | Out-Null
}
$oneOff = Test-Endpoint -Path "/api/plan/one-off"
$oneOffItem = ($oneOff | ConvertFrom-Json)[0]
if ($oneOffItem) {
    Test-Endpoint -Method PATCH -Path "/api/plan/one-off/$($oneOffItem.id)/toggle-paid" | Out-Null
    Test-Endpoint -Method DELETE -Path "/api/plan/one-off/$($oneOffItem.id)" | Out-Null
}
$income = Test-Endpoint -Path "/api/plan/incomes"
$incomeItem = ($income | ConvertFrom-Json)[0]
if ($incomeItem) {
    Test-Endpoint -Method PATCH -Path "/api/plan/incomes/$($incomeItem.id)" -Body @{ name = "SalaryUpdated"; amount = 1100; currency = "RSD" } | Out-Null
    Test-Endpoint -Method DELETE -Path "/api/plan/incomes/$($incomeItem.id)" | Out-Null
}
Test-Endpoint -Method DELETE -Path "/api/debts/$debtId" | Out-Null
Test-Endpoint -Method DELETE -Path "/api/savings/goals/$goalId" | Out-Null
Test-Endpoint -Method DELETE -Path "/api/categories/$catId" | Out-Null

Write-Host "`n=== Re-fetch ==="
Test-Endpoint -Path "/api/dashboard/summary" | Out-Null
Test-Endpoint -Path "/api/debts" | Out-Null
Test-Endpoint -Path "/api/savings" | Out-Null
Test-Endpoint -Path "/api/transactions" | Out-Null
Test-Endpoint -Path "/api/plan/recurring" | Out-Null
Test-Endpoint -Path "/api/plan/one-off" | Out-Null
Test-Endpoint -Path "/api/plan/incomes" | Out-Null

Write-Host "`n=== Logout ==="
Test-Endpoint -Method POST -Path "/api/auth/logout" | Out-Null
Test-Endpoint -Path "/api/me" -ExpectStatus @(401) | Out-Null

$failed = @($results | Where-Object { -not $_.Ok })
Write-Host "`n=== $($results.Count) tests, $($failed.Count) failed ==="
if ($failed.Count -gt 0) {
    $failed | Format-Table Method, Path, Code -AutoSize
    exit 1
}
