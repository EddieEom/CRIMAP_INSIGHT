# Non-destructive endpoint smoke test. Run after backend and database are started.
$base = 'http://localhost:5088/api'
Invoke-RestMethod "$base/health" | ConvertTo-Json
Invoke-RestMethod "$base/meta" | ConvertTo-Json
Invoke-RestMethod "$base/pipeline/runs" | ConvertTo-Json
Write-Host "To trigger sync, set `$adminKey locally then run:" -ForegroundColor Yellow
Write-Host "Invoke-RestMethod -Method Post -Uri '$base/admin/sync/2025' -Headers @{ 'X-Admin-Key' = `$adminKey }"
