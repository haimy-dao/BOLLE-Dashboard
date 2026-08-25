using System.Security.Cryptography.X509Certificates;
using Microsoft.Identity.Client;

namespace EmailSearch;

public static class GraphClient
{
    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };

    public static async Task<string> GetAccessTokenAsync()
    {
        var certificate = FindCertificateByThumbprint(Config.AzureCertThumbprint);

        var app = ConfidentialClientApplicationBuilder
            .Create(Config.AzureClientId)
            .WithCertificate(certificate)
            .WithAuthority($"https://login.microsoftonline.com/{Config.AzureTenantId}")
            .Build();

        try
        {
            var result = await app.AcquireTokenForClient(Scopes).ExecuteAsync();
            return result.AccessToken;
        }
        catch (MsalServiceException ex)
        {
            throw new InvalidOperationException($"Token-Abruf fehlgeschlagen: {ex.Message}", ex);
        }
    }

    private static readonly StoreName[] StoresToSearch = { StoreName.My, StoreName.Root };

    private static X509Certificate2 FindCertificateByThumbprint(string thumbprint)
    {
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            foreach (var storeName in StoresToSearch)
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadOnly);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                if (matches.Count > 0)
                {
                    return matches[0];
                }
            }
        }

        throw new InvalidOperationException(
            $"Zertifikat mit Thumbprint '{thumbprint}' wurde in keinem der durchsuchten Speicher " +
            "(My, Root; CurrentUser, LocalMachine) gefunden.");
    }
}
