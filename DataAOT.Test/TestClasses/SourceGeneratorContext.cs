using System.Text.Json.Serialization;

namespace DataAOT.Test.TestClasses;

/// <summary>
/// We need this to serialize to JSON with AOT
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UserAccountModel))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}