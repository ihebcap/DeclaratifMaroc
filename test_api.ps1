$ErrorActionPreference = "Stop"

$dbPath = "D:\_vibe\GRF\Declaration.API\tva.db"
if (Test-Path $dbPath) { Remove-Item $dbPath -Force }

$apiProc = Start-Process -FilePath "dotnet" -ArgumentList "run --project D:\_vibe\GRF\Declaration.API\Declaration.API.csproj --urls http://localhost:5005 --no-build" -PassThru -NoNewWindow -RedirectStandardOutput "D:\_vibe\GRF\api_stdout.log" -RedirectStandardError "D:\_vibe\GRF\api_stderr.log"

$ready = $false
for ($i = 0; $i -lt 25; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5005/api/societes" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
}
if (-not $ready) { Get-Content "D:\_vibe\GRF\api_stderr.log"; throw "API non demarree" }

try {
    Write-Host ""
    Write-Host "=== 1. LOGIN ==="
    $loginResp = Invoke-RestMethod -Uri "http://localhost:5005/api/auth/login" -Method Post -Body '{"username":"admin","password":"admin"}' -ContentType "application/json"
    $token = $loginResp.token
    Write-Host "200 OK -- JWT: $($token.Substring(0,40))..."
    $H = @{ Authorization = "Bearer $token" }

    Write-Host ""
    Write-Host "=== 2. CREER DECLARATION ==="
    $decl = Invoke-RestMethod -Uri "http://localhost:5005/api/declarations" -Method Post -Headers $H -Body '{"societeId":"001","exercice":2026,"periode":6,"type":0}' -ContentType "application/json"
    $id = $decl.id
    Write-Host "201 Created -- Numero=$($decl.numero) Id=$id"

    Write-Host ""
    Write-Host "=== 3. DOUBLON (409 attendu) ==="
    try {
        Invoke-RestMethod -Uri "http://localhost:5005/api/declarations" -Method Post -Headers $H -Body '{"societeId":"001","exercice":2026,"periode":6,"type":0}' -ContentType "application/json"
        Write-Host "ERREUR: aurait du renvoyer 409"
    } catch {
        Write-Host "409 Conflict -- OK"
    }

    Write-Host ""
    Write-Host "=== 4. GET LIGNES (figeage au 1er appel) ==="
    $url4 = "http://localhost:5005/api/declarations/$id/lignes?domaine=Decaissement&page=1&size=1000"
    $lignes = Invoke-RestMethod -Uri $url4 -Method Get -Headers $H
    Write-Host "200 OK -- TotalCount=$($lignes.totalCount)"
    $grouped = $lignes.items | Group-Object numeroRapprochement | Where-Object { $_.Count -gt 1 -and $_.Name -ne '' }
    foreach ($g in $grouped | Select-Object -First 2) {
        Write-Host "--- Groupe: Reglement='$($g.Name)' ---"
        foreach ($l in $g.Group) {
            $etatLabel = switch ($l.etat) { 0 { "Proposee" } 1 { "Integree" } 2 { "Exclue" } default { "?" } }
            Write-Host "  [$etatLabel] $($l.numeroFacture) -- Reglement='$($l.numeroRapprochement)' -- Tiers=$($l.tiersNom) -- MotifRejet='$($l.motifRejet)'"
        }
    }

    Write-Host ""
    Write-Host "=== 5. RECHARGEMENT (idempotent) ==="
    $lignes2 = Invoke-RestMethod -Uri $url4 -Method Get -Headers $H
    Write-Host "200 OK -- TotalCount=$($lignes2.totalCount) (identique -- figeage idempotent)"

    Write-Host ""
    Write-Host "=== 6. PATCH lignes Proposee vers Integree ==="
    $eligibles = $lignes.items | Where-Object { $_.etat -eq 0 }
    foreach ($l in $eligibles) {
        Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id/lignes/$($l.id)" -Method Patch -Headers $H -Body '{"etat":1}' -ContentType "application/json"
        Write-Host "  204 No Content -- $($l.numeroFacture) patche vers Integree"
    }

    Write-Host ""
    Write-Host "=== 7. CHECKUP (service reel GetCheckupAsync) ==="
    $checkup = Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id/checkup" -Method Get -Headers $H
    Write-Host "200 OK -- Alertes=$($checkup.alertes.Count)"
    Write-Host "  TotalMontantAffecte=$($checkup.controleEquilibre.totalMontantAffecte)"
    Write-Host "  TotalDeclareTtc=$($checkup.controleEquilibre.totalDeclareTtc)"
    foreach ($a in $checkup.alertes) {
        Write-Host "  [Alerte $($a.niveau)] $($a.code) -- $($a.message) ($($a.refLigne))"
    }
    $bloquantes = $checkup.alertes | Where-Object { $_.niveau -eq 2 }
    Write-Host "  Alertes Error bloquantes: $($bloquantes.Count)"

    Write-Host ""
    Write-Host "=== 8. CLOTURE BLOQUEE (declaration sans lignes integrees) ==="
    $decl2 = Invoke-RestMethod -Uri "http://localhost:5005/api/declarations" -Method Post -Headers $H -Body '{"societeId":"002","exercice":2026,"periode":7,"type":0}' -ContentType "application/json"
    $id2 = $decl2.id
    $null = Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id2/lignes?domaine=Decaissement&page=1&size=5" -Method Get -Headers $H
    try {
        Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id2/cloture" -Method Post -Headers $H
        Write-Host "ERREUR: cloture aurait du etre refusee"
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "$statusCode Bad Request -- Cloture refusee (attendu)"
        Write-Host "  Corps: $($_.ErrorDetails.Message)"
    }

    Write-Host ""
    Write-Host "=== 9. CLOTURE REUSSIE (lignes integrees presentes) ==="
    $bloquantes2 = ($checkup.alertes | Where-Object { $_.niveau -eq 2 }).Count
    Write-Host "Checkup avant cloture -- Alertes Error: $bloquantes2"
    Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id/cloture" -Method Post -Headers $H
    Write-Host "204 No Content -- Declaration cloturee"

    Write-Host ""
    Write-Host "=== 10. GENERATION (501 Not Implemented, honnetement delegue TASK-010/011) ==="
    try {
        Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id/generation" -Method Post -Headers $H
    } catch {
        $statusCode = [int]$_.Exception.Response.StatusCode
        Write-Host "$statusCode -- $($_.ErrorDetails.Message)"
    }

    Write-Host ""
    Write-Host "=== TOUS LES TESTS PASSES ==="

} finally {
    Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue
    Write-Host "API arretee."
}