namespace TimetableSolver.Models.Domain;

public class CurriculumSubject
{
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string Type { get; set; } = "Subject"; // Subject or Activity Group / Activity
    public int PeriodsPerDay { get; set; } = 1;
    public int PeriodsPerWeek { get; set; } = 1;
    public bool IsActivity => Type.Contains("Activity", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{ClassName} - {SubjectName} ({PeriodsPerWeek} ppw, {PeriodsPerDay} ppd)";
}

public class TeachingAssignment
{
    public string AssignmentId { get; set; } = Guid.NewGuid().ToString("N");
    public string SectionId { get; set; } = string.Empty;
    public string SectionDisplayName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int PeriodsPerWeek { get; set; }
    public int MaxPeriodsPerDay { get; set; } = 1;
    public bool IsActivity { get; set; }
    public List<string> DayRestrictions { get; set; } = new();
    public List<int> RestrictedPeriods { get; set; } = new();
    public List<FixedSlotSpec> FixedSlots { get; set; } = new();

    public bool IsBlockSubject => MaxPeriodsPerDay >= 2;

    public bool IsGamesOrLibrary =>
        SubjectName.Equals("Games", StringComparison.OrdinalIgnoreCase) ||
        SubjectName.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
        SubjectName.Equals("Games / Library", StringComparison.OrdinalIgnoreCase);

    public string FirstWord
    {
        get
        {
            var trimmed = SubjectName.Trim();
            var idx = trimmed.IndexOf(' ');
            return (idx > 0 ? trimmed.Substring(0, idx) : trimmed).ToUpperInvariant();
        }
    }

    public bool IsUnassigned =>
        string.IsNullOrWhiteSpace(TeacherCode) ||
        TeacherCode.Equals("UNASSIGNED-TT", StringComparison.OrdinalIgnoreCase) ||
        TeacherName.Contains("UNASSIGNED", StringComparison.OrdinalIgnoreCase);

    public override string ToString() =>
        $"[{SectionDisplayName}] {SubjectName} -> {TeacherName} ({TeacherCode}) [{PeriodsPerWeek} ppw]";
}
