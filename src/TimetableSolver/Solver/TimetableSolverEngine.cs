using System.Diagnostics;
using Google.OrTools.Sat;
using TimetableSolver.Constraints;
using TimetableSolver.Models.Domain;
using TimetableSolver.Models.Output;

namespace TimetableSolver.Solver;

public class TimetableSolverEngine
{
    private readonly SolverConfiguration _config;

    public TimetableSolverEngine(SolverConfiguration? config = null)
    {
        _config = config ?? new SolverConfiguration();
    }

    public TimetableResponse Solve(NormalizedSchoolDataset dataset)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Initialize Context & Variables
        var context = new CpModelContext(dataset);
        context.CreateDecisionVariables();

        // 2. Apply Hard Constraints
        HardConstraints.Apply(context);

        // 3. Apply Soft Constraints & Objective
        SoftConstraints.Apply(context);

        // 4. Configure CP-SAT Solver
        var solver = new CpSolver();
        solver.StringParameters = $"max_time_in_seconds:{_config.TimeLimitSeconds} num_search_workers:{_config.NumSearchWorkers} log_search_progress:{_config.LogSearchProgress.ToString().ToLowerInvariant()}";

        // 5. Execute Solver
        var status = solver.Solve(context.Model);
        stopwatch.Stop();

        // 6. Build Result Response
        var response = new TimetableResponse
        {
            Success = status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible,
            DataConflicts = dataset.DataConflicts,
            Solver = new SolverMetadata
            {
                Engine = "Google OR-Tools CP-SAT",
                Status = status.ToString().ToUpperInvariant(),
                WallTimeMs = stopwatch.ElapsedMilliseconds,
                TimeLimitSeconds = _config.TimeLimitSeconds,
                ObjectiveValue = status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible
                    ? solver.ObjectiveValue
                    : 0
            }
        };

        if (!response.Success)
        {
            response.SchedulingConflicts.Add("The CP-SAT solver determined that the problem is INFEASIBLE with the given hard constraints and assignments.");
            return response;
        }

        // 7. Extract Solution Timetables
        ExtractTimetables(context, solver, response);

        return response;
    }

    private void ExtractTimetables(CpModelContext context, CpSolver solver, TimetableResponse response)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        var sectionSchedules = new Dictionary<string, Dictionary<string, List<SectionPeriodAssignment>>>();
        var teacherSchedules = new Dictionary<string, Dictionary<string, List<TeacherPeriodAssignment>>>();

        int totalScheduledSlots = 0;
        var involvedTeachers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Populate Section Timetables
        foreach (var section in context.SchedulableSections)
        {
            var sectionDayMap = new Dictionary<string, List<SectionPeriodAssignment>>();

            if (context.AssignmentsBySection.TryGetValue(section.Id, out var assignments))
            {
                foreach (var day in workingDays)
                {
                    var daySlots = new List<SectionPeriodAssignment>();
                    var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);

                    foreach (var slot in slots)
                    {
                        foreach (var a in assignments)
                        {
                            var v = context.GetVar(a.AssignmentId, day, slot.Period);
                            if (v is not null && solver.BooleanValue(v))
                            {
                                totalScheduledSlots++;
                                if (!string.IsNullOrEmpty(a.TeacherName) && !a.IsUnassigned)
                                {
                                    involvedTeachers.Add(a.TeacherName);
                                }

                                var periodAssignment = new SectionPeriodAssignment
                                {
                                    Period = slot.Period,
                                    Slot = slot.Slot,
                                    Subject = a.SubjectName,
                                    TeacherCode = a.TeacherCode,
                                    TeacherName = a.TeacherName,
                                    IsActivity = a.IsActivity
                                };
                                daySlots.Add(periodAssignment);

                                // Add to Teacher Timetable if real assigned teacher
                                if (!a.IsActivity && !a.IsUnassigned && !string.IsNullOrEmpty(a.TeacherName))
                                {
                                    if (!teacherSchedules.TryGetValue(a.TeacherName, out var teacherDayMap))
                                    {
                                        teacherDayMap = new Dictionary<string, List<TeacherPeriodAssignment>>();
                                        teacherSchedules[a.TeacherName] = teacherDayMap;
                                    }

                                    if (!teacherDayMap.TryGetValue(day, out var teacherSlots))
                                    {
                                        teacherSlots = new List<TeacherPeriodAssignment>();
                                        teacherDayMap[day] = teacherSlots;
                                    }

                                    teacherSlots.Add(new TeacherPeriodAssignment
                                    {
                                        Period = slot.Period,
                                        Slot = slot.Slot,
                                        Section = section.DisplayName,
                                        Subject = a.SubjectName
                                    });
                                }

                                break;
                            }
                        }
                    }

                    // Order periods chronologically
                    sectionDayMap[day] = daySlots.OrderBy(s => s.Period).ToList();
                }
            }

            sectionSchedules[section.DisplayName] = sectionDayMap;
        }

        // Sort teacher periods
        foreach (var teacherDayMap in teacherSchedules.Values)
        {
            foreach (var day in teacherDayMap.Keys.ToList())
            {
                teacherDayMap[day] = teacherDayMap[day].OrderBy(s => s.Period).ToList();
            }
        }

        response.SectionTimetables = sectionSchedules;
        response.TeacherTimetables = teacherSchedules;

        response.Summary = new SummaryMetrics
        {
            SectionsTotal = context.Dataset.Sections.Count,
            SectionsScheduled = sectionSchedules.Count,
            TotalSlotsScheduled = totalScheduledSlots,
            TeachersInvolved = involvedTeachers.Count > 0 ? involvedTeachers.Count : context.Dataset.Teachers.Count,
            UnassignedPlaceholderRows = context.Dataset.UnassignedPlaceholderCount,
            ZeroWorkloadRows = context.Dataset.ZeroWorkloadCount
        };

        response.Validation = new ValidationReport
        {
            HardConstraintsSatisfied = true,
            SoftConstraintPenalty = response.Solver.ObjectiveValue,
            ViolatedSoftConstraints = response.Solver.ObjectiveValue > 0
                ? new List<string> { $"Math afternoon placement penalty: {response.Solver.ObjectiveValue}" }
                : new List<string>()
        };
    }
}
