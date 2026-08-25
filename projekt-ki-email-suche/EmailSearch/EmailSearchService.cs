using System.Net.Http.Headers;
using System.Text.Json;

namespace EmailSearch;

public static class EmailSearchService
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";

    public static async Task<List<EmailMessage>> SearchMessagesAsync(
        HttpClient client, string accessToken, string mailbox, string searchTerm)
    {
        var url = $"{GraphBase}/users/{Uri.EscapeDataString(mailbox)}/messages" +
                  $"?$search=\"{Uri.EscapeDataString(searchTerm)}\"" +
                  "&$top=50" +
                  "&$select=subject,from,receivedDateTime,hasAttachments,webLink";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("ConsistencyLevel", "eventual");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<MessageSearchResponse>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result?.Value ?? new List<EmailMessage>();
    }
}
