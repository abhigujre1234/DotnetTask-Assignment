using Google.OrTools.Sat;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Constraints;

public class HardConstraints
{
    public static void Apply(CpModelContext context)
    {
        ApplySlotUniqueness(context);          // H3
        ApplyNoTeacherDoubleBooking(context);  // H1
        ApplyWeeklyCurriculumQuota(context);   // H8
        ApplyDailySubjectCap(context);         // H7
        ApplyDayRestrictions(context);         // H6
        ApplyActivityFixedSlots(context);      // H9
        ApplyGamesLibraryRules(context);       // G1, G2
        ApplySameFirstWordBan(context);        // L1
        ApplyNoBreakCrossing(context);         // L2
        ApplyMaxConsecutiveLimit(context);     // L3
        ApplyBlockPairingRules(context);       // H5, B1
    }

    /// <summary>
    /// H3: Each section has exactly one lesson per valid teaching day/period.
    /// </summary>
    private static void ApplySlotUniqueness(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var section in context.SchedulableSections)
        {
            if (!context.AssignmentsBySection.TryGetValue(section.Id, out var assignments))
                continue;

            var totalPpw = assignments.Sum(a => a.PeriodsPerWeek);
            var isFullSection = totalPpw == context.Dataset.BellSchedule.WeeklyTeachingCapacity;

            foreach (var day in workingDays)
            {
                var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);
                foreach (var slot in slots)
                {
                    var varsInSlot = new List<BoolVar>();
                    foreach (var a in assignments)
                    {
                        var v = context.GetVar(a.AssignmentId, day, slot.Period);
                        if (v is not null)
                        {
                            varsInSlot.Add(v);
                        }
                    }

                    if (varsInSlot.Count > 0)
                    {
                        if (isFullSection)
                        {
                            context.Model.Add(LinearExpr.Sum(varsInSlot) == 1);
                        }
                        else
                        {
                            context.Model.Add(LinearExpr.Sum(varsInSlot) <= 1);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// H1: A teacher cannot teach two sections at the same day/period.
    /// </summary>
    private static void ApplyNoTeacherDoubleBooking(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var (teacherCode, assignments) in context.AssignmentsByTeacher)
        {
            if (assignments.Count <= 1)
                continue;

            foreach (var day in workingDays)
            {
                var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);
                foreach (var slot in slots)
                {
                    var teacherVars = new List<BoolVar>();
                    foreach (var a in assignments)
                    {
                        var v = context.GetVar(a.AssignmentId, day, slot.Period);
                        if (v is not null)
                        {
                            teacherVars.Add(v);
                        }
                    }

                    if (teacherVars.Count > 1)
                    {
                        context.Model.AddAtMostOne(teacherVars);
                    }
                }
            }
        }
    }

    /// <summary>
    /// H8: Each assignment receives exactly periodsPerWeek.
    /// </summary>
    private static void ApplyWeeklyCurriculumQuota(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments)
        {
            var allVars = new List<BoolVar>();
            foreach (var day in workingDays)
            {
                var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);
                foreach (var slot in slots)
                {
                    var v = context.GetVar(a.AssignmentId, day, slot.Period);
                    if (v is not null)
                    {
                        allVars.Add(v);
                    }
                }
            }

            if (allVars.Count > 0)
            {
                context.Model.Add(LinearExpr.Sum(allVars) == a.PeriodsPerWeek);
            }
        }
    }

    /// <summary>
    /// H7: A subject cannot exceed periodsPerDay on any single day for a section.
    /// </summary>
    private static void ApplyDailySubjectCap(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments)
        {
            foreach (var day in workingDays)
            {
                var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);
                var dayVars = new List<BoolVar>();
                foreach (var slot in slots)
                {
                    var v = context.GetVar(a.AssignmentId, day, slot.Period);
                    if (v is not null)
                    {
                        dayVars.Add(v);
                    }
                }

                if (dayVars.Count > 0)
                {
                    context.Model.Add(LinearExpr.Sum(dayVars) <= a.MaxPeriodsPerDay);
                }
            }
        }
    }

    /// <summary>
    /// H6: Day restrictions (e.g. ONLY ON Saturday) fixed to 0 on non-allowed days.
    /// </summary>
    private static void ApplyDayRestrictions(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments.Where(a => a.DayRestrictions.Count > 0))
        {
            var allowedSet = new HashSet<string>(a.DayRestrictions, StringComparer.OrdinalIgnoreCase);

            foreach (var day in workingDays)
            {
                if (!allowedSet.Contains(day))
                {
                    var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(day);
                    foreach (var slot in slots)
                    {
                        var v = context.GetVar(a.AssignmentId, day, slot.Period);
                        if (v is not null)
                        {
                            context.Model.Add(v == 0);
                        }
                    }

                    var dayActiveVar = context.GetDayActiveVar(a.AssignmentId, day);
                    if (dayActiveVar is not null)
                    {
                        context.Model.Add(dayActiveVar == 0);
                    }
                }
            }
        }
    }

    /// <summary>
    /// H9: Activity fixed slots respected when present.
    /// </summary>
    private static void ApplyActivityFixedSlots(CpModelContext context)
    {
        foreach (var a in context.SchedulableAssignments.Where(a => a.FixedSlots.Count > 0))
        {
            foreach (var fix in a.FixedSlots)
            {
                if (fix.Period.HasValue)
                {
                    var v = context.GetVar(a.AssignmentId, fix.Day, fix.Period.Value);
                    if (v is not null)
                    {
                        context.Model.Add(v == 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// G1: Games and Library not in first slot pair (periods 1–2).
    /// G2: Class 11 & 12: Games and Library cannot both appear on the same day.
    /// </summary>
    private static void ApplyGamesLibraryRules(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        // G1: Not in periods 1 & 2
        foreach (var a in context.SchedulableAssignments.Where(a => a.IsGamesOrLibrary || a.RestrictedPeriods.Contains(1)))
        {
            foreach (var day in workingDays)
            {
                var v1 = context.GetVar(a.AssignmentId, day, 1);
                var v2 = context.GetVar(a.AssignmentId, day, 2);

                if (v1 is not null) context.Model.Add(v1 == 0);
                if (v2 is not null) context.Model.Add(v2 == 0);
            }
        }

        // G2: For Class 11 & 12 sections: Games and Library cannot both appear on the same day
        foreach (var section in context.SchedulableSections.Where(s => s.IsSeniorSecondary))
        {
            if (!context.AssignmentsBySection.TryGetValue(section.Id, out var assignments))
                continue;

            var games = assignments.Where(a => a.SubjectName.Equals("Games", StringComparison.OrdinalIgnoreCase)).ToList();
            var library = assignments.Where(a => a.SubjectName.Equals("Library", StringComparison.OrdinalIgnoreCase)).ToList();

            if (games.Count > 0 && library.Count > 0)
            {
                foreach (var day in workingDays)
                {
                    var gamesVars = games.Select(a => context.GetDayActiveVar(a.AssignmentId, day)).Where(v => v is not null).Select(v => v!).ToList();
                    var libVars = library.Select(a => context.GetDayActiveVar(a.AssignmentId, day)).Where(v => v is not null).Select(v => v!).ToList();

                    if (gamesVars.Count > 0 && libVars.Count > 0)
                    {
                        var both = gamesVars.Concat(libVars).ToList();
                        context.Model.AddAtMostOne(both);
                    }
                }
            }
        }
    }

    /// <summary>
    /// L1: Same-first-word subjects (e.g. English Language + English Literature) cannot share a day
    /// when the sum of required days fits within the 6-day week.
    /// </summary>
    private static void ApplySameFirstWordBan(CpModelContext context)
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

                if (minDaysNeeded <= workingDays.Count)
                {
                    foreach (var day in workingDays)
                    {
                        var dayActiveVars = new List<BoolVar>();
                        foreach (var a in groupAssignments)
                        {
                            var v = context.GetDayActiveVar(a.AssignmentId, day);
                            if (v is not null)
                            {
                                dayActiveVars.Add(v);
                            }
                        }

                        if (dayActiveVars.Count > 1)
                        {
                            context.Model.AddAtMostOne(dayActiveVars);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// L2: Subject cannot span last slot before break AND first slot after break.
    /// </summary>
    private static void ApplyNoBreakCrossing(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments)
        {
            foreach (var day in workingDays)
            {
                var isMonToThu = Days.IsMondayToThursday(day);

                // Short break 1 is between Period 2 and 3
                var v2 = context.GetVar(a.AssignmentId, day, 2);
                var v3 = context.GetVar(a.AssignmentId, day, 3);
                if (v2 is not null && v3 is not null)
                {
                    context.Model.Add(v2 + v3 <= 1);
                }

                // Lunch break is between Period 4 and 5
                var v4 = context.GetVar(a.AssignmentId, day, 4);
                var v5 = context.GetVar(a.AssignmentId, day, 5);
                if (v4 is not null && v5 is not null)
                {
                    context.Model.Add(v4 + v5 <= 1);
                }

                // Short break 2 on Mon-Thu is between Period 6 and 7
                if (isMonToThu)
                {
                    var v6 = context.GetVar(a.AssignmentId, day, 6);
                    var v7 = context.GetVar(a.AssignmentId, day, 7);
                    if (v6 is not null && v7 is not null)
                    {
                        context.Model.Add(v6 + v7 <= 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// L3: Max 3 consecutive slots with the same subject.
    /// </summary>
    private static void ApplyMaxConsecutiveLimit(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments)
        {
            foreach (var day in workingDays)
            {
                var isMonToThu = Days.IsMondayToThursday(day);

                if (isMonToThu)
                {
                    var v5 = context.GetVar(a.AssignmentId, day, 5);
                    var v6 = context.GetVar(a.AssignmentId, day, 6);
                    var v7 = context.GetVar(a.AssignmentId, day, 7);
                    var v8 = context.GetVar(a.AssignmentId, day, 8);
                    if (v5 is not null && v6 is not null && v7 is not null && v8 is not null)
                    {
                        context.Model.Add(v5 + v6 + v7 + v8 <= 3);
                    }
                }
            }
        }
    }

    /// <summary>
    /// H5 & B1: Block subjects (periodsPerDay >= 2) occupy consecutive non-break pairs.
    /// Whenever 2 periods of a block subject appear on a day, they must occupy an adjacent pair (not split).
    /// </summary>
    private static void ApplyBlockPairingRules(CpModelContext context)
    {
        var workingDays = context.Dataset.BellSchedule.WorkingDays.Count > 0
            ? context.Dataset.BellSchedule.WorkingDays
            : Days.All;

        foreach (var a in context.SchedulableAssignments.Where(a => a.IsBlockSubject))
        {
            foreach (var d in workingDays)
            {
                var slots = context.Dataset.BellSchedule.GetTeachingSlotsForDay(d);
                var dayVars = slots
                    .Select(sl => context.GetVar(a.AssignmentId, d, sl.Period))
                    .Where(v => v is not null)
                    .Select(v => v!)
                    .ToList();

                var pairs = context.Dataset.BellSchedule.GetSlotPairsForDay(d);
                var pairIndicatorVars = new List<BoolVar>();

                foreach (var pair in pairs)
                {
                    var v1 = context.GetVar(a.AssignmentId, d, pair.Period1);
                    var v2 = context.GetVar(a.AssignmentId, d, pair.Period2);

                    if (v1 is not null && v2 is not null)
                    {
                        var pairActive = context.Model.NewBoolVar($"pair_{a.AssignmentId}_{d}_{pair.Period1}");
                        pairIndicatorVars.Add(pairActive);

                        context.Model.Add(v1 >= pairActive);
                        context.Model.Add(v2 >= pairActive);
                    }
                }

                // If sum(dayVars) >= 2, at least one pair must be active
                var isDoubleDay = context.Model.NewBoolVar($"is_double_{a.AssignmentId}_{d}");
                context.Model.Add(LinearExpr.Sum(dayVars) >= 2).OnlyEnforceIf(isDoubleDay);
                context.Model.Add(LinearExpr.Sum(dayVars) < 2).OnlyEnforceIf(isDoubleDay.Not());

                context.Model.Add(LinearExpr.Sum(pairIndicatorVars) >= 1).OnlyEnforceIf(isDoubleDay);
            }
        }
    }
}
