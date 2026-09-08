using System.Text.Json.Serialization;

namespace TimetableSolver.Models.Domain;

public class Section
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("grade")]
    public string Grade { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string SectionName { get; set; } = string.Empty;

    [JsonPropertyName("curriculumKey")]
    public string CurriculumKey { get; set; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; } = 40;

    public bool IsSeniorSecondary =>
        CurriculumKey.Contains("11", StringComparison.OrdinalIgnoreCase) ||
        CurriculumKey.Contains("12", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => DisplayName;
}

public class SectionsRoot
{
    [JsonPropertyName("academicYear")]
    public string AcademicYear { get; set; } = "2026-27";

    [JsonPropertyName("weeklyTeachingCapacity")]
    public int WeeklyTeachingCapacity { get; set; } = 52;

    [JsonPropertyName("totalSections")]
    public int TotalSections { get; set; } = 41;

    [JsonPropertyName("sections")]
    public List<Section> Sections { get; set; } = new();

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
