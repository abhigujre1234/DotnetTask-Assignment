using Google.OrTools.Sat;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Constraints;

public class CpModelContext
{
    public CpModel Model { get; } = new();
    public NormalizedSchoolDataset Dataset { get; }

    // X[AssignmentId, Day, Period] -> BoolVar
    public Dictionary<(string AssignmentId, string Day, int Period), BoolVar> AssignmentVars { get; } = new();

    // DayActive[AssignmentId, Day] -> BoolVar (1 if subject is taught on this day)
    public Dictionary<(string AssignmentId, string Day), BoolVar> DayActiveVars { get; } = new();

    // Lookups
    public List<Section> SchedulableSections { get; } = new();
    public List<TeachingAssignment> SchedulableAssignments { get; } = new();
    public Dictionary<string, List<TeachingAssignment>> AssignmentsBySection { get; } = new();
    public Dictionary<string, List<TeachingAssignment>> AssignmentsByTeacher { get; } = new();

    public CpModelContext(NormalizedSchoolDataset dataset)
    {
        Dataset = dataset;

        // Filter active schedulable assignments
        SchedulableAssignments = dataset.Assignments
            .Where(a => a.PeriodsPerWeek > 0)
            .ToList();

        AssignmentsBySection = SchedulableAssignments
            .GroupBy(a => a.SectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        AssignmentsByTeacher = SchedulableAssignments
            .Where(a => !a.IsActivity && !a.IsUnassigned && !string.IsNullOrEmpty(a.TeacherCode))
            .GroupBy(a => a.TeacherCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        SchedulableSections = dataset.Sections
            .Where(s => AssignmentsBySection.ContainsKey(s.Id))
            .ToList();
    }

    public void CreateDecisionVariables()
    {
        var workingDays = Dataset.BellSchedule.WorkingDays.Count > 0
            ? Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var assignment in SchedulableAssignments)
        {
            foreach (var day in workingDays)
            {
                var teachingSlots = Dataset.BellSchedule.GetTeachingSlotsForDay(day);

                // Day presence variable
                var dayActiveVar = Model.NewBoolVar($"active_{assignment.SectionId}_{assignment.SubjectName}_{day}");
                DayActiveVars[(assignment.AssignmentId, day)] = dayActiveVar;

                var daySlotVars = new List<BoolVar>();

                foreach (var slot in teachingSlots)
                {
                    var varName = $"x_{assignment.SectionId}_{assignment.SubjectName}_{day}_p{slot.Period}";
                    var assignVar = Model.NewBoolVar(varName);

                    AssignmentVars[(assignment.AssignmentId, day, slot.Period)] = assignVar;
                    daySlotVars.Add(assignVar);

                    // Link assignVar <= dayActiveVar (Assigning any slot on this day forces DayActive = 1)
                    Model.Add(assignVar <= dayActiveVar);
                }

                // If DayActive == 1, then at least 1 slot is assigned on that day
                Model.Add(LinearExpr.Sum(daySlotVars) >= dayActiveVar);
            }
        }
    }

    public BoolVar? GetVar(string assignmentId, string day, int period)
    {
        return AssignmentVars.TryGetValue((assignmentId, day, period), out var v) ? v : null;
    }

    public BoolVar? GetDayActiveVar(string assignmentId, string day)
    {
        return DayActiveVars.TryGetValue((assignmentId, day), out var v) ? v : null;
    }
}
