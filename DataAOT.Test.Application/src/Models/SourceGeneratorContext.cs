using System.Text.Json.Serialization;

namespace DataAOT.Test.Application.Models;

/// <summary>
/// We need this to serialize to JSON with AOT
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UserAccount))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}