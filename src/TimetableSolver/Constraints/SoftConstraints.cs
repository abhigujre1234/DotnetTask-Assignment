using Google.OrTools.Sat;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Constraints;

public class SoftConstraints
{
    public static List<LinearExpr> Apply(CpModelContext context)
    {
        var penaltyTerms = new List<LinearExpr>();

        ApplyMathMorningPreference(context, penaltyTerms);
        ApplyGamesLibraryDistribution(context, penaltyTerms);
        ApplySameFirstWordPenaltyWhenOverCapacity(context, penaltyTerms);

        if (penaltyTerms.Count > 0)
        {
            context.Model.Minimize(LinearExpr.Sum(penaltyTerms));
        }

        return penaltyTerms;
    }

    /// <summary>
    /// PR-MATH (Weight 10): Prefer Mathematics in morning (periods 1–4 on Mon–Thu).
    /// </summary>
    private static void ApplyMathMorningPreference(CpModelContext context, List<LinearExpr> penaltyTerms)
    {
        var mathAssignments = context.SchedulableAssignments
            .Where(a => a.SubjectName.Contains("Math", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var a in mathAssignments)
        {
            foreach (var day in Days.MondayToThursday)
            {
                // Periods 5, 6, 7, 8 on Mon-Thu are afternoon periods
                for (int p = 5; p <= 8; p++)
                {
                    var v = context.GetVar(a.AssignmentId, day, p);
                    if (v is not null)
                    {
                        // Add penalty: 10 * v
                        penaltyTerms.Add(v * 10);
                    }
                }
            }
        }
    }

    /// <summary>
    /// DB-RULE (Weight 8): At most one Games / Library per section per day (Classes 1-10).
    /// </summary>
    private static void ApplyGamesLibraryDistribution(CpModelContext context, List<LinearExpr> penaltyTerms)
    {
        foreach (var section in context.SchedulableSections.Where(s => !s.IsSeniorSecondary))
        {
            if (!context.AssignmentsBySection.TryGetValue(section.Id, out var assignments))
                continue;

            var gamesAndLib = assignments.Where(a => a.IsGamesOrLibrary).ToList();
            if (gamesAndLib.Count > 1)
            {
                foreach (var day in Days.All)
                {
                    var dayVars = new List<BoolVar>();
                    foreach (var a in gamesAndLib)
                    {
                        var v = context.GetDayActiveVar(a.AssignmentId, day);
                        if (v is not null)
                        {
                            dayVars.Add(v);
                        }
                    }

                    if (dayVars.Count > 1)
                    {
                        var excessVar = context.Model.NewIntVar(0, dayVars.Count, $"excess_games_lib_{section.Id}_{day}");
                        context.Model.Add(excessVar >= LinearExpr.Sum(dayVars) - 1);
                        penaltyTerms.Add(excessVar * 8);
                    }
                }
            }
        }
    }

    /// <summary>
    /// L1 Penalty: For groups whose required days > working days (e.g. Class 11/12 English 12 ppw),
    /// minimize days where both variants are active.
    /// </summary>
    private static void ApplySameFirstWordPenaltyWhenOverCapacity(CpModelContext context, List<LinearExpr> penaltyTerms)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var section in context.SchedulableSections)
        {
            if (!context.AssignmentsBySection.TryGetValue(section.Id, out var assignments))
                continue;

            var groups = assignments
                .GroupBy(a => a.FirstWord)
                .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key));

            foreach (var group in groups)
            {
                var groupAssignments = group.ToList();
                var minDaysNeeded = groupAssignments.Sum(a => (int)Math.Ceiling((double)a.PeriodsPerWeek / a.MaxPeriodsPerDay));

                if (minDaysNeeded > workingDays.Count)
                {
                    foreach (var day in workingDays)
                    {
                        var dayVars = new List<BoolVar>();
                        foreach (var a in groupAssignments)
                        {
                            var v = context.GetDayActiveVar(a.AssignmentId, day);
                            if (v is not null)
                            {
                                dayVars.Add(v);
                            }
                        }

                        if (dayVars.Count > 1)
                        {
                            var overlapVar = context.Model.NewIntVar(0, dayVars.Count, $"overlap_first_word_{section.Id}_{day}");
                            context.Model.Add(overlapVar >= LinearExpr.Sum(dayVars) - 1);
                            penaltyTerms.Add(overlapVar * 50);
                        }
                    }
                }
            }
        }
    }
}
