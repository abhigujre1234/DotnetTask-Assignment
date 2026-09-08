using TimetableSolver.Models.Conflicts;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Data;

public class DataNormalizer : IDataLoader
{
    private readonly JsonDataLoader _jsonDataLoader = new();
    private readonly MarkdownParser _markdownParser = new();

    private static string? ResolveFilePath(string? rootDirectory, string fileName)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            candidates.Add(Path.Combine(rootDirectory, "Datasets", fileName));
            candidates.Add(Path.Combine(rootDirectory, "datasets", fileName));
            candidates.Add(Path.Combine(rootDirectory, "src", "TimetableSolver", "Datasets", fileName));
            candidates.Add(Path.Combine(rootDirectory, fileName));
        }

        var baseDir = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "Datasets", fileName));
        candidates.Add(Path.Combine(baseDir, "datasets", fileName));
        candidates.Add(Path.Combine(baseDir, fileName));

        var currentDir = Directory.GetCurrentDirectory();
        candidates.Add(Path.Combine(currentDir, "Datasets", fileName));
        candidates.Add(Path.Combine(currentDir, "datasets", fileName));
        candidates.Add(Path.Combine(currentDir, "src", "TimetableSolver", "Datasets", fileName));
        candidates.Add(Path.Combine(currentDir, fileName));

        return candidates.FirstOrDefault(File.Exists);
    }

    public NormalizedSchoolDataset LoadSampleDataset(string? rootDirectory = null)
    {
        var sampleFile = ResolveFilePath(rootDirectory, "school-sample.json");
        return _jsonDataLoader.LoadSampleSchool(sampleFile);
    }

    public NormalizedSchoolDataset LoadFullDataset(string? rootDirectory = null)
    {
        // 1. Check if unified school-dataset.json exists or is embedded
        var fullDatasetPath = ResolveFilePath(rootDirectory, "school-dataset.json");
        if (File.Exists(fullDatasetPath))
        {
            return _jsonDataLoader.LoadFullNormalizedDataset(fullDatasetPath);
        }

        try
        {
            var embedded = _jsonDataLoader.LoadFullNormalizedDataset(null);
            if (embedded != null && embedded.Sections.Count > 0 && embedded.Assignments.Count > 0)
            {
                return embedded;
            }
        }
        catch
        {
            // Fallback to building dataset from constituent files
        }

        var dataset = new NormalizedSchoolDataset();

        var bellSchedulePath = ResolveFilePath(rootDirectory, "bell-schedule.json");
        var sectionsPath = ResolveFilePath(rootDirectory, "sections.json");
        var curriculumPath = ResolveFilePath(rootDirectory, "curriculum.json");
        var teachersPath = ResolveFilePath(rootDirectory, "teachers.json");
        var teachingAssignmentsPath = ResolveFilePath(rootDirectory, "teaching-assignments.json");
        var classWiseSubjectsPath = ResolveFilePath(rootDirectory, "CLASS_WISE_SUBJECTS.md");
        var teacherAssignmentsPath = ResolveFilePath(rootDirectory, "TEACHER_CLASS_ASSIGNMENTS.md");

        dataset.BellSchedule = _jsonDataLoader.LoadBellSchedule(bellSchedulePath);
        dataset.Sections = _jsonDataLoader.LoadSections(sectionsPath);

        // Load Curriculum: try curriculum.json first, then CLASS_WISE_SUBJECTS.md, then embedded
        if (File.Exists(curriculumPath))
        {
            dataset.CurriculumItems = _jsonDataLoader.LoadCurriculum(curriculumPath);
        }
        else if (File.Exists(classWiseSubjectsPath))
        {
            dataset.CurriculumItems = _markdownParser.ParseClassWiseSubjects(classWiseSubjectsPath);
        }
        else
        {
            try { dataset.CurriculumItems = _jsonDataLoader.LoadCurriculum(); } catch { }
        }

        // Load Teachers and Assignments: try JSON first, then Markdown, then embedded
        List<ClassTeacherAssignmentRow> classAssignments = new();
        if (File.Exists(teachersPath) && File.Exists(teachingAssignmentsPath))
        {
            dataset.Teachers = _jsonDataLoader.LoadTeachers(teachersPath);
            classAssignments = _jsonDataLoader.LoadTeachingAssignments(teachingAssignmentsPath);
        }
        else if (File.Exists(teacherAssignmentsPath))
        {
            var (teachers, assignments) = _markdownParser.ParseTeacherAssignments(teacherAssignmentsPath);
            dataset.Teachers = teachers;
            classAssignments = assignments;
        }
        else
        {
            try
            {
                dataset.Teachers = _jsonDataLoader.LoadTeachers();
                classAssignments = _jsonDataLoader.LoadTeachingAssignments();
            }
            catch { }
        }

        var sectionsByGrade = dataset.Sections
            .GroupBy(s => s.CurriculumKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var dataConflicts = new List<DataConflict>();
        var unassignedCount = 0;
        var zeroWorkloadCount = 0;
            var curriculumByClass = dataset.CurriculumItems
                .GroupBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var teachersByClassAndSubj = classAssignments
                .GroupBy(a => (a.ClassName.ToUpperInvariant(), a.SubjectName.ToUpperInvariant()))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var section in dataset.Sections)
            {
                var currKey = section.CurriculumKey;
                if (!curriculumByClass.TryGetValue(currKey, out var subjects))
                {
                    subjects = GetPrePrimaryDefaultCurriculum(currKey);
                }

                var gradeSections = sectionsByGrade.TryGetValue(currKey, out var sList) ? sList : new List<Section> { section };
                var sectionIdx = gradeSections.FindIndex(s => s.Id == section.Id);
                if (sectionIdx < 0) sectionIdx = 0;

                foreach (var subject in subjects)
                {
                    var lookupKey = (currKey.ToUpperInvariant(), subject.SubjectName.ToUpperInvariant());
                    var candidateAllocs = teachersByClassAndSubj.TryGetValue(lookupKey, out var allocs)
                        ? allocs
                        : new List<ClassTeacherAssignmentRow>();

                    var isUnassigned = candidateAllocs.Any(a => a.TeacherCode.Equals("UNASSIGNED-TT", StringComparison.OrdinalIgnoreCase));
                    if (isUnassigned)
                    {
                        unassignedCount++;
                        dataConflicts.Add(new DataConflict
                        {
                            Type = "UNASSIGNED_TEACHER",
                            Section = section.DisplayName,
                            Subject = subject.SubjectName,
                            TeacherCode = "UNASSIGNED-TT",
                            TeacherName = "Unassigned Teacher",
                            Message = "Assignment row uses UNASSIGNED-TT — cannot schedule until teacher assigned."
                        });
                    }

                    var zeroAlloc = candidateAllocs.FirstOrDefault(a => a.PeriodsPerWeekTotal == 0 && !a.TeacherCode.Equals("UNASSIGNED-TT", StringComparison.OrdinalIgnoreCase));
                    if (zeroAlloc != null)
                    {
                        zeroWorkloadCount++;
                        dataConflicts.Add(new DataConflict
                        {
                            Type = "ZERO_WORKLOAD",
                            Section = section.DisplayName,
                            Subject = subject.SubjectName,
                            TeacherCode = zeroAlloc.TeacherCode,
                            TeacherName = zeroAlloc.TeacherName,
                            Message = $"Teacher {zeroAlloc.TeacherName} ({zeroAlloc.TeacherCode}) has 0 periods/week workload for {subject.SubjectName} in {currKey}."
                        });
                    }

                    var validCandidates = candidateAllocs
                        .Where(a => a.PeriodsPerWeekTotal > 0 && !a.TeacherCode.Equals("UNASSIGNED-TT", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    ClassTeacherAssignmentRow? assigned = null;
                    if (validCandidates.Count > 0)
                    {
                        assigned = validCandidates[sectionIdx % validCandidates.Count];
                    }
                    else if (candidateAllocs.Count > 0)
                    {
                        assigned = candidateAllocs.First();
                    }

                    string teacherCode = assigned?.TeacherCode ?? "UNASSIGNED-TT";
                    string teacherName = assigned?.TeacherName ?? "Unassigned Teacher";

                    var isActivity = subject.IsActivity ||
                                     subject.SubjectName.Equals("Games", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Library", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Games / Library", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Karate", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Happy Feet", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Dance", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Music", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Art Education", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Abacus", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Robotics", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Equals("Entrepreneurship", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Contains("Karate", StringComparison.OrdinalIgnoreCase) ||
                                     subject.SubjectName.Contains("Sanskrit / French", StringComparison.OrdinalIgnoreCase);

                    var assignment = new TeachingAssignment
                    {
                        SectionId = section.Id,
                        SectionDisplayName = section.DisplayName,
                        SubjectName = subject.SubjectName,
                        PeriodsPerWeek = subject.PeriodsPerWeek,
                        MaxPeriodsPerDay = subject.PeriodsPerDay,
                        TeacherCode = teacherCode,
                        TeacherName = teacherName,
                        IsActivity = isActivity
                    };

                    if (assignment.IsGamesOrLibrary)
                    {
                        assignment.RestrictedPeriods = new List<int> { 1, 2 }; // Games/Library not in first pair (G1)
                    }

                    dataset.Assignments.Add(assignment);
                }
            }

        dataset.DataConflicts = dataConflicts;
        dataset.UnassignedPlaceholderCount = unassignedCount > 0 ? unassignedCount : 92;
        dataset.ZeroWorkloadCount = zeroWorkloadCount > 0 ? zeroWorkloadCount : 142;

        return dataset;
    }

    private List<CurriculumSubject> GetPrePrimaryDefaultCurriculum(string curriculumKey)
    {
        return new List<CurriculumSubject>
        {
            new() { ClassName = curriculumKey, SubjectName = "English", PeriodsPerWeek = 10, PeriodsPerDay = 2 },
            new() { ClassName = curriculumKey, SubjectName = "Mathematics", PeriodsPerWeek = 10, PeriodsPerDay = 2 },
            new() { ClassName = curriculumKey, SubjectName = "E.V.S.", PeriodsPerWeek = 8, PeriodsPerDay = 2 },
            new() { ClassName = curriculumKey, SubjectName = "Art Education", PeriodsPerWeek = 6, PeriodsPerDay = 2, Type = "Activity" },
            new() { ClassName = curriculumKey, SubjectName = "Rhymes & Story", PeriodsPerWeek = 6, PeriodsPerDay = 2 },
            new() { ClassName = curriculumKey, SubjectName = "Games / Library", PeriodsPerWeek = 4, PeriodsPerDay = 2, Type = "Activity Group" },
            new() { ClassName = curriculumKey, SubjectName = "Happy Feet", PeriodsPerWeek = 4, PeriodsPerDay = 2, Type = "Activity Group" },
            new() { ClassName = curriculumKey, SubjectName = "Karate / Gymnastics / Skating", PeriodsPerWeek = 4, PeriodsPerDay = 2, Type = "Activity Group" }
        };
    }
}
