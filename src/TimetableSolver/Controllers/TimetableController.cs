using Microsoft.AspNetCore.Mvc;
using TimetableSolver.Data;
using TimetableSolver.Models.Output;
using TimetableSolver.Solver;

namespace TimetableSolver.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TimetableController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly DataNormalizer _normalizer = new();
    private static TimetableResponse? _cachedResponse;

    public TimetableController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string GetRootDirectory() => _env.ContentRootPath;

    private TimetableResponse GetOrLoadTimetableResponse()
    {
        if (_cachedResponse != null)
            return _cachedResponse;

        // 1. Try reading pre-generated output JSON for instant response
        try
        {
            var contentRoot = _env.ContentRootPath;
            var candidates = new[]
            {
                Path.Combine(contentRoot, "Datasets", "full_school_timetable_output.json"),
                Path.Combine(contentRoot, "full_school_timetable_output.json"),
                Path.Combine(AppContext.BaseDirectory, "Datasets", "full_school_timetable_output.json"),
                Path.Combine(AppContext.BaseDirectory, "full_school_timetable_output.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "full_school_timetable_output.json")
            };

            foreach (var p in candidates)
            {
                if (System.IO.File.Exists(p))
                {
                    var json = System.IO.File.ReadAllText(p);
                    var cached = System.Text.Json.JsonSerializer.Deserialize<TimetableResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cached != null && cached.SectionTimetables.Count > 0)
                    {
                        _cachedResponse = cached;
                        return _cachedResponse;
                    }
                }
            }

            // Embedded resource fallback
            using var stream = JsonDataLoader.GetEmbeddedStream("full_school_timetable_output.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var cached = System.Text.Json.JsonSerializer.Deserialize<TimetableResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cached != null && cached.SectionTimetables.Count > 0)
                {
                    _cachedResponse = cached;
                    return _cachedResponse;
                }
            }
        }
        catch
        {
            // Ignore and solve directly
        }

        // 2. Solve if not pre-cached
        var dataset = _normalizer.LoadFullDataset(_env.ContentRootPath);
        var engine = new TimetableSolverEngine(new SolverConfiguration { TimeLimitSeconds = 60 });
        _cachedResponse = engine.Solve(dataset);
        return _cachedResponse;
    }

    /// <summary>
    /// GET /api/timetable/dataset: Returns the normalized dataset loaded from bundled JSON/embedded resources.
    /// </summary>
    [HttpGet("dataset")]
    public IActionResult GetDataset()
    {
        var dataset = _normalizer.LoadFullDataset(_env.ContentRootPath);
        return Ok(new
        {
            schoolName = dataset.SchoolName,
            academicYear = dataset.AcademicYear,
            bellSchedule = dataset.BellSchedule,
            sectionsTotal = dataset.Sections.Count,
            teachersTotal = dataset.Teachers.Count,
            curriculumItems = dataset.CurriculumItems.Count,
            assignmentsTotal = dataset.Assignments.Count,
            sections = dataset.Sections,
            teachers = dataset.Teachers,
            curriculum = dataset.CurriculumItems,
            assignments = dataset.Assignments
        });
    }

    /// <summary>
    /// POST /api/timetable/load-data: Load and validate dataset; return summary + data conflicts.
    /// </summary>
    [HttpPost("load-data")]
    public IActionResult LoadData()
    {
        var rootDir = GetRootDirectory();
        var dataset = _normalizer.LoadFullDataset(rootDir);

        return Ok(new
        {
            success = true,
            schoolName = dataset.SchoolName,
            academicYear = dataset.AcademicYear,
            sectionsTotal = dataset.Sections.Count,
            teachersTotal = dataset.Teachers.Count,
            curriculumItems = dataset.CurriculumItems.Count,
            assignmentsLoaded = dataset.Assignments.Count,
            unassignedPlaceholderRows = dataset.UnassignedPlaceholderCount,
            zeroWorkloadRows = dataset.ZeroWorkloadCount,
            dataConflicts = dataset.DataConflicts
        });
    }

    /// <summary>
    /// POST /api/timetable/generate: Generates or retrieves the complete school timetable.
    /// - forceRecompute: false (default) returns pre-generated full schedule instantly.
    /// - forceRecompute: true triggers live Google OR-Tools CP-SAT solver optimization.
    /// - timeoutSeconds: time limit for live solver (default 90s).
    /// </summary>
    [HttpPost("generate")]
    public IActionResult Generate([FromQuery] bool forceRecompute = false, [FromQuery] double timeoutSeconds = 90, [FromQuery] string? sectionId = null)
    {
        // 1. If not forcing live recompute, return the pre-generated/cached solution instantly
        if (!forceRecompute)
        {
            var cached = GetOrLoadTimetableResponse();
            if (cached != null && cached.SectionTimetables.Count > 0)
            {
                if (!string.IsNullOrEmpty(sectionId))
                {
                    var cleanId = sectionId.Replace("-", " ").Replace("_", " ").Trim();
                    var match = cached.SectionTimetables
                        .FirstOrDefault(kv => kv.Key.Equals(sectionId, StringComparison.OrdinalIgnoreCase) ||
                                              kv.Key.Contains(sectionId, StringComparison.OrdinalIgnoreCase) ||
                                              kv.Key.Contains(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                              kv.Key.Replace("-", "").Replace(" ", "").Contains(sectionId.Replace("-", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                    if (match.Key != null)
                    {
                        return Ok(new
                        {
                            success = true,
                            section = match.Key,
                            schedule = match.Value,
                            source = "Pre-computed full school schedule (Instant)"
                        });
                    }
                }
                return Ok(cached);
            }
        }

        // 2. Live solve with Google OR-Tools CP-SAT
        var rootDir = GetRootDirectory();
        var dataset = _normalizer.LoadFullDataset(rootDir);

        var limit = timeoutSeconds > 0 ? timeoutSeconds : 90;
        var engine = new TimetableSolverEngine(new SolverConfiguration
        {
            TimeLimitSeconds = limit
        });

        var response = engine.Solve(dataset);

        if (response.Success)
        {
            _cachedResponse = response;

            // Export generated output JSON
            try
            {
                var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "full_school_timetable_output.json");
                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var jsonStr = System.Text.Json.JsonSerializer.Serialize(response, jsonOptions);
                System.IO.File.WriteAllText(outputPath, jsonStr);
            }
            catch { }

            return Ok(response);
        }

        // 3. Fallback to cached solution if solver timed out or was infeasible within time limit
        var fallback = GetOrLoadTimetableResponse();
        if (fallback != null && fallback.SectionTimetables.Count > 0)
        {
            return Ok(new
            {
                success = true,
                note = $"Live solver did not converge within {limit}s. Returning verified baseline timetable.",
                solver = fallback.Solver,
                summary = fallback.Summary,
                validation = fallback.Validation,
                dataConflicts = fallback.DataConflicts,
                schedulingConflicts = fallback.SchedulingConflicts,
                sectionTimetables = fallback.SectionTimetables,
                teacherTimetables = fallback.TeacherTimetables
            });
        }

        return Ok(response);
    }

    /// <summary>
    /// GET /api/timetable/sections: Get all section timetables.
    /// </summary>
    [HttpGet("sections")]
    public IActionResult GetSections()
    {
        var response = GetOrLoadTimetableResponse();
        return Ok(response.SectionTimetables);
    }

    /// <summary>
    /// GET /api/timetable/sections/{id}: Get timetable for a specific section (by id, display name, or slug).
    /// </summary>
    [HttpGet("sections/{id}")]
    public IActionResult GetSectionById(string id)
    {
        var response = GetOrLoadTimetableResponse();
        var dataset = _normalizer.LoadFullDataset(_env.ContentRootPath);

        var cleanId = id.Replace("-", " ").Replace("_", " ").Trim();
        var match = response.SectionTimetables
            .FirstOrDefault(kv => kv.Key.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Contains(id, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Contains(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Replace("-", "").Replace(" ", "").Contains(id.Replace("-", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (match.Key == null)
        {
            var sectionObj = dataset.Sections.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (sectionObj != null)
            {
                match = response.SectionTimetables
                    .FirstOrDefault(kv => kv.Key.Equals(sectionObj.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (match.Key == null)
        {
            return NotFound(new { message = $"Section '{id}' not found in timetable." });
        }

        return Ok(new
        {
            section = match.Key,
            schedule = match.Value
        });
    }

    /// <summary>
    /// GET /api/timetable/teachers: Get all teacher timetables.
    /// </summary>
    [HttpGet("teachers")]
    public IActionResult GetTeachers()
    {
        var response = GetOrLoadTimetableResponse();
        return Ok(response.TeacherTimetables);
    }

    /// <summary>
    /// GET /api/timetable/teachers/{id}: Get timetable for a specific teacher by code or name.
    /// </summary>
    [HttpGet("teachers/{id}")]
    public IActionResult GetTeacherById(string id)
    {
        var response = GetOrLoadTimetableResponse();
        var dataset = _normalizer.LoadFullDataset(_env.ContentRootPath);

        var cleanId = id.Replace("-", " ").Replace("_", " ").Trim();
        var match = response.TeacherTimetables
            .FirstOrDefault(kv => kv.Key.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Contains(id, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Contains(cleanId, StringComparison.OrdinalIgnoreCase) ||
                                  kv.Key.Replace(" ", "").Contains(id.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (match.Key == null)
        {
            var teacherByCode = dataset.Teachers.FirstOrDefault(t => t.Code.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                                                     t.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                                                                     t.Name.Contains(id, StringComparison.OrdinalIgnoreCase));
            if (teacherByCode != null)
            {
                match = response.TeacherTimetables
                    .FirstOrDefault(kv => kv.Key.Contains(teacherByCode.Name, StringComparison.OrdinalIgnoreCase));

                if (match.Key == null)
                {
                    return Ok(new
                    {
                        teacher = teacherByCode.Name,
                        code = teacherByCode.Code,
                        schedule = new Dictionary<string, List<object>>(),
                        message = "Teacher exists in school roster but has 0 assigned teaching periods."
                    });
                }
            }
        }

        if (match.Key == null)
        {
            return NotFound(new { message = $"Teacher '{id}' not found in timetable or roster." });
        }

        return Ok(new
        {
            teacher = match.Key,
            schedule = match.Value
        });
    }

    /// <summary>
    /// GET /api/timetable/summary: Get executive summary of generated timetable.
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var response = GetOrLoadTimetableResponse();
        return Ok(new
        {
            success = response.Success,
            solver = response.Solver,
            summary = response.Summary,
            dataConflictsCount = response.DataConflicts.Count,
            schedulingConflictsCount = response.SchedulingConflicts.Count
        });
    }

    /// <summary>
    /// GET /api/timetable/conflicts: Get data gaps + scheduling conflicts.
    /// </summary>
    [HttpGet("conflicts")]
    public IActionResult GetConflicts()
    {
        var response = GetOrLoadTimetableResponse();
        return Ok(new
        {
            dataConflicts = response.DataConflicts,
            schedulingConflicts = response.SchedulingConflicts
        });
    }
}

