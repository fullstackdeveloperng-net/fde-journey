using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", apiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

var request = new
{
    model = "claude-sonnet-5-5",
    max_tokens = 300,
    system = "You are a friendly waiter at a South Indian restaurant. Keep answers short.Reply in plain text, no Markdown.",
    messages = new[]
    {
        new { role = "user", content = "What do you recommend for breakfast?" }
    }
};

var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();

if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

Console.WriteLine(json.GetProperty("content")[0].GetProperty("text").GetString());
var usage = json.GetProperty("usage");
Console.WriteLine($"\n[Tokens in: {usage.GetProperty("input_tokens")}, out: {usage.GetProperty("output_tokens")}]");