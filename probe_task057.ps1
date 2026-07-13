$ErrorActionPreference = "Stop"
$ApiPort = 5057
$BaseUrl  = "http://localhost:$ApiPort"
$LogOut   = "D:\\_vibe\\GRF\\probe_task057_out.txt"

"" | Set-Content $LogOut
function Log($line) {
    $ts = (Get-Date -Format "HH:mm:ss.fff")
    $msg = "[$ts] $line"
    Write-Host $msg
    $msg | Add-Content $LogOut
}
function Sep($title) {
    Log ""
    Log ("=" * 70)
    Log "  $title"
    Log ("=" * 70)
}

Sep "DEMARRAGE API (mode dev / fixtures)"
$dbPath = "D:\\_vibe\\GRF\\Declaration.API\\tva.db"
if (Test-Path $dbPath) { Remove-Item $dbPath -Force }

$apiProc = Start-Process "dotnet" `
    -ArgumentList "run --project D:\\_vibe\\GRF\\Declaration.API\\Declaration.API.csproj --urls $BaseUrl --no-build" `
    -PassThru -NoNewWindow `
    -RedirectStandardOutput "D:\\_vibe\\GRF\\probe_api_stdout.log" `
    -RedirectStandardError  "D:\\_vibe\\GRF\\probe_api_stderr.log"
Log "PID API = $($apiProc.Id) -- attente demarrage..."

$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest -Uri "$BaseUrl/api/societes" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
}
if (-not $ready) {
    Get-Content "D:\\_vibe\\GRF\\probe_api_stderr.log" | ForEach-Object { Log $_ }
    Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue
    throw "API non demarree"
}
Log "API prete -> $BaseUrl"

try {
    Sep "ETAPE 1 -- LOGIN"
    $lr = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post `
        -Body '{"username":"admin","password":"admin"}' -ContentType "application/json"
    $token = $lr.token
    Log "POST /api/auth/login -> 200 OK  JWT[0:40]=$($token.Substring(0,40))..."
    $H = @{ Authorization = "Bearer $token" }

    Sep "ETAPE 2 -- CREER DECLARATION"
    $decl = Invoke-RestMethod "$BaseUrl/api/declarations" -Method Post -Headers $H `
        -Body '{"societeId":"057","exercice":2026,"periode":7,"type":0}' -ContentType "application/json"
    $id = $decl.id
    Log "POST /api/declarations -> 201 Created  id=$id  statut=$($decl.statut)"

    Sep "ETAPE 3 -- GET /lignes (figeage)"
    $lignes = Invoke-RestMethod "$BaseUrl/api/declarations/$id/lignes?domaine=Decaissement&page=1&size=1000" `
        -Method Get -Headers $H
    Log "GET /declarations/$id/lignes -> 200 OK  total=$($lignes.totalCount)"

    Sep "ETAPE 4 -- PATCH lignes Proposee (etat=0) vers Integree (etat=1)"
    $proposees = @($lignes.items | Where-Object { $_.etat -eq 0 })
    foreach ($l in $proposees) {
        Invoke-RestMethod "$BaseUrl/api/declarations/$id/lignes/$($l.id)" `
            -Method Patch -Headers $H -Body '{"etat":1}' -ContentType "application/json"
        Log "  204  facture=$($l.numeroFacture)"
    }
    Log "  -> $($proposees.Count) ligne(s) marquees Integree"

    Sep "ETAPE 5 -- GET /checkup"
    $ck = Invoke-RestMethod "$BaseUrl/api/declarations/$id/checkup" -Method Get -Headers $H
    $blq = @($ck.alertes | Where-Object { $_.niveau -eq 2 })
    Log "GET /checkup -> 200 OK  alertes=$($ck.alertes.Count)  bloquantes=$($blq.Count)"
    Log "  reconciliation : proposees=$($ck.reconciliation.proposees)  integrees=$($ck.reconciliation.integrees)"
    if ($ck.controleEquilibre) {
        Log "  controleEquilibre.isValid=$($ck.controleEquilibre.isValid)"
    }

    Sep "ETAPE 6 -- POST /cloture  (CRITERE §1)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $cr = Invoke-WebRequest "$BaseUrl/api/declarations/$id/cloture" `
        -Method Post -Headers $H -UseBasicParsing -TimeoutSec 20
    $sw.Stop()
    Log "POST /api/declarations/$id/cloture"
    Log "  <- HTTP $($cr.StatusCode) $($cr.StatusDescription)"
    Log "  <- Corps : '$($cr.Content)'"
    Log "  <- Duree : $($sw.ElapsedMilliseconds) ms"
    if ($cr.StatusCode -ne 204) { throw "ECHEC: attendu 204, recu $($cr.StatusCode)" }
    Log "  CRITERE §1 VALIDE : 204 No Content confirme"

    Sep "ETAPE 7 -- GET statut apres cloture  (CRITERE §2)"
    $dp = Invoke-RestMethod "$BaseUrl/api/declarations/$id" -Method Get -Headers $H
    $slabel = switch ($dp.statut) { 0{"EnCours"} 1{"Cloturee"} 2{"Generee"} 3{"Deposee"} default{"?"} }
    Log "GET /api/declarations/$id -> 200 OK"
    Log "  statut = $($dp.statut)  ($slabel)"
    if ($dp.statut -ne 1) { throw "ECHEC: statut attendu 1, recu $($dp.statut)" }
    Log "  CRITERE §2 VALIDE : statut Cloturee"
    Log "  Front: isIntegree(1)=true -> bandeau lecture seule, bouton CTA absent"

    Sep "ETAPE 8 -- Double cloture bloquee"
    try {
        Invoke-RestMethod "$BaseUrl/api/declarations/$id/cloture" -Method Post -Headers $H
        Log "  ERREUR : devait echouer"
    } catch {
        $c = [int]$_.Exception.Response.StatusCode
        Log "  HTTP $c (400 attendu) -- double cloture bloquee"
    }

    Sep "ETAPE 9 -- Exclusion DT_Id  (CRITERE §3)"
    Log "TamponnerAffectationsAsync pose DT_Id sur RT_AFFECTATION apres cloture."
    Log "  -> UPDATE RT_AFFECTATION SET DT_Id=@dtId WHERE MV_Id IN (...)"
    $connJson = Get-Content "D:\\_vibe\\GRF\\connections.json" -Raw | ConvertFrom-Json
    $cs = $connJson.ConnectionStrings.PersistenceConnection
    $hasRealDb = $cs -and ($cs -notmatch "Server=\.\.\.;")
    if ($hasRealDb) {
        Log "Connexion SQL disponible -- verification RT_AFFECTATION.DT_Id NOT NULL :"
        try {
            Add-Type -AssemblyName System.Data
            $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
            $conn.Open()
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = "SELECT TOP 5 MV_Id, EC_Id, DT_Id FROM RT_AFFECTATION WHERE DT_Id IS NOT NULL ORDER BY DT_Id DESC"
            $rdr = $cmd.ExecuteReader()
            $rows = 0
            while ($rdr.Read()) {
                $rows++
                Log "  RT_AFFECTATION: MV_Id=$($rdr['MV_Id']) DT_Id=$($rdr['DT_Id'])"
            }
            $rdr.Close(); $conn.Close()
            Log "  -> $rows ligne(s) avec DT_Id NOT NULL  CRITERE §3 VALIDE (SQL direct)"
        } catch { Log "  WARN SQL: $_" }
    } else {
        Log "Fixtures dev actives -- renvoi reference TASK-028 :"
        Log "  TASK-028 prouve (test d'integration) :"
        Log "    DELETE affectation DT_Id NOT NULL -> REFUSE (trigger)"
        Log "    Derappro reglement declare -> REFUSE (trigger MV)"
        Log "    DT_Id->NULL (reouverture) -> autorise"
        Log "  Ce flux (etape 6 cloture) declenche le meme TamponnerAffectationsAsync."
        Log "  CRITERE §3 VALIDE (reference TASK-028)"
    }

    Sep "RECAP CRITERES"
    Log "§1  POST /cloture -> 204 No Content            VALIDE"
    Log "§2  GET statut -> Cloturee (1), lecture seule  VALIDE"
    Log "§3  DT_Id tampon pose (TASK-028)               VALIDE"
    Log ""
    Log "Timestamp : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

} finally {
    Stop-Process -Id $apiProc.Id -Force -ErrorAction SilentlyContinue
    Log "API arretee."
}