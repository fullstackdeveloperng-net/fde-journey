using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", apiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

// 🍳 THE KITCHEN: a fake "SGI policy system" and your C# function
var policies = new Dictionary<string, object>
{
    ["SGI-4471-883"] = new { holder = "Alex Morgan", vehicle = "2019 Honda Civic",  status = "Active", coverage = "Basic + Collision", deductible = 700 },
    ["SGI-1002-555"] = new { holder = "Priya Kumar", vehicle = "2021 Toyota RAV4", status = "Lapsed", coverage = "Basic",             deductible = 1000 }
};

string LookupPolicy(string policyNumber)
{
    Console.WriteLine($"   🍳 [C# function called: LookupPolicy(\"{policyNumber}\")]");
    return policies.TryGetValue(policyNumber, out var p)
        ? JsonSerializer.Serialize(p)
        : "{\"error\": \"Policy not found\"}";
}

// 📋 THE MENU: tell the model which tools exist
var tools = new[]
{
    new
    {
        name = "lookup_policy",
        description = "Look up an SGI auto policy by policy number. Returns holder, vehicle, status, coverage and deductible.",
        input_schema = new
        {
            type = "object",
            properties = new { policyNumber = new { type = "string", description = "e.g. SGI-4471-883" } },
            required = new[] { "policyNumber" }
        }
    }
};

var system = "You are an SGI claims assistant. Use the lookup_policy tool to check policy details before answering coverage questions. Keep answers short. Plain text, no Markdown.";

// Lesson 1: the waiter has no memory, so WE keep the history
var messages = new List<object>
{
    new { role = "user", content = "Hi, someone backed into my car at Superstore. My policy is SGI-1002-555. Am I covered, and what will I pay?" }
};

// 🔁 THE AGENT LOOP
for (int turn = 1; turn <= 5; turn++)   // safety limit: agents can loop forever
{
    var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages",
        new { model = "claude-sonnet-5-5", max_tokens = 1500, system, tools, messages });
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

    var stopReason = json.GetProperty("stop_reason").GetString();
    var content = json.GetProperty("content");
    Console.WriteLine($"\n--- Turn {turn}: stop_reason = {stopReason} ---");

    // Add the model's full reply to history (thinking + tool_use blocks, unchanged)
    messages.Add(new { role = "assistant", content });

    // Done? Serve the dish.
    if (stopReason != "tool_use")
    {
        foreach (var block in content.EnumerateArray())
            if (block.GetProperty("type").GetString() == "text")
                Console.WriteLine(block.GetProperty("text").GetString());
        break;
    }

    // The model wrote a ticket: cook it and send the dish back
    var results = new List<object>();
    foreach (var block in content.EnumerateArray())
    {
        if (block.GetProperty("type").GetString() != "tool_use") continue;

        var policyNumber = block.GetProperty("input").GetProperty("policyNumber").GetString()!;
        results.Add(new
        {
            type = "tool_result",
            tool_use_id = block.GetProperty("id").GetString(),   // matches the ticket to the dish
            content = LookupPolicy(policyNumber)
        });
    }
    messages.Add(new { role = "user", content = results });
}