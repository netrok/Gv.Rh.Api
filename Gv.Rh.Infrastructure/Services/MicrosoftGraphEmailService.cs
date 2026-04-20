using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gv.Rh.Infrastructure.Services;

public sealed class MicrosoftGraphEmailService : IEmailService
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    private readonly MicrosoftGraphMailOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfidentialClientApplication _confidentialClient;

    public MicrosoftGraphEmailService(
        IOptions<MicrosoftGraphMailOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;

        _confidentialClient = ConfidentialClientApplicationBuilder
            .Create(_options.ClientId)
            .WithTenantId(_options.TenantId)
            .WithClientSecret(_options.ClientSecret)
            .Build();
    }

    public Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        return SendAsync([to], subject, htmlBody, cancellationToken);
    }

    public async Task SendAsync(
        IEnumerable<string> to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var recipients = to
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Debe existir al menos un destinatario válido.");
        }

        if (string.IsNullOrWhiteSpace(_options.SenderUserId))
        {
            throw new InvalidOperationException("MicrosoftGraphMail:SenderUserId no está configurado.");
        }

        var tokenResult = await _confidentialClient
            .AcquireTokenForClient(GraphScopes)
            .ExecuteAsync(cancellationToken);

        var payload = new
        {
            message = new
            {
                subject,
                body = new
                {
                    contentType = "HTML",
                    content = htmlBody
                },
                toRecipients = recipients.Select(email => new
                {
                    emailAddress = new
                    {
                        address = email
                    }
                })
            },
            saveToSentItems = true
        };

        var json = JsonSerializer.Serialize(payload);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(_options.SenderUserId)}/sendMail");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Error al enviar correo con Microsoft Graph. Status: {(int)response.StatusCode}. Body: {responseBody}");
        }
    }
}