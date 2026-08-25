using System.Text.Json.Serialization;

namespace EmailSearch;

public class Attachment
{
    [JsonPropertyName("@odata.type")]
    public string? ODataType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("contentBytes")]
    public string? ContentBytes { get; set; }

    public bool IsFileAttachment => ODataType == "#microsoft.graph.fileAttachment";
}

public class AttachmentsResponse
{
    [JsonPropertyName("value")]
    public List<Attachment> Value { get; set; } = new();
}
