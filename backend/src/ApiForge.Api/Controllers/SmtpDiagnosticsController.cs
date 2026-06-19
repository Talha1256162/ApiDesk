using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Domain.Constants;
using ApiForge.Infrastructure.Email;
using ApiForge.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/admin/smtp-diagnostics")]
public sealed class SmtpDiagnosticsController(
    IOptionsSnapshot<EmailOptions> emailOptions,
    IConfiguration configuration,
    ICurrentUserContext currentUserContext,
    IPermissionService permissionService) : ApiControllerBase
{
    private static readonly string[] ExpectedKeys =
    [
        "Email:PublicBaseUrl",
        "Email:Smtp:Enabled",
        "Email:Smtp:Host",
        "Email:Smtp:Port",
        "Email:Smtp:Username",
        "Email:Smtp:Password",
        "Email:Smtp:FromName",
        "Email:Smtp:FromEmail",
        "Email:Smtp:EncryptionId",
        "Email:Smtp:IsVerified"
    ];

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var currentUser = currentUserContext.User;
        if (currentUser is null)
        {
            return FromResult(Result<SmtpDiagnosticsDto>.Failure(
                "Authentication is required.",
                new ErrorDetail("auth.required", "Authentication is required.")));
        }

        if (currentUser.OrganizationId is not Guid organizationId)
        {
            return FromResult(Result<SmtpDiagnosticsDto>.Failure(
                "Organization context is required.",
                new ErrorDetail("organization.required", "Organization context is required.")));
        }

        var allowed = await permissionService.HasPermissionAsync(
            currentUser.UserId,
            organizationId,
            null,
            PermissionKeys.InviteMembers,
            cancellationToken);

        if (!allowed)
        {
            return FromResult(Result<SmtpDiagnosticsDto>.Failure(
                "You do not have permission to inspect SMTP diagnostics.",
                new ErrorDetail("permission.denied", $"Missing permission: {PermissionKeys.InviteMembers}.")));
        }

        var email = emailOptions.Value;
        var smtp = email.Smtp;
        var configRoot = configuration as IConfigurationRoot;

        var diagnostics = new SmtpDiagnosticsDto(
            EmailSectionFound: configuration.GetSection("Email").Exists(),
            SmtpSectionFound: configuration.GetSection("Email:Smtp").Exists(),
            Enabled: smtp.Enabled,
            HostPresent: !string.IsNullOrWhiteSpace(smtp.Host),
            Port: smtp.Port,
            UsernamePresent: !string.IsNullOrWhiteSpace(smtp.Username),
            PasswordConfigured: !string.IsNullOrWhiteSpace(smtp.Password),
            FromEmailPresent: !string.IsNullOrWhiteSpace(smtp.FromEmail),
            EncryptionId: smtp.EncryptionId,
            IsVerified: smtp.IsVerified,
            PublicBaseUrl: string.IsNullOrWhiteSpace(email.PublicBaseUrl) ? null : email.PublicBaseUrl,
            IsConfigured: smtp.IsConfigured,
            ExpectedEnvironmentVariables: ExpectedKeys.Select(ToEnvironmentVariableName).ToArray(),
            KeyPresence: ExpectedKeys.ToDictionary(
                key => key,
                key => new ConfigKeyDiagnostics(
                    Present: !string.IsNullOrWhiteSpace(configuration[key]),
                    Sources: GetSources(configRoot, key))),
            Notes:
            [
                "Secret values are intentionally not returned.",
                "For nested ASP.NET Core configuration on MonsterASP, use double underscore names such as Email__Smtp__Password."
            ]);

        return FromResult(Result<SmtpDiagnosticsDto>.Success(diagnostics));
    }

    private static string ToEnvironmentVariableName(string key) => key.Replace(':', '_').Replace("_", "__");

    private static string[] GetSources(IConfigurationRoot? configRoot, string key)
    {
        if (configRoot is null)
        {
            return [];
        }

        return configRoot.Providers
            .Where(provider => provider.TryGet(key, out var value) && !string.IsNullOrWhiteSpace(value))
            .Select(provider => provider.GetType().Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed record SmtpDiagnosticsDto(
    bool EmailSectionFound,
    bool SmtpSectionFound,
    bool Enabled,
    bool HostPresent,
    int Port,
    bool UsernamePresent,
    bool PasswordConfigured,
    bool FromEmailPresent,
    int EncryptionId,
    bool IsVerified,
    string? PublicBaseUrl,
    bool IsConfigured,
    IReadOnlyList<string> ExpectedEnvironmentVariables,
    IReadOnlyDictionary<string, ConfigKeyDiagnostics> KeyPresence,
    IReadOnlyList<string> Notes);

public sealed record ConfigKeyDiagnostics(bool Present, IReadOnlyList<string> Sources);
