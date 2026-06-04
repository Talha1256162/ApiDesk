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

$roles = Invoke-RestMethod -Method Get -Uri "$api/api/organizations/$organizationId/roles" -Headers $headers
if (-not $roles.succeeded) { throw "Role load failed: $($roles.message)" }
$viewer = $roles.data | Where-Object { $_.name -eq "Viewer" } | Select-Object -First 1
if (-not $viewer) { throw "Viewer role not found." }

$inviteEmail = "smoke+$([guid]::NewGuid().ToString('N').Substring(0, 8))@example.com"
$invite = Invoke-RestMethod -Method Post -Uri "$api/api/organizations/$organizationId/invites" -Headers $headers -ContentType "application/json" -Body (@{
  email = $inviteEmail
  roleId = $viewer.id
  message = "Smoke invite"
} | ConvertTo-Json)
if (-not $invite.succeeded -or -not $invite.data.inviteToken) { throw "Invite did not return a token/link payload." }

$regenerated = Invoke-RestMethod -Method Post -Uri "$api/api/organizations/$organizationId/invites/$($invite.data.id)/regenerate" -Headers $headers -ContentType "application/json" -Body "{}"
if (-not $regenerated.succeeded -or -not $regenerated.data.inviteToken) { throw "Invite regeneration failed." }

$revoked = Invoke-RestMethod -Method Patch -Uri "$api/api/organizations/$organizationId/invites/$($invite.data.id)/revoke" -Headers $headers -ContentType "application/json" -Body "{}"
if (-not $revoked.succeeded) { throw "Invite revoke failed: $($revoked.message)" }

Write-Host "PASS team collaboration smoke: invite/regenerate/revoke completed for $inviteEmail"
