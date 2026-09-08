using System.Text.Json.Serialization;

namespace TimetableSolver.Models.Conflicts;

public class DataConflict
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("teacherCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TeacherCode { get; set; }

    [JsonPropertyName("teacherName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TeacherName { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public override string ToString() => $"[{Type}] {Section} - {Subject}: {Message}";
}
