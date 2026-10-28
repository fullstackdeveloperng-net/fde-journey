using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", apiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

var customerMessage = """
    Hi, so yesterday around 6pm I was at the Superstore on Albert St in Regina
    and some guy backed his truck into my Honda Civic. Rear bumper is smashed.
    Nobody got hurt thank god. My policy is SGI-4471-883. I got his plate,
    it's ABC 123. Didn't call the cops.
    """;

var request = new
{
    model = "claude-sonnet-5-5",
    max_tokens = 1000,
    system = "You extract claim details from customer messages. Use null when information is missing. Never guess. Always call the record_claim tool. Also write a friendly note to the customer",

    // The "online form": a tool with a JSON schema
    tools = new[]
    {
        new
        {
            name = "record_claim",
            description = "Record the structured details of an auto insurance claim.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    policyNumber      = new { type = new[] { "string", "null" } },
                    incidentLocation  = new { type = new[] { "string", "null" } },
                    vehicle           = new { type = new[] { "string", "null" } },
                    damageDescription = new { type = new[] { "string", "null" } },
                    injuries          = new { type = new[] { "boolean" } },
                    policeReported    = new { type = new[] { "boolean" } },
                    otherPartyPlate   = new { type = new[] { "string", "null" } }
                },
                required = new[] { "policyNumber", "incidentLocation", "vehicle",
                                   "damageDescription", "injuries", "policeReported", "otherPartyPlate" }
            }
        }
    },

    // Force the model to fill in THIS form
   // tool_choice = new { type = "tool", name = "record_claim" },

    // changing type to auto
    tool_choice = new { type = "auto" },

    messages = new[] { new { role = "user", content = customerMessage } }
};

var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();
if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

// Lesson 1 habit pays off: look for the TOOL_USE block this time
var toolBlock = json.GetProperty("content").EnumerateArray()
    .FirstOrDefault(b => b.GetProperty("type").GetString() == "tool_use");

if (toolBlock.ValueKind == JsonValueKind.Undefined)
{
    Console.WriteLine("Model did not call the tool. Raw response:");
    Console.WriteLine(json);
    return;   // in production: retry, or route to a human
}

var input = toolBlock.GetProperty("input");
Console.WriteLine("TOOL INPUT:\n" + input);

// No string parsing needed: "input" is already a JSON object
var claim = input.Deserialize<ClaimIntake>(
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

Console.WriteLine($"\nPolicy:      {claim!.PolicyNumber}");
Console.WriteLine($"Injuries:    {claim.Injuries}");
Console.WriteLine($"Other plate: {claim.OtherPartyPlate}");

record ClaimIntake(
    string? PolicyNumber,
    string? IncidentLocation,
    string? Vehicle,
    string? DamageDescription,
    bool Injuries,
    bool PoliceReported,
    string? OtherPartyPlate);