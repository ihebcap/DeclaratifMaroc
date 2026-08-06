$H = @{ Authorization = "Bearer " + (Invoke-RestMethod -Uri "http://localhost:5018/api/auth/login" -Method Post -Body '{"username":"admin","password":"admin"}' -ContentType "application/json").token }
$decls = Invoke-RestMethod -Uri "http://localhost:5018/api/declarations?societeId=1" -Method Get -Headers $H
$decl = $decls | Where-Object { $_.periode -eq 6 }
$id = $decl.id
Write-Host "Declaration ID: $id"

$filterObj = @{ numeroRapprochement = "RF26060125" }
$filterJson = ConvertTo-Json $filterObj -Compress
$urlFiltered = "http://localhost:5018/api/declarations/$id/lignes?domaine=Decaissement&page=1&size=200&filter=$([Uri]::EscapeDataString($filterJson))"
$resFiltered = Invoke-RestMethod -Uri $urlFiltered -Method Get -Headers $H
Write-Host "TotalCount for RF26060125: $($resFiltered.totalCount)"
