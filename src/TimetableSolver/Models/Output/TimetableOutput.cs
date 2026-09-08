using System.Text.Json.Serialization;
using TimetableSolver.Models.Conflicts;

namespace TimetableSolver.Models.Output;

public class TimetableResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("solver")]
    public SolverMetadata Solver { get; set; } = new();

    [JsonPropertyName("summary")]
    public SummaryMetrics Summary { get; set; } = new();

    [JsonPropertyName("dataConflicts")]
    public List<DataConflict> DataConflicts { get; set; } = new();

    [JsonPropertyName("schedulingConflicts")]
    public List<string> SchedulingConflicts { get; set; } = new();

    [JsonPropertyName("sectionTimetables")]
    public Dictionary<string, Dictionary<string, List<SectionPeriodAssignment>>> SectionTimetables { get; set; } = new();

    [JsonPropertyName("teacherTimetables")]
    public Dictionary<string, Dictionary<string, List<TeacherPeriodAssignment>>> TeacherTimetables { get; set; } = new();

    [JsonPropertyName("validation")]
    public ValidationReport Validation { get; set; } = new();
}

public class SolverMetadata
{
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "Google OR-Tools CP-SAT";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "UNKNOWN";

    [JsonPropertyName("wallTimeMs")]
    public long WallTimeMs { get; set; }

    [JsonPropertyName("timeLimitSeconds")]
    public double TimeLimitSeconds { get; set; } = 300;

    [JsonPropertyName("objectiveValue")]
    public double ObjectiveValue { get; set; }
}

public class SummaryMetrics
{
    [JsonPropertyName("sectionsTotal")]
    public int SectionsTotal { get; set; }

    [JsonPropertyName("sectionsScheduled")]
    public int SectionsScheduled { get; set; }

    [JsonPropertyName("totalSlotsScheduled")]
    public int TotalSlotsScheduled { get; set; }

    [JsonPropertyName("teachersInvolved")]
    public int TeachersInvolved { get; set; }

    [JsonPropertyName("unassignedPlaceholderRows")]
    public int UnassignedPlaceholderRows { get; set; }

    [JsonPropertyName("zeroWorkloadRows")]
    public int ZeroWorkloadRows { get; set; }
}

public class SectionPeriodAssignment
{
    [JsonPropertyName("period")]
    public int Period { get; set; }

    [JsonPropertyName("slot")]
    public string? Slot { get; set; }

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("teacherCode")]
    public string TeacherCode { get; set; } = string.Empty;

    [JsonPropertyName("teacherName")]
    public string TeacherName { get; set; } = string.Empty;

    [JsonPropertyName("isActivity")]
    public bool IsActivity { get; set; }
}

public class TeacherPeriodAssignment
{
    [JsonPropertyName("period")]
    public int Period { get; set; }

    [JsonPropertyName("slot")]
    public string? Slot { get; set; }

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
}

public class ValidationReport
{
    [JsonPropertyName("hardConstraintsSatisfied")]
    public bool HardConstraintsSatisfied { get; set; } = true;

    [JsonPropertyName("softConstraintPenalty")]
    public double SoftConstraintPenalty { get; set; }

    [JsonPropertyName("violatedSoftConstraints")]
    public List<string> ViolatedSoftConstraints { get; set; } = new();
}
