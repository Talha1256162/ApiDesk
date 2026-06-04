param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$Email = "admin@apiforge.local",
  [string]$Password = "Admin@12345",
  [string]$SamplePath = "docs/samples/postman-v2.1-smoke.postman_collection.json"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")
$sample = Get-Content -Raw -LiteralPath $SamplePath | ConvertFrom-Json -Depth 50

$login = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType "application/json" -Body (@{
  email = $Email
  password = $Password
} | ConvertTo-Json)

if (-not $login.succeeded) { throw "Login failed: $($login.message)" }
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }
$workspaceId = $login.data.workspaceId

function Convert-PostmanItem {
  param([array]$Items, [string[]]$FolderPath = @())
  $requests = @()
  foreach ($item in $Items) {
    if ($item.item) {
      $requests += Convert-PostmanItem -Items $item.item -FolderPath ($FolderPath + $item.name)
      continue
    }
    $request = $item.request
    $url = if ($request.url.raw) { $request.url.raw } else { "https://example.com" }
    $headers = @()
    foreach ($h in @($request.header)) {
      $headers += @{ key = $h.key; value = $h.value; enabled = -not $h.disabled; isSecret = ($h.key -match "token|secret|authorization|cookie") }
    }
    $requests += @{
      name = $item.name
      description = $request.description
      method = $request.method
      url = $url
      bodyType = if ($request.body.mode -eq "raw") { "rawJson" } else { "none" }
      bodyContent = $request.body.raw
      timeoutMs = 30000
      followRedirects = $true
      sslVerification = $true
      headers = $headers
      queryParams = @()
      pathParams = @()
      folderPath = $FolderPath
    }
  }
  return $requests
}

function Get-Folders {
  param([array]$Items, [string[]]$FolderPath = @())
  $folders = @()
  foreach ($item in $Items) {
    if ($item.item) {
      $next = $FolderPath + $item.name
      $folders += ,$next
      $folders += Get-Folders -Items $item.item -FolderPath $next
    }
  }
  return $folders
}

$payload = @{
  name = $sample.info.name
  description = $sample.info.description
  folders = @(Get-Folders -Items $sample.item)
  requests = @(Convert-PostmanItem -Items $sample.item)
}

$imported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($payload | ConvertTo-Json -Depth 50)
if (-not $imported.succeeded) { throw "Import failed: $($imported.message)" }

$exported = Invoke-RestMethod -Method Get -Uri "$api/api/collections/$($imported.data.collectionId)/export" -Headers $headers
if (-not $exported.succeeded) { throw "Export failed: $($exported.message)" }

$reimportPayload = @{
  name = "$($exported.data.collection.name) Reimport"
  description = $exported.data.collection.description
  folders = @($exported.data.requests | Where-Object { $_.folderPath } | ForEach-Object { ,$_.folderPath })
  requests = $exported.data.requests
}

$reimported = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/collections/import" -Headers $headers -ContentType "application/json" -Body ($reimportPayload | ConvertTo-Json -Depth 50)
if (-not $reimported.succeeded) { throw "Re-import failed: $($reimported.message)" }

$expectedRequests = @($payload.requests).Count
$exportedRequests = @($exported.data.requests).Count
$exportedFolders = @($exported.data.requests | Where-Object { $_.folderPath } | ForEach-Object { $_.folderPath -join "/" } | Select-Object -Unique).Count

if ($exportedRequests -ne $expectedRequests) { throw "Request count mismatch. Expected $expectedRequests, got $exportedRequests" }
if ($exportedFolders -lt 2) { throw "Folder path preservation failed. Expected at least 2 folders, got $exportedFolders" }

Write-Host "PASS Postman round-trip: $expectedRequests requests, $exportedFolders folder paths preserved."
