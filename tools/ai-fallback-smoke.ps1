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
$organizationId = $login.data.organizationId
$workspaceId = $login.data.workspaceId

$status = Invoke-RestMethod -Method Get -Uri "$api/api/organizations/$organizationId/ai-provider/status" -Headers $headers
if (-not $status.succeeded) { throw "AI status failed: $($status.message)" }

$action = Invoke-RestMethod -Method Post -Uri "$api/api/workspaces/$workspaceId/ai-assistant/actions" -Headers $headers -ContentType "application/json" -Body (@{
  action = "GenerateTests"
  input = "School parent pays invoice then receives voucher"
} | ConvertTo-Json)

if (-not $action.succeeded -or @($action.data.suggestions).Count -eq 0) { throw "AI action did not return fallback suggestions." }
Write-Host "PASS AI smoke: provider=$($status.data.providerName), configured=$($status.data.configured), suggestions=$(@($action.data.suggestions).Count)"
