using System.Text.Json.Serialization;

namespace HrastERP.API.Models;

public record ErrorResponse(string Code, string Message)
{
    // Present only on validation failures. Omitted from JSON entirely when null so that
    // non-validation responses (404, 500, etc.) do not include an "errors" key at all.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
