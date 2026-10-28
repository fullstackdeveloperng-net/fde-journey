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
    system = "You are an SGI auto insurance claims assistant. Ask for the policy number before anything else. Keep answers short. Reply in plain text, no Markdown.",
   messages = new[]
{
    new { role = "user",      content = "Someone hit my car in the parking lot." },
    new { role = "assistant", content = "I'm sorry to hear that. What is your policy number?" },
    new { role = "user",      content = "What was my first question?" }
}
};

var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();

if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

Console.WriteLine(json); // temporary: see the raw response

foreach (var block in json.GetProperty("content").EnumerateArray())
{
    if (block.GetProperty("type").GetString() == "text")
        Console.WriteLine(block.GetProperty("text").GetString());
}
var usage = json.GetProperty("usage");
Console.WriteLine($"\n[Tokens in: {usage.GetProperty("input_tokens")}, out: {usage.GetProperty("output_tokens")}]");