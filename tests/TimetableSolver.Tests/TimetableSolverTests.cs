using TimetableSolver.Data;
using TimetableSolver.Models.Domain;
using TimetableSolver.Solver;
using Xunit;

namespace TimetableSolver.Tests;

public class TimetableSolverTests
{
    private string GetWorkspaceDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "CLASS_WISE_SUBJECTS.md")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? Directory.GetCurrentDirectory();
    }

    [Fact]
    public void Test_EmbeddedDataset_LoadsDirectlyWithoutFilePath()
    {
        var normalizer = new DataNormalizer();
        var dataset = normalizer.LoadFullDataset();

        Assert.NotNull(dataset);
        Assert.Equal(41, dataset.Sections.Count);
        Assert.Equal(52, dataset.BellSchedule.WeeklyTeachingCapacity);
        Assert.NotEmpty(dataset.Teachers);
        Assert.NotEmpty(dataset.Assignments);
    }

    [Fact]
    public void Test1_BellSchedule_And_Sections_LoadedCorrectly()
    {
        var jsonLoader = new JsonDataLoader();

        var bellSchedule = jsonLoader.LoadBellSchedule();
        Assert.NotNull(bellSchedule);
        Assert.Equal(52, bellSchedule.WeeklyTeachingCapacity);
        Assert.Equal(6, bellSchedule.WorkingDays.Count);

        var sections = jsonLoader.LoadSections();
        Assert.NotNull(sections);
        Assert.Equal(41, sections.Count);

        var teachers = jsonLoader.LoadTeachers();
        Assert.NotNull(teachers);
        Assert.True(teachers.Count >= 44);

        var curriculum = jsonLoader.LoadCurriculum();
        Assert.NotNull(curriculum);
        Assert.NotEmpty(curriculum);
    }

    [Fact]
    public void Test2_MarkdownParser_ClassWiseSubjects_And_TeacherAssignments()
    {
        var workspaceDir = GetWorkspaceDirectory();
        var parser = new MarkdownParser();

        var subjects = parser.ParseClassWiseSubjects(Path.Combine(workspaceDir, "CLASS_WISE_SUBJECTS.md"));
        Assert.NotEmpty(subjects);

        // Every class in CLASS_WISE_SUBJECTS should have 52 ppw
        var classGroups = subjects.GroupBy(s => s.ClassName);
        foreach (var group in classGroups)
        {
            var totalPpw = group.Sum(s => s.PeriodsPerWeek);
            Assert.Equal(52, totalPpw);
        }

        var (teachers, assignments) = parser.ParseTeacherAssignments(Path.Combine(workspaceDir, "TEACHER_CLASS_ASSIGNMENTS.md"));
        Assert.NotEmpty(teachers);
        Assert.True(teachers.Count >= 44);
    }

    [Fact]
    public void Test3_Solve_SampleSchool_FindsFeasibleSolution()
    {
        var workspaceDir = GetWorkspaceDirectory();
        var normalizer = new DataNormalizer();
        var dataset = normalizer.LoadSampleDataset(workspaceDir);

        Assert.NotEmpty(dataset.Sections);
        Assert.NotEmpty(dataset.Assignments);

        var engine = new TimetableSolverEngine(new SolverConfiguration
        {
            TimeLimitSeconds = 30,
            LogSearchProgress = false
        });

        var response = engine.Solve(dataset);

        Assert.True(response.Success);
        Assert.True(response.Solver.Status == "OPTIMAL" || response.Solver.Status == "FEASIBLE");
        Assert.NotEmpty(response.SectionTimetables);
        Assert.NotEmpty(response.TeacherTimetables);

        // Verify that Games/Library are never in periods 1 or 2 (Rule G1)
        foreach (var (secName, days) in response.SectionTimetables)
        {
            foreach (var (day, periods) in days)
            {
                foreach (var p in periods)
                {
                    if (p.Subject.Equals("Games", StringComparison.OrdinalIgnoreCase) ||
                        p.Subject.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
                        p.Subject.Equals("Games / Library", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.NotInRange(p.Period, 1, 2);
                    }
                }
            }
        }
    }

    [Fact]
    public void Test4_Solve_FullSchool_GeneratesAll41Sections()
    {
        var workspaceDir = GetWorkspaceDirectory();
        var normalizer = new DataNormalizer();
        var dataset = normalizer.LoadFullDataset(workspaceDir);

        Assert.Equal(41, dataset.Sections.Count);
        Assert.NotEmpty(dataset.Assignments);

        var engine = new TimetableSolverEngine(new SolverConfiguration
        {
            TimeLimitSeconds = 90,
            LogSearchProgress = false
        });

        var response = engine.Solve(dataset);

        Assert.True(response.Success);
        Assert.Equal(41, response.Summary.SectionsScheduled);
        Assert.Equal(2132, response.Summary.TotalSlotsScheduled); // 41 * 52 = 2132 slots

        // Verify No Teacher Double-Booking (Rule H1)
        var teacherSlotBookings = new HashSet<string>();
        foreach (var (teacherName, dayMap) in response.TeacherTimetables)
        {
            foreach (var (day, periods) in dayMap)
            {
                foreach (var p in periods)
                {
                    var bookingKey = $"{teacherName}_{day}_{p.Period}";
                    Assert.True(teacherSlotBookings.Add(bookingKey), $"Teacher double-booking detected for {teacherName} on {day} Period {p.Period}");
                }
            }
        }

        // Verify Rule G2: Class 11 and 12 never have both Games and Library on the same day
        foreach (var (secName, dayMap) in response.SectionTimetables)
        {
            if (secName.Contains("11") || secName.Contains("12"))
            {
                foreach (var (day, periods) in dayMap)
                {
                    var hasGames = periods.Any(p => p.Subject.Equals("Games", StringComparison.OrdinalIgnoreCase));
                    var hasLibrary = periods.Any(p => p.Subject.Equals("Library", StringComparison.OrdinalIgnoreCase));
                    Assert.False(hasGames && hasLibrary, $"Class 11/12 section {secName} has both Games and Library on {day}");
                }
            }
        }
    }
}
