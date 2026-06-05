param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$Email = "admin@apiforge.local",
  [string]$Password = "Admin@12345"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")

function Expect-Status($scriptBlock, [int[]]$allowedStatusCodes, [string]$label) {
  try {
    $result = & $scriptBlock
    if ($allowedStatusCodes -notcontains 200) {
      throw "$label was not blocked."
    }
    return $result
  } catch {
    if (-not $_.Exception.Response) { throw }
    $status = [int]$_.Exception.Response.StatusCode
    if ($allowedStatusCodes -notcontains $status) {
      throw "$label returned unexpected status $status."
    }
    return $null
  }
}

$login = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType "application/json" -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)
if (-not $login.succeeded) { throw "Login failed: $($login.message)" }
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
$workspaceId = $login.data.workspaceId
$organizationId = $login.data.organizationId

$payload = @{
  name = "Security Mock Docs $(Get-Date -Format yyyyMMddHHmmss)"
  description = "Security smoke collection"
  folders = @(, @("Users"))
  requests = @(@{
    folderPath = @("Users")
    name = "Get user"
    method = "GET"
    url = "https://example.com/users/123"
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
$collectionId = $imported.data.collectionId

$apiKey = Invoke-RestMethod -Method Post -Uri "$api/api/organizations/$organizationId/api-keys" -Headers $headers -ContentType "application/json" -Body (@{
  workspaceId = $workspaceId
  name = "Mock security key $(Get-Date -Format yyyyMMddHHmmss)"
} | ConvertTo-Json)
if (-not $apiKey.succeeded) { throw "API key create failed: $($apiKey.message)" }
$plainKey = $apiKey.data.plainTextKey

$publicMock = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/mock-servers" -Headers $headers -ContentType "application/json" -Body (@{
  collectionId = $collectionId
  name = "Public mock $(Get-Date -Format HHmmss)"
  isPublic = $true
  apiKeyRequired = $false
  delayMs = 0
} | ConvertTo-Json)
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($publicMock.data.slug)/users/123" } @(200,501) "Public mock"

$privateMock = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/mock-servers" -Headers $headers -ContentType "application/json" -Body (@{
  collectionId = $collectionId
  name = "Private mock $(Get-Date -Format HHmmss)"
  isPublic = $false
  apiKeyRequired = $false
  delayMs = 0
} | ConvertTo-Json)
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($privateMock.data.slug)/users/123" } @(401) "Private mock without key"
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($privateMock.data.slug)/users/123" -Headers @{ "X-API-Desk-Key" = "invalid" } } @(403) "Private mock invalid key"
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($privateMock.data.slug)/users/123" -Headers @{ "X-API-Desk-Key" = $plainKey } } @(200,501) "Private mock valid key"

$keyMock = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/mock-servers" -Headers $headers -ContentType "application/json" -Body (@{
  collectionId = $collectionId
  name = "Key mock $(Get-Date -Format HHmmss)"
  isPublic = $true
  apiKeyRequired = $true
  delayMs = 0
} | ConvertTo-Json)
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($keyMock.data.slug)/users/123" } @(401) "API-key mock without key"
Expect-Status { Invoke-WebRequest -UseBasicParsing -Uri "$api/api/mock/$($keyMock.data.slug)/users/123" -Headers @{ "X-API-Desk-Key" = $plainKey } } @(200,501) "API-key mock valid key"

$publicSlug = "public-docs-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$privateSlug = "private-docs-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$passwordSlug = "password-docs-$([guid]::NewGuid().ToString('N').Substring(0, 8))"

$publicDocs = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/published-docs" -Headers $headers -ContentType "application/json" -Body (@{ collectionId = $collectionId; slug = $publicSlug; isPublic = $true; brandJson = "{}" } | ConvertTo-Json)
if (-not $publicDocs.succeeded) { throw "Public docs publish failed: $($publicDocs.message)" }
Expect-Status { Invoke-RestMethod -Method Get -Uri "$api/api/docs/$publicSlug" } @(200) "Public docs"

$privateDocs = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/published-docs" -Headers $headers -ContentType "application/json" -Body (@{ collectionId = $collectionId; slug = $privateSlug; isPublic = $false; brandJson = "{}" } | ConvertTo-Json)
if (-not $privateDocs.succeeded) { throw "Private docs publish failed: $($privateDocs.message)" }
Expect-Status { Invoke-RestMethod -Method Get -Uri "$api/api/docs/$privateSlug" } @(403) "Private docs"

$passwordDocs = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/published-docs" -Headers $headers -ContentType "application/json" -Body (@{ collectionId = $collectionId; slug = $passwordSlug; isPublic = $true; password = "Docs@12345"; brandJson = "{}" } | ConvertTo-Json)
if (-not $passwordDocs.succeeded) { throw "Password docs publish failed: $($passwordDocs.message)" }
Expect-Status { Invoke-RestMethod -Method Get -Uri "$api/api/docs/$passwordSlug" } @(401) "Password docs GET"
Expect-Status { Invoke-RestMethod -Method Post -Uri "$api/api/docs/$passwordSlug/unlock" -ContentType "application/json" -Body (@{ password = "wrong" } | ConvertTo-Json) } @(403) "Password docs wrong password"
Expect-Status { Invoke-RestMethod -Method Post -Uri "$api/api/docs/$passwordSlug/unlock" -ContentType "application/json" -Body (@{ password = "Docs@12345" } | ConvertTo-Json) } @(200) "Password docs correct password"

Write-Host "PASS mock/docs security smoke: mock key enforcement and docs privacy/password verified."
