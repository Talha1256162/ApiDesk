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
  name = "Smoke Mock $(Get-Date -Format yyyyMMddHHmmss)"
  requests = @(@{
    name = "Mock user"
    method = "GET"
    url = "https://example.com/users/123"
    bodyType = "none"
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

$mock = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/mock-servers" -Headers $headers -ContentType "application/json" -Body (@{
  collectionId = $imported.data.collectionId
  name = "Smoke Mock Server $(Get-Date -Format HHmmss)"
  isPublic = $true
  apiKeyRequired = $false
  delayMs = 0
} | ConvertTo-Json)
if (-not $mock.succeeded -or $mock.data.routeCount -lt 1) { throw "Mock server creation failed." }

$routes = Invoke-RestMethod -Method Get -Uri "$api/api/mock-servers/$($mock.data.id)/routes" -Headers $headers
if (-not $routes.succeeded -or @($routes.data).Count -lt 1) { throw "Mock route list failed." }

$path = $routes.data[0].path.TrimStart("/")
$statusCode = $null
try {
  $response = Invoke-WebRequest -Method Get -Uri "$api/api/mock/$($mock.data.slug)/$path" -UseBasicParsing
  $statusCode = [int]$response.StatusCode
} catch {
  if (-not $_.Exception.Response) { throw }
  $statusCode = [int]$_.Exception.Response.StatusCode
}

if (@(200, 501) -notcontains $statusCode) { throw "Mock endpoint returned unexpected HTTP $statusCode." }

Write-Host "PASS mock server smoke: slug=$($mock.data.slug), route=/$path"
