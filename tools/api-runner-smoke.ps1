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

$environment = Invoke-RestMethod -Method Post -Uri "$api/api/environments" -Headers $headers -ContentType "application/json" -Body (@{
  workspaceId = $workspaceId
  name = "Smoke Environment $(Get-Date -Format yyyyMMddHHmmss)"
  isDefault = $false
} | ConvertTo-Json)
if (-not $environment.succeeded) { throw "Environment create failed: $($environment.message)" }

$variables = Invoke-RestMethod -Method Put -Uri "$api/api/environments/$($environment.data.id)/variables" -Headers $headers -ContentType "application/json" -Body (@{
  variables = @(
    @{ key = "baseUrl"; value = "https://postman-echo.com"; scope = "Environment"; isSecret = $false; enabled = $true }
  )
} | ConvertTo-Json -Depth 10)
if (-not $variables.succeeded) { throw "Environment variables failed: $($variables.message)" }

$payload = @{
  name = "Smoke Runner $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Smoke request collection"
  folders = @(, @("Smoke"))
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
$run = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$requestId/send" -Headers $headers -ContentType "application/json" -Body (@{ environmentId = $environment.data.id; saveHistory = $true } | ConvertTo-Json)
if (-not $run.succeeded) { throw "Runner failed: $($run.message)" }

Write-Host "PASS API runner smoke: $($run.data.statusCode) $($run.data.elapsedMs)ms"
