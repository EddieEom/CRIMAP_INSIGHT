# PowerShell: run from repository root. Values are local process variables only.
# Run docker compose up -d first, then wait for SQL Server to accept connections.
if (-not $env:OpenData__ServiceKey) { throw 'Set OpenData__ServiceKey in your terminal first.' }
if (-not $env:Admin__SyncKey) { throw 'Set Admin__SyncKey in your terminal first.' }
if (-not $env:ConnectionStrings__CrimeInsight) { throw 'Set ConnectionStrings__CrimeInsight in your terminal first.' }
dotnet run --project ./backend/CrimeInsight.Api --launch-profile http
