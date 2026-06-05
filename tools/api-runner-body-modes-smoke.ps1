param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$Email = "admin@apiforge.local",
  [string]$Password = "Admin@12345"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")

function New-RequestPayload {
  param(
    [string]$Name,
    [string]$Method,
    [string]$Url,
    [string]$BodyType = "none",
    [string]$BodyContent = "",
    [array]$Headers = @()
  )

  return @{
    folderPath = @("Body Modes")
    name = $Name
    method = $Method
    url = $Url
    bodyType = $BodyType
    bodyContent = $BodyContent
    timeoutMs = 30000
    followRedirects = $true
    sslVerification = $true
    headers = $Headers
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
  name = "Body Mode Smoke $(Get-Date -Format yyyyMMddHHmmss)"
  isDefault = $false
} | ConvertTo-Json)
if (-not $environment.succeeded) { throw "Environment create failed: $($environment.message)" }

$variables = Invoke-RestMethod -Method Put -Uri "$api/api/environments/$($environment.data.id)/variables" -Headers $headers -ContentType "application/json" -Body (@{
  variables = @(
    @{ key = "baseUrl"; value = "https://postman-echo.com"; scope = "Environment"; isSecret = $false; enabled = $true },
    @{ key = "token"; value = "body-smoke-secret"; scope = "Environment"; isSecret = $true; enabled = $true }
  )
} | ConvertTo-Json -Depth 10)
if (-not $variables.succeeded) { throw "Environment variables failed: $($variables.message)" }

$jsonHeader = @(@{ key = "Content-Type"; value = "application/json"; enabled = $true; isSecret = $false })
$textHeader = @(@{ key = "Content-Type"; value = "text/plain"; enabled = $true; isSecret = $false })
$formHeader = @(@{ key = "Content-Type"; value = "application/x-www-form-urlencoded"; enabled = $true; isSecret = $false })

$payload = @{
  name = "Body Mode Smoke Collection $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Verifies API Desk request runner body modes."
  folders = @(, @("Body Modes"))
  requests = @(
    (New-RequestPayload -Name "GET echo" -Method "GET" -Url "{{baseUrl}}/get?source=apidesk"),
    (New-RequestPayload -Name "POST raw JSON" -Method "POST" -Url "{{baseUrl}}/post" -BodyType "rawJson" -BodyContent '{"source":"api-desk","token":"{{token}}"}' -Headers $jsonHeader),
    (New-RequestPayload -Name "POST raw text" -Method "POST" -Url "{{baseUrl}}/post" -BodyType "rawText" -BodyContent "hello api desk" -Headers $textHeader),
    (New-RequestPayload -Name "POST form urlencoded" -Method "POST" -Url "{{baseUrl}}/post" -BodyType "formUrlEncoded" -BodyContent "name=api-desk&mode=smoke" -Headers $formHeader),
    (New-RequestPayload -Name "POST form-data placeholder" -Method "POST" -Url "{{baseUrl}}/post" -BodyType "formData" -BodyContent "name=api-desk`nmode=smoke")
  )
}

$imported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 20)
if (-not $imported.succeeded) { throw "Import failed: $($imported.message)" }

$requests = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($imported.data.collectionId)/requests" -Headers $headers
if ($requests.data.Count -ne 5) { throw "Expected 5 imported requests, got $($requests.data.Count)." }

$results = @()
foreach ($request in $requests.data) {
  $run = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$($request.id)/send" -Headers $headers -ContentType "application/json" -Body (@{
    environmentId = $environment.data.id
    saveHistory = $true
  } | ConvertTo-Json)

  if (-not $run.succeeded) {
    throw "Runner failed for '$($request.name)': $($run.message)"
  }

  if ([int]$run.data.statusCode -lt 200 -or [int]$run.data.statusCode -ge 300) {
    throw "Runner returned non-2xx for '$($request.name)': $($run.data.statusCode)"
  }

  $results += "$($request.name)=$($run.data.statusCode)"
}

$missingPayload = @{
  name = "Missing Variable Smoke $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Verifies missing variable guard."
  folders = @(, @("Missing Variable"))
  requests = @(
    (New-RequestPayload -Name "Missing variable request" -Method "GET" -Url "{{missingBaseUrl}}/get")
  )
}
$missingImport = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($missingPayload | ConvertTo-Json -Depth 20)
$missingRequests = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($missingImport.data.collectionId)/requests" -Headers $headers
$missingBlocked = $false
try {
  $missingRun = Invoke-RestMethod -Method Post -Uri "$api/api/requests/$($missingRequests.data[0].id)/send" -Headers $headers -ContentType "application/json" -Body (@{
    environmentId = $environment.data.id
    saveHistory = $false
  } | ConvertTo-Json)

  if ($missingRun.succeeded) {
    throw "Missing variable guard failed; request succeeded unexpectedly."
  }

  $missingBlocked = $true
} catch {
  if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 400) {
    $missingBlocked = $true
  } else {
    throw
  }
}

if (-not $missingBlocked) {
  throw "Missing variable guard failed; no blocking response was observed."
}

Write-Host "PASS API runner body modes smoke: $($results -join ', '); missing variable guard blocked."
