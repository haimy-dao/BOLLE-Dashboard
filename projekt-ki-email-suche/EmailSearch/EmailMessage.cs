using System.Text.Json.Serialization;

namespace EmailSearch;

public class EmailMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    [JsonPropertyName("receivedDateTime")]
    public string? ReceivedDateTime { get; set; }

    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; set; }

    [JsonPropertyName("from")]
    public FromField? From { get; set; }
}

public class FromField
{
    [JsonPropertyName("emailAddress")]
    public EmailAddress? EmailAddress { get; set; }
}

public class EmailAddress
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public class MessageSearchResponse
{
    [JsonPropertyName("value")]
    public List<EmailMessage> Value { get; set; } = new();
}
