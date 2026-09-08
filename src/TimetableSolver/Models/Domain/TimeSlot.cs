namespace TimetableSolver.Models.Domain;

public static class Days
{
    public const string Monday = "MONDAY";
    public const string Tuesday = "TUESDAY";
    public const string Wednesday = "WEDNESDAY";
    public const string Thursday = "THURSDAY";
    public const string Friday = "FRIDAY";
    public const string Saturday = "SATURDAY";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Monday, Tuesday, Wednesday, Thursday, Friday, Saturday
    };

    public static readonly IReadOnlyList<string> MondayToThursday = new[]
    {
        Monday, Tuesday, Wednesday, Thursday
    };

    public static bool IsMondayToThursday(string day) =>
        day.Equals(Monday, StringComparison.OrdinalIgnoreCase) ||
        day.Equals(Tuesday, StringComparison.OrdinalIgnoreCase) ||
        day.Equals(Wednesday, StringComparison.OrdinalIgnoreCase) ||
        day.Equals(Thursday, StringComparison.OrdinalIgnoreCase);
}

public class TimeSlot
{
    public string Day { get; set; } = string.Empty;
    public int Period { get; set; }
    public string Slot { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Type { get; set; } = "Teaching";

    public override string ToString() => $"{Day} Period {Period} ({Slot})";
}

public class SlotPair
{
    public string Day { get; set; } = string.Empty;
    public int Period1 { get; set; }
    public int Period2 { get; set; }
    public string Label1 { get; set; } = string.Empty;
    public string Label2 { get; set; } = string.Empty;

    public override string ToString() => $"{Day} Pair ({Period1}-{Period2})";
}

public class FixedSlotSpec
{
    public string Day { get; set; } = string.Empty;
    public int? Period { get; set; }
}
