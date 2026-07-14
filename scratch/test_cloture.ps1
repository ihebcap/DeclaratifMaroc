$H = @{ Authorization = "Bearer " + (Invoke-RestMethod -Uri "http://localhost:5005/api/auth/login" -Method Post -Body '{"username":"admin","password":"admin"}' -ContentType "application/json").token }
$decls = Invoke-RestMethod -Uri "http://localhost:5005/api/declarations?societeId=1" -Method Get -Headers $H
$decl = $decls | Where-Object { $_.periode -eq 6 }
$id = $decl.id
Write-Host "Declaration ID: $id"
try {
    Invoke-RestMethod -Uri "http://localhost:5005/api/declarations/$id/cloture" -Method Post -Headers $H
    Write-Host "Success!"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        Write-Host "Body: $body"
    }
}
