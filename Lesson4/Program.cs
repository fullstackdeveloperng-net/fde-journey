using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", apiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

// 🗄️ Fake SGI policy system
var policies = new Dictionary<string, Policy>
{
    ["SGI-4471-883"] = new("Alex Morgan", "1985-03-14", "2019 Honda Civic",  "Active", "Basic + Collision", 700),
    ["SGI-1002-555"] = new("Priya Kumar", "1990-11-02", "2021 Toyota RAV4", "Lapsed", "Basic",             1000),
};

// 🔐 The LOCK: lives in C#, not in the prompt
string? verifiedPolicy = null;
int failedAttempts = 0;

string VerifyIdentity(string policyNumber, string dateOfBirth)
{
    Console.WriteLine($"   🔐 [C#: VerifyIdentity(\"{policyNumber}\", \"{dateOfBirth}\")]");
    if (failedAttempts >= 3)
    {
        Console.WriteLine("   🔒 [C#: LOCKED OUT - too many failed attempts]");
        return "{\"verified\": false, \"error\": \"Too many failed attempts. Please call SGI directly.\"}";
    }
    if (policies.TryGetValue(policyNumber, out var p) && p.DateOfBirth == dateOfBirth)
    {
        verifiedPolicy = policyNumber;
        return "{\"verified\": true}";
    }
    failedAttempts++;
    return "{\"verified\": false, \"error\": \"Policy number and date of birth do not match.\"}";
}

string LookupPolicy(string policyNumber)
{
    Console.WriteLine($"   🍳 [C#: LookupPolicy(\"{policyNumber}\")]");
    if (verifiedPolicy != policyNumber)
    {
        Console.WriteLine("   🚫 [C#: BLOCKED - caller not verified for this policy]");
        return "{\"error\": \"Access denied. The caller must be verified for this policy first.\"}";
    }
    var p = policies[policyNumber];
    // Return only what's needed; never send the date of birth back
    return JsonSerializer.Serialize(new { p.Holder, p.Vehicle, p.Status, p.Coverage, p.Deductible });
}

// 🔀 Dispatcher: the model chooses, the code routes
string RunTool(string name, JsonElement input) => name switch
{
    "verify_identity" => VerifyIdentity(input.GetProperty("policyNumber").GetString()!,
                                        input.GetProperty("dateOfBirth").GetString()!),
    "lookup_policy"   => LookupPolicy(input.GetProperty("policyNumber").GetString()!),
    _                 => "{\"error\": \"Unknown tool\"}"
};



// 📋 The menu
var tools = new object[]
{
    new
    {
        name = "verify_identity",
        description = "Verify the caller owns a policy by checking policy number and date of birth. Must succeed before any policy details are shared.",
        input_schema = new
        {
            type = "object",
            properties = new
            {
                policyNumber = new { type = "string", description = "e.g. SGI-4471-883" },
                dateOfBirth  = new { type = "string", description = "Format YYYY-MM-DD" }
            },
            required = new[] { "policyNumber", "dateOfBirth" }
        }
    },
    new
    {
        name = "lookup_policy",
        description = "Look up an SGI auto policy. Returns holder, vehicle, status, coverage and deductible. Only works for a verified caller.",
        input_schema = new
        {
            type = "object",
            properties = new { policyNumber = new { type = "string", description = "e.g. SGI-4471-883" } },
            required = new[] { "policyNumber" }
        }
    }
};

var system = "You are an SGI claims assistant. Before sharing any policy details, verify the caller with verify_identity using their policy number and date of birth. Keep answers short. Plain text, no Markdown.";
var messages = new List<object>();

Console.WriteLine("SGI Claims Assistant (type 'exit' to quit)");

// 💬 Outer loop: the conversation with the customer
while (true)
{
    Console.Write("\nYou: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput) || userInput == "exit") break;
    messages.Add(new { role = "user", content = userInput });

    // 🔁 Inner loop: the agent working (same as Lesson 3)
    for (int turn = 1; turn <= 5; turn++)
    {
        var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages",
            new { model = "claude-sonnet-5-5", max_tokens = 1500, system, tools, messages });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

        var stopReason = json.GetProperty("stop_reason").GetString();
        var content = json.GetProperty("content");
        messages.Add(new { role = "assistant", content });

        if (stopReason != "tool_use")
        {
            foreach (var block in content.EnumerateArray())
                if (block.GetProperty("type").GetString() == "text")
                    Console.WriteLine($"Agent: {block.GetProperty("text").GetString()}");
            break;
        }

        var results = new List<object>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() != "tool_use") continue;
            results.Add(new
            {
                type = "tool_result",
                tool_use_id = block.GetProperty("id").GetString(),
                content = RunTool(block.GetProperty("name").GetString()!, block.GetProperty("input"))
            });
        }
        messages.Add(new { role = "user", content = results });
    }
}

record Policy(string Holder, string DateOfBirth, string Vehicle, string Status, string Coverage, int Deductible);