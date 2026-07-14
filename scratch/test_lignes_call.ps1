$H = @{ Authorization = "Bearer " + (Invoke-RestMethod -Uri "http://localhost:5018/api/auth/login" -Method Post -Body '{"username":"admin","password":"admin"}' -ContentType "application/json").token }
$decls = Invoke-RestMethod -Uri "http://localhost:5018/api/declarations?societeId=1" -Method Get -Headers $H
$decl = $decls | Where-Object { $_.periode -eq 6 }
$id = $decl.id
Write-Host "Declaration ID: $id"

# 1. Get all lines without filter
$resAll = Invoke-RestMethod -Uri "http://localhost:5018/api/declarations/$id/lignes?domaine=Decaissement&page=1&size=200" -Method Get -Headers $H
Write-Host "TotalCount without filter: $($resAll.totalCount)"
if ($resAll.items.Count -gt 0) {
    $firstLigne = $resAll.items[0]
    Write-Host "First Ligne - numeroRapprochement: '$($firstLigne.numeroRapprochement)'"
    
    # 2. Get lines with filter
    $filterObj = @{ numeroRapprochement = $firstLigne.numeroRapprochement }
    $filterJson = ConvertTo-Json $filterObj -Compress
    $urlFiltered = "http://localhost:5018/api/declarations/$id/lignes?domaine=Decaissement&page=1&size=200&filter=$([Uri]::EscapeDataString($filterJson))"
    Write-Host "Querying: $urlFiltered"
    $resFiltered = Invoke-RestMethod -Uri $urlFiltered -Method Get -Headers $H
    Write-Host "TotalCount with filter: $($resFiltered.totalCount)"
}
