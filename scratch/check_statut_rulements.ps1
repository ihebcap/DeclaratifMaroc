$H = @{ Authorization = "Bearer " + (Invoke-RestMethod -Uri "http://localhost:5018/api/auth/login" -Method Post -Body '{"username":"admin","password":"admin"}' -ContentType "application/json").token }
$url = "http://localhost:518/api/rapprochement" # Wait, port 5018!
$res = Invoke-RestMethod -Uri "http://localhost:5018/api/rapprochement?soId=1&debut=2026-06-01&fin=2026-06-30&page=1&size=1000" -Method Get -Headers $H
Write-Host "Total rulements: $($res.totalCount)"
$items = $res.items
$eligibleCount = 0
$controleCount = 0
$bloqueCount = 0

function statutDe($row) {
    if ($row.declare) { return "bloque" }
    if ($row.nbFacturesAffectees -eq 0) { return "bloque" }
    if ([Math]::Abs($row.resteAAffecter) -gt 0.005) { return "controle" }
    return "eligible"
}

foreach ($r in $items) {
    $st = statutDe $r
    if ($st -eq "eligible") { $eligibleCount++ }
    elseif ($st -eq "controle") { $controleCount++ }
    else { $bloqueCount++ }
}

Write-Host "Eligibles: $eligibleCount"
Write-Host "Controles: $controleCount"
Write-Host "Bloques: $bloqueCount"

if ($eligibleCount -gt 0) {
    Write-Host "First eligible: $($items | Where-Object { (statutDe $_) -eq 'eligible' } | Select-Object -First 1 | ConvertTo-Json)"
}
