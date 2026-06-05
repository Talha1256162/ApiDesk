param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$Email = "admin@apiforge.local",
  [string]$Password = "Admin@12345"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")

function New-AuthRequest {
  param([string]$Name, [string]$Url, [string]$AuthType, [string]$AuthConfigJson)
  return @{
    folderPath = @("Auth Types")
    name = $Name
    method = "GET"
    url = $Url
    authType = $AuthType
    authConfigJson = $AuthConfigJson
    bodyType = "none"
    bodyContent = ""
    timeoutMs = 30000
    followRedirects = $true
    sslVerification = $true
    headers = @()
    queryParams = @()
    pathParams = @()
  }
}

$login = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType "application/json" -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)
if (-not $login.succeeded) { throw "Login failed: $($login.message)" }
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
$workspaceId = $login.data.workspaceId

$environment = Invoke-RestMethod -Method Post -Uri "$api/api/environments" -Headers $headers -ContentType "application/json" -Body (@{
  workspaceId = $workspaceId
  name = "Auth Smoke Environment $(Get-Date -Format yyyyMMddHHmmss)"
  isDefault = $false
} | ConvertTo-Json)

$variables = Invoke-RestMethod -Method Put -Uri "$api/api/environments/$($environment.data.id)/variables" -Headers $headers -ContentType "application/json" -Body (@{
  variables = @(
    @{ key = "baseUrl"; value = "https://postman-echo.com"; scope = "Environment"; isSecret = $false; enabled = $true },
    @{ key = "token"; value = "auth-smoke-token"; scope = "Environment"; isSecret = $true; enabled = $true },
    @{ key = "apiKey"; value = "auth-smoke-key"; scope = "Environment"; isSecret = $true; enabled = $true }
  )
} | ConvertTo-Json -Depth 10)
if (-not $variables.succeeded) { throw "Environment variables failed: $($variables.message)" }

$payload = @{
  name = "Auth Type Smoke $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Verifies structured auth execution."
  folders = @(, @("Auth Types"))
  requests = @(
    (New-AuthRequest -Name "Bearer auth" -Url "{{baseUrl}}/headers" -AuthType "Bearer" -AuthConfigJson '{"token":"{{token}}"}'),
    (New-AuthRequest -Name "Basic auth" -Url "{{baseUrl}}/headers" -AuthType "Basic" -AuthConfigJson '{"username":"api-user","password":"api-pass"}'),
    (New-AuthRequest -Name "ApiKey header auth" -Url "{{baseUrl}}/headers" -AuthType "ApiKey" -AuthConfigJson '{"name":"X-API-Key","value":"{{apiKey}}","location":"header"}'),
    (New-AuthRequest -Name "ApiKey query auth" -Url "{{baseUrl}}/get" -AuthType "ApiKey" -AuthConfigJson '{"name":"deskKey","value":"{{apiKey}}","location":"query"}'),
    (New-AuthRequest -Name "OAuth2 manual bearer" -Url "{{baseUrl}}/headers" -AuthType "OAuth2" -AuthConfigJson '{"token":"{{token}}","grantType":"manual_bearer"}')
  )
}

$imported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 20)
if (-not $imported.succeeded) { throw "Import failed: $($imported.message)" }

$requests = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($imported.data.collectionId)/requests" -Headers $headers
foreach ($request in $requests.data) {
  $run = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$($request.id)/send" -Headers $headers -ContentType "application/json" -Body (@{
    environmentId = $environment.data.id
    saveHistory = $true
  } | ConvertTo-Json)
  if (-not $run.succeeded -or [int]$run.data.statusCode -lt 200 -or [int]$run.data.statusCode -ge 300) {
    throw "Auth request '$($request.name)' failed: $($run.message) $($run.data.statusCode)"
  }
  if ($request.name -eq "ApiKey query auth" -and $run.data.body -notmatch "deskKey") {
    throw "ApiKey query auth did not append query parameter."
  }
}

Write-Host "PASS API runner auth types smoke: Bearer, Basic, ApiKey header/query, OAuth2 manual bearer executed."
