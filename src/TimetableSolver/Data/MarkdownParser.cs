using System.Text.RegularExpressions;
using TimetableSolver.Models.Domain;

namespace TimetableSolver.Data;

public class MarkdownParser
{
    public List<CurriculumSubject> ParseClassWiseSubjects(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Class-wise subjects file not found at: {filePath}");
        }

        var lines = File.ReadAllLines(filePath);
        var curriculum = new List<CurriculumSubject>();
        string currentClass = string.Empty;

        var classHeaderRegex = new Regex(@"^###\s+(Class\s+\d+|Pre\s+Nursery|Nursery|Junior\s+KG|Senior\s+KG)", RegexOptions.IgnoreCase);
        var tableRowRegex = new Regex(@"^\|\s*(Subject|Activity Group|Activity)\s*\|\s*([^\|]+)\|\s*(\d+)\s*\|\s*(\d+)\s*\|", RegexOptions.IgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            var headerMatch = classHeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                currentClass = headerMatch.Groups[1].Value.Trim();
                continue;
            }

            if (string.IsNullOrEmpty(currentClass))
                continue;

            var match = tableRowRegex.Match(line);
            if (match.Success)
            {
                var type = match.Groups[1].Value.Trim();
                var subjName = match.Groups[2].Value.Trim();
                var ppd = int.Parse(match.Groups[3].Value.Trim());
                var ppw = int.Parse(match.Groups[4].Value.Trim());

                curriculum.Add(new CurriculumSubject
                {
                    ClassName = currentClass,
                    SubjectName = subjName,
                    Type = type,
                    PeriodsPerDay = ppd,
                    PeriodsPerWeek = ppw
                });
            }
        }

        return curriculum;
    }

    public (List<Teacher> Teachers, List<ClassTeacherAssignmentRow> Assignments) ParseTeacherAssignments(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Teacher assignments file not found at: {filePath}");
        }

        var lines = File.ReadAllLines(filePath);
        var teachers = new List<Teacher>();
        var assignments = new List<ClassTeacherAssignmentRow>();

        // 1. Parse Teacher Roster from Section 2
        var inRoster = false;
        var inDetail = false;
        string currentTeacherCode = string.Empty;
        string currentTeacherName = string.Empty;

        var teacherDetailHeaderRegex = new Regex(@"^###\s+`?([^`—\s]+)`?\s*—\s*(.+)$");
        var rosterRowRegex = new Regex(@"^\|\s*\d+\s*\|\s*`?([^`\|]+)`?\s*\|\s*([^\|]+)\|\s*([^\|]+)\|\s*(\d+)\s*\|\s*(\d+)\s*\|");
        var detailRowRegex = new Regex(@"^\|\s*(Class\s+\d+|Pre\s+Nursery|Nursery|Junior\s+KG|Senior\s+KG)\s*\|\s*(Subject|Activity)\s*\|\s*([^\|]+)\|\s*(\d+)\s*\|");

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("## 2. Teacher Roster", StringComparison.OrdinalIgnoreCase))
            {
                inRoster = true;
                inDetail = false;
                continue;
            }

            if (line.StartsWith("## 3.", StringComparison.OrdinalIgnoreCase))
            {
                inRoster = false;
                continue;
            }

            if (line.StartsWith("## 4. Teacher Assignments by Class", StringComparison.OrdinalIgnoreCase))
            {
                inDetail = true;
                inRoster = false;
                continue;
            }

            if (line.StartsWith("## 5.", StringComparison.OrdinalIgnoreCase))
            {
                inDetail = false;
                break;
            }

            if (inRoster)
            {
                var rMatch = rosterRowRegex.Match(line);
                if (rMatch.Success)
                {
                    var code = rMatch.Groups[1].Value.Trim();
                    var name = rMatch.Groups[2].Value.Trim();
                    var classesStr = rMatch.Groups[3].Value.Trim();
                    var ppw = int.Parse(rMatch.Groups[5].Value.Trim());

                    var assignedClasses = classesStr
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .ToList();

                    teachers.Add(new Teacher
                    {
                        Id = code,
                        Code = code,
                        Name = name,
                        MaxPeriodsPerWeek = ppw > 0 ? ppw : null,
                        AssignedClasses = assignedClasses
                    });
                }
            }

            if (inDetail)
            {
                var tMatch = teacherDetailHeaderRegex.Match(line);
                if (tMatch.Success)
                {
                    currentTeacherCode = tMatch.Groups[1].Value.Trim();
                    currentTeacherName = tMatch.Groups[2].Value.Trim();
                    continue;
                }

                var dMatch = detailRowRegex.Match(line);
                if (dMatch.Success && !string.IsNullOrEmpty(currentTeacherCode))
                {
                    var className = dMatch.Groups[1].Value.Trim();
                    var type = dMatch.Groups[2].Value.Trim();
                    var subjectName = dMatch.Groups[3].Value.Trim();
                    var ppw = int.Parse(dMatch.Groups[4].Value.Trim());

                    assignments.Add(new ClassTeacherAssignmentRow
                    {
                        TeacherCode = currentTeacherCode,
                        TeacherName = currentTeacherName,
                        ClassName = className,
                        SubjectName = subjectName,
                        PeriodsPerWeekTotal = ppw
                    });
                }
            }
        }

        return (teachers, assignments);
    }
}

public class ClassTeacherAssignmentRow
{
    public string TeacherCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int PeriodsPerWeekTotal { get; set; }
}
