using System.Net.Http.Headers;
using System.Text.Json;

namespace EmailSearch;

public static class AttachmentClient
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    public static async Task<List<Attachment>> GetAttachmentsAsync(
        HttpClient client, string accessToken, string mailbox, string messageId)
    {
        var url = $"{GraphBase}/users/{Uri.EscapeDataString(mailbox)}/messages/{messageId}/attachments";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Anhänge konnten nicht geladen werden ({(int)response.StatusCode} {response.StatusCode}): {json}");
        }

        var result = JsonSerializer.Deserialize<AttachmentsResponse>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result?.Value ?? new List<Attachment>();
    }
}
