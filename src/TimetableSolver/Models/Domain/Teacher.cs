using System.Text.Json.Serialization;

namespace TimetableSolver.Models.Domain;

public class Teacher
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("maxPeriodsPerDay")]
    public int? MaxPeriodsPerDay { get; set; }

    [JsonPropertyName("maxPeriodsPerWeek")]
    public int? MaxPeriodsPerWeek { get; set; }

    [JsonPropertyName("assignedClasses")]
    public List<string> AssignedClasses { get; set; } = new();

    public override string ToString() => $"{Name} ({Code})";
}
