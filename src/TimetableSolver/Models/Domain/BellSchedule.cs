using System.Text.Json.Serialization;

namespace TimetableSolver.Models.Domain;

public class BellSchedule
{
    [JsonPropertyName("scheduleName")]
    public string ScheduleName { get; set; } = "Schedule-1";

    [JsonPropertyName("workingDays")]
    public List<string> WorkingDays { get; set; } = new();

    [JsonPropertyName("dayNumbering")]
    public Dictionary<string, int> DayNumbering { get; set; } = new();

    [JsonPropertyName("mondayToThursday")]
    public DayScheduleProfile MondayToThursday { get; set; } = new();

    [JsonPropertyName("fridayAndSaturday")]
    public DayScheduleProfile FridayAndSaturday { get; set; } = new();

    [JsonPropertyName("weeklyTeachingCapacity")]
    public int WeeklyTeachingCapacity { get; set; } = 52;

    public DayScheduleProfile GetProfileForDay(string day)
    {
        return Days.IsMondayToThursday(day) ? MondayToThursday : FridayAndSaturday;
    }

    public List<TimeSlot> GetTeachingSlotsForDay(string day)
    {
        var profile = GetProfileForDay(day);
        var slots = new List<TimeSlot>();
        foreach (var p in profile.TeachingPeriods)
        {
            slots.Add(new TimeSlot
            {
                Day = day,
                Period = p.Period,
                Slot = p.Slot,
                Start = p.Start,
                End = p.End,
                Type = p.Type
            });
        }
        return slots;
    }

    public List<SlotPair> GetSlotPairsForDay(string day)
    {
        var profile = GetProfileForDay(day);
        var pairs = new List<SlotPair>();
        
        // Slot pairs are adjacent non-break teaching periods:
        // Mon-Thu: (1, 2) [1A-1B], (3, 4) [2A-2B], (5, 6) [3A-3B], (7, 8) [4A-4B]
        // Fri-Sat: (1, 2) [1A-1B], (3, 4) [2A-2B], (5, 6) [3A-3B], (7, 8) [4A-4B], (9, 10) [5A-5B]
        for (int i = 0; i < profile.TeachingPeriods.Count - 1; i += 2)
        {
            var p1 = profile.TeachingPeriods[i];
            var p2 = profile.TeachingPeriods[i + 1];
            pairs.Add(new SlotPair
            {
                Day = day,
                Period1 = p1.Period,
                Period2 = p2.Period,
                Label1 = p1.Slot,
                Label2 = p2.Slot
            });
        }
        return pairs;
    }
}

public class DayScheduleProfile
{
    [JsonPropertyName("teachingPeriods")]
    public List<PeriodInfo> TeachingPeriods { get; set; } = new();

    [JsonPropertyName("breakPeriods")]
    public List<BreakInfo> BreakPeriods { get; set; } = new();

    [JsonPropertyName("teachingPeriodsPerDay")]
    public int TeachingPeriodsPerDay { get; set; }

    [JsonPropertyName("morningPeriods")]
    public List<int> MorningPeriods { get; set; } = new();

    [JsonPropertyName("afterLunchPeriods")]
    public List<int> AfterLunchPeriods { get; set; } = new();
}

public class PeriodInfo
{
    [JsonPropertyName("period")]
    public int Period { get; set; }

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Teaching";
}

public class BreakInfo
{
    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;
}
