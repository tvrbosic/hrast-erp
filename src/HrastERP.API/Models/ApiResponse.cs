using System.Text.Json.Serialization;

namespace HrastERP.API.Models;

public record ApiResponse<T>(T Data)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Meta { get; init; }
}
