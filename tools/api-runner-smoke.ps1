param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$Email = "admin@apiforge.local",
  [string]$Password = "Admin@12345"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")

$login = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType "application/json" -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)
if (-not $login.succeeded) { throw "Login failed: $($login.message)" }
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
$workspaceId = $login.data.workspaceId

$payload = @{
  name = "Smoke Runner $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Smoke request collection"
  folders = @(@("Smoke"))
  requests = @(@{
    folderPath = @("Smoke")
    name = "Postman Echo GET"
    method = "GET"
    url = "{{baseUrl}}/get?source=apidesk"
    bodyType = "none"
    bodyContent = ""
    timeoutMs = 30000
    followRedirects = $true
    sslVerification = $true
    headers = @()
    queryParams = @()
    pathParams = @()
  })
}

$imported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 20)
if (-not $imported.succeeded) { throw "Import failed: $($imported.message)" }

$requests = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($imported.data.collectionId)/requests" -Headers $headers
$requestId = $requests.data[0].id
$run = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$requestId/send" -Headers $headers -ContentType "application/json" -Body (@{ environmentId = $null; saveHistory = $true } | ConvertTo-Json)
if (-not $run.succeeded) { throw "Runner failed: $($run.message)" }

Write-Host "PASS API runner smoke: $($run.data.statusCode) $($run.data.elapsedMs)ms"
