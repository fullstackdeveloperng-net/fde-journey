using System.Net.Http.Json;
using System.Text.Json;

var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");

var http = new HttpClient();
http.DefaultRequestHeaders.Add("x-api-key", apiKey);
http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

// The "letter": a messy, real-world customer message
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
    // system = """
    //     You extract claim details from customer messages.
    //     Respond with ONLY a JSON object, no other text, in exactly this shape:
    //     {
    //       "policyNumber": string or null,
    //       "incidentLocation": string or null,
    //       "vehicle": string or null,
    //       "damageDescription": string or null,
    //       "injuries": boolean,
    //       "policeReported": boolean,
    //       "otherPartyPlate": string or null
    //     }
    //     Use null when information is missing. Never guess.
    //     """,
     system = """
        You extract claim details from customer messages.
        First write a short friendly note to the customer, then give the JSON object in exactly this shape:
        {
          "policyNumber": string or null,
          "incidentLocation": string or null,
          "vehicle": string or null,
          "damageDescription": string or null,
          "injuries": boolean,
          "policeReported": boolean,
          "otherPartyPlate": string or null
        }
        Use null when information is missing. Never guess.
        """,
    messages = new[] { new { role = "user", content = customerMessage } }
};

var response = await http.PostAsJsonAsync("https://api.anthropic.com/v1/messages", request);
var json = await response.Content.ReadFromJsonAsync<JsonElement>();
if (!response.IsSuccessStatusCode) { Console.WriteLine(json); return; }

// Lesson 1 habit: find the TEXT block, don't assume [0]
var text = json.GetProperty("content").EnumerateArray()
    .First(b => b.GetProperty("type").GetString() == "text")
    .GetProperty("text").GetString()!;

Console.WriteLine("RAW MODEL OUTPUT:\n" + text);

// The "form": deserialize into a real C# object
var claim = JsonSerializer.Deserialize<ClaimIntake>(text,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

Console.WriteLine($"\nPolicy:      {claim!.PolicyNumber}");
Console.WriteLine($"Location:    {claim.IncidentLocation}");
Console.WriteLine($"Vehicle:     {claim.Vehicle}");
Console.WriteLine($"Damage:      {claim.DamageDescription}");
Console.WriteLine($"Injuries:    {claim.Injuries}");
Console.WriteLine($"Police:      {claim.PoliceReported}");
Console.WriteLine($"Other plate: {claim.OtherPartyPlate}");

record ClaimIntake(
    string? PolicyNumber,
    string? IncidentLocation,
    string? Vehicle,
    string? DamageDescription,
    bool Injuries,
    bool PoliceReported,
    string? OtherPartyPlate);