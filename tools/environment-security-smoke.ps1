param(
  [string]$BaseUrl = "http://localhost:5248",
  [string]$AdminEmail = "admin@apiforge.local",
  [string]$AdminPassword = "Admin@12345"
)

$ErrorActionPreference = "Stop"
$api = $BaseUrl.TrimEnd("/")

function Login($email, $password) {
  $result = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType "application/json" -Body (@{ email = $email; password = $password } | ConvertTo-Json)
  if (-not $result.succeeded) { throw "Login failed for $email`: $($result.message)" }
  return $result.data
}

function Expect-HttpBlock($scriptBlock, [int[]]$allowedStatusCodes, [string]$label) {
  try {
    & $scriptBlock | Out-Null
    throw "$label was not blocked."
  } catch {
    if (-not $_.Exception.Response) { throw }
    $status = [int]$_.Exception.Response.StatusCode
    if ($allowedStatusCodes -notcontains $status) {
      throw "$label returned unexpected status $status."
    }
  }
}

$admin = Login $AdminEmail $AdminPassword
$adminHeaders = @{ Authorization = "Bearer $($admin.accessToken)" }
$workspaceId = $admin.workspaceId

$environment = Invoke-RestMethod -Method Post -Uri "$api/api/environments" -Headers $adminHeaders -ContentType "application/json" -Body (@{
  workspaceId = $workspaceId
  name = "Security Environment $(Get-Date -Format yyyyMMddHHmmss)"
  isDefault = $false
} | ConvertTo-Json)
if (-not $environment.succeeded) { throw "Admin environment create failed: $($environment.message)" }

$otherEmail = "env-security+$([guid]::NewGuid().ToString('N').Substring(0, 8))@example.com"
$registered = Invoke-RestMethod -Method Post -Uri "$api/api/auth/register" -ContentType "application/json" -Body (@{
  email = $otherEmail
  password = "Admin@12345"
  fullName = "Environment Security User"
  organizationName = "Environment Security Org"
  workspaceName = "External Workspace"
} | ConvertTo-Json)
if (-not $registered.succeeded) { throw "Registration failed: $($registered.message)" }
$otherHeaders = @{ Authorization = "Bearer $($registered.data.accessToken)" }

Expect-HttpBlock {
  Invoke-RestMethod -Method Get -Uri "$api/api/workspaces/$workspaceId/environments?count=10" -Headers $otherHeaders
} @(403) "Cross-workspace environment listing"

Expect-HttpBlock {
  Invoke-RestMethod -Method Put -Uri "$api/api/environments/$($environment.data.id)/variables" -Headers $otherHeaders -ContentType "application/json" -Body (@{
    variables = @(@{ key = "baseUrl"; value = "https://example.com"; scope = "Environment"; isSecret = $false; enabled = $true })
  } | ConvertTo-Json -Depth 10)
} @(403) "Cross-workspace environment variable update"

Write-Host "PASS environment security smoke: cross-workspace list/update blocked."
