using System.Reflection;
using System.Text.Json;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Data;

public class JsonDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static Stream? GetEmbeddedStream(string fileName)
    {
        var assembly = typeof(JsonDataLoader).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        return resourceName != null ? assembly.GetManifestResourceStream(resourceName) : null;
    }

    public static string ReadJsonContent(string? filePath, string fallbackResourceName)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        using var stream = GetEmbeddedStream(fallbackResourceName);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        throw new FileNotFoundException($"Dataset '{fallbackResourceName}' could not be found at path '{filePath}' or as an embedded resource.");
    }

    public BellSchedule LoadBellSchedule(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "bell-schedule.json");
        var schedule = JsonSerializer.Deserialize<BellSchedule>(json, JsonOptions);
        return schedule ?? new BellSchedule();
    }

    public List<Section> LoadSections(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "sections.json");
        var root = JsonSerializer.Deserialize<SectionsRoot>(json, JsonOptions);
        return root?.Sections ?? new List<Section>();
    }

    public List<CurriculumSubject> LoadCurriculum(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "curriculum.json");
        return JsonSerializer.Deserialize<List<CurriculumSubject>>(json, JsonOptions) ?? new List<CurriculumSubject>();
    }

    public List<Teacher> LoadTeachers(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "teachers.json");
        return JsonSerializer.Deserialize<List<Teacher>>(json, JsonOptions) ?? new List<Teacher>();
    }

    public List<ClassTeacherAssignmentRow> LoadTeachingAssignments(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "teaching-assignments.json");
        return JsonSerializer.Deserialize<List<ClassTeacherAssignmentRow>>(json, JsonOptions) ?? new List<ClassTeacherAssignmentRow>();
    }

    public NormalizedSchoolDataset LoadFullNormalizedDataset(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "school-dataset.json");
        return JsonSerializer.Deserialize<NormalizedSchoolDataset>(json, JsonOptions) ?? new NormalizedSchoolDataset();
    }

    public NormalizedSchoolDataset LoadSampleSchool(string? filePath = null)
    {
        var json = ReadJsonContent(filePath, "school-sample.json");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dataset = new NormalizedSchoolDataset
        {
            SchoolName = root.TryGetProperty("schoolName", out var sn) ? sn.GetString() ?? "Sample School" : "Sample School",
            AcademicYear = root.TryGetProperty("academicYear", out var ay) ? ay.GetString() ?? "2026-27" : "2026-27"
        };

        // Bell schedule
        dataset.BellSchedule = new BellSchedule
        {
            WeeklyTeachingCapacity = 52,
            WorkingDays = new List<string> { Days.Monday, Days.Tuesday, Days.Wednesday, Days.Thursday, Days.Friday, Days.Saturday },
            MondayToThursday = new DayScheduleProfile
            {
                TeachingPeriodsPerDay = 8,
                MorningPeriods = new List<int> { 1, 2, 3, 4 },
                AfterLunchPeriods = new List<int> { 5, 6, 7, 8 },
                TeachingPeriods = Enumerable.Range(1, 8).Select(p => new PeriodInfo
                {
                    Period = p,
                    Slot = p <= 2 ? (p == 1 ? "1A" : "1B") : p <= 4 ? (p == 3 ? "2A" : "2B") : p <= 6 ? (p == 5 ? "3A" : "3B") : (p == 7 ? "4A" : "4B"),
                    Type = "Teaching"
                }).ToList()
            },
            FridayAndSaturday = new DayScheduleProfile
            {
                TeachingPeriodsPerDay = 10,
                MorningPeriods = new List<int> { 1, 2, 3, 4 },
                AfterLunchPeriods = new List<int> { 5, 6, 7, 8, 9, 10 },
                TeachingPeriods = Enumerable.Range(1, 10).Select(p => new PeriodInfo
                {
                    Period = p,
                    Slot = p <= 2 ? (p == 1 ? "1A" : "1B") : p <= 4 ? (p == 3 ? "2A" : "2B") : p <= 6 ? (p == 5 ? "3A" : "3B") : p <= 8 ? (p == 7 ? "4A" : "4B") : (p == 9 ? "5A" : "5B"),
                    Type = "Teaching"
                }).ToList()
            }
        };

        // Teachers
        var teachersMap = new Dictionary<string, Teacher>();
        if (root.TryGetProperty("teachers", out var teachersElem))
        {
            foreach (var t in teachersElem.EnumerateArray())
            {
                var teacher = new Teacher
                {
                    Id = t.GetProperty("id").GetString() ?? "",
                    Name = t.GetProperty("name").GetString() ?? "",
                    Code = t.GetProperty("code").GetString() ?? "",
                    MaxPeriodsPerDay = t.TryGetProperty("maxPeriodsPerDay", out var mpd) ? mpd.GetInt32() : null,
                    MaxPeriodsPerWeek = t.TryGetProperty("maxPeriodsPerWeek", out var mpw) ? mpw.GetInt32() : null
                };
                dataset.Teachers.Add(teacher);
                teachersMap[teacher.Id] = teacher;
            }
        }

        // Sections & Curriculum
        if (root.TryGetProperty("classSections", out var sectionsElem))
        {
            foreach (var s in sectionsElem.EnumerateArray())
            {
                var section = new Section
                {
                    Id = s.GetProperty("id").GetString() ?? "",
                    DisplayName = s.GetProperty("displayName").GetString() ?? "",
                    Grade = s.GetProperty("grade").GetString() ?? "",
                    SectionName = s.GetProperty("section").GetString() ?? "",
                    CurriculumKey = $"Class {s.GetProperty("grade").GetString()}",
                    Capacity = 40
                };
                dataset.Sections.Add(section);

                if (s.TryGetProperty("curriculum", out var currElem))
                {
                    foreach (var c in currElem.EnumerateArray())
                    {
                        var subjName = c.GetProperty("name").GetString() ?? "";
                        var ppw = c.GetProperty("periodsPerWeek").GetInt32();
                        var ppd = c.GetProperty("maxPeriodsPerDay").GetInt32();
                        var teacherId = c.TryGetProperty("teacherId", out var tid) ? tid.GetString() ?? "" : "";

                        teachersMap.TryGetValue(teacherId, out var assignedTeacher);

                        var isAct = subjName.Equals("Games", StringComparison.OrdinalIgnoreCase) ||
                                    subjName.Equals("Karate", StringComparison.OrdinalIgnoreCase) ||
                                    subjName.Equals("Happy Feet", StringComparison.OrdinalIgnoreCase) ||
                                    subjName.Equals("Games / Library", StringComparison.OrdinalIgnoreCase);

                        var assignment = new TeachingAssignment
                        {
                            SectionId = section.Id,
                            SectionDisplayName = section.DisplayName,
                            SubjectName = subjName,
                            PeriodsPerWeek = ppw,
                            MaxPeriodsPerDay = ppd,
                            TeacherCode = assignedTeacher?.Code ?? teacherId,
                            TeacherName = assignedTeacher?.Name ?? teacherId,
                            IsActivity = isAct
                        };

                        if (c.TryGetProperty("restrictedPeriods", out var rpElem))
                        {
                            foreach (var rp in rpElem.EnumerateArray())
                            {
                                assignment.RestrictedPeriods.Add(rp.GetInt32());
                            }
                        }

                        dataset.Assignments.Add(assignment);
                    }
                }
            }
        }

        return dataset;
    }
}
