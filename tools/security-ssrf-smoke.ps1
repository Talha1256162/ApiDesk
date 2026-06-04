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
  name = "Smoke SSRF $(Get-Date -Format yyyyMMddHHmmss)"
  requests = @(
    @{
      name = "Blocked localhost"
      method = "GET"
      url = "http://127.0.0.1:80/"
      bodyType = "none"
      timeoutMs = 30000
      followRedirects = $true
      sslVerification = $true
      headers = @()
      queryParams = @()
      pathParams = @()
    }
  )
}

$imported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 20)
$requests = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($imported.data.collectionId)/requests" -Headers $headers
$requestId = $requests.data[0].id
$run = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$requestId/send" -Headers $headers -ContentType "application/json" -Body (@{ environmentId = $null; saveHistory = $true } | ConvertTo-Json)

if ($run.succeeded -and $run.data.succeeded) {
  throw "SSRF request unexpectedly succeeded."
}

Write-Host "PASS SSRF smoke: localhost/private target blocked."
