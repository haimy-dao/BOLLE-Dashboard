namespace EmailSearch;

public static class Config
{
    public static string AzureTenantId { get; }
    public static string AzureClientId { get; }
    public static string AzureCertThumbprint { get; }
    public static string TargetMailbox { get; }
    public static string SearchTerm { get; }
    public static string SemanticQuery { get; }

    static Config()
    {
        DotNetEnv.Env.Load();

        AzureTenantId = Require("AZURE_TENANT_ID");
        AzureClientId = Require("AZURE_CLIENT_ID");
        AzureCertThumbprint = Require("AZURE_CERT_THUMBPRINT");
        TargetMailbox = Require("TARGET_MAILBOX");
        SearchTerm = Environment.GetEnvironmentVariable("SEARCH_TERM") ?? "12774";
        SemanticQuery = Environment.GetEnvironmentVariable("SEMANTIC_QUERY") ?? $"Projekt {SearchTerm}";
    }

    private static string Require(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Umgebungsvariable '{name}' fehlt. Bitte .env anlegen (siehe .env.example).");
        }
        return value;
    }
}
