using TimetableSolver.Models.Conflicts;

namespace TimetableSolver.Models.Domain;

public class NormalizedSchoolDataset
{
    public string SchoolName { get; set; } = "School Timetable 2026-27";
    public string AcademicYear { get; set; } = "2026-27";
    public BellSchedule BellSchedule { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
    public List<Teacher> Teachers { get; set; } = new();
    public List<CurriculumSubject> CurriculumItems { get; set; } = new();
    public List<TeachingAssignment> Assignments { get; set; } = new();
    public List<DataConflict> DataConflicts { get; set; } = new();
    public int UnassignedPlaceholderCount { get; set; }
    public int ZeroWorkloadCount { get; set; }
}
