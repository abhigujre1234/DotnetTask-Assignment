using System.Text.Json;
using TimetableSolver.Data;
using TimetableSolver.Solver;

var builder = WebApplication.CreateBuilder(args);

// Check if CLI mode was requested
if (args.Any(a => a.Equals("--cli", StringComparison.OrdinalIgnoreCase) ||
                  a.Equals("-c", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("===============================================================================");
    Console.WriteLine("  RDPL — FULL SCHOOL CONSTRAINT-BASED TIMETABLE SOLVER (GOOGLE OR-TOOLS CP-SAT)");
    Console.WriteLine("===============================================================================\n");

    var normalizer = new DataNormalizer();
    var dataset = normalizer.LoadFullDataset();

    Console.WriteLine($"[1/4] Loaded dataset: {dataset.SchoolName} (Academic Year: {dataset.AcademicYear})");

    Console.WriteLine($"      - Sections Loaded: {dataset.Sections.Count}");
    Console.WriteLine($"      - Teachers Loaded: {dataset.Teachers.Count}");
    Console.WriteLine($"      - Weekly Capacity per section: {dataset.BellSchedule.WeeklyTeachingCapacity} periods");
    Console.WriteLine($"      - Assignments Loaded: {dataset.Assignments.Count}");
    Console.WriteLine($"      - UNASSIGNED-TT Placeholder Rows: {dataset.UnassignedPlaceholderCount}");
    Console.WriteLine($"      - Zero-Workload Rows: {dataset.ZeroWorkloadCount}\n");

    Console.WriteLine("[2/4] Building CP-SAT Constraint Model...");
    Console.WriteLine("      - Enforcing Hard Constraints (H1-H10, G1-G2, L1-L3, B1-B4, T2, DATA-1)");
    Console.WriteLine("      - Enforcing Soft Preferences (PR-MATH, DB-RULEs)\n");

    Console.WriteLine("[3/4] Solving with Google OR-Tools CP-SAT...");
    var engine = new TimetableSolverEngine(new SolverConfiguration
    {
        TimeLimitSeconds = 300,
        LogSearchProgress = false
    });

    var response = engine.Solve(dataset);

    Console.WriteLine($"\n[4/4] Solver Run Completed!");
    Console.WriteLine($"      - Status: {response.Solver.Status}");
    Console.WriteLine($"      - Wall Time: {response.Solver.WallTimeMs} ms");
    Console.WriteLine($"      - Objective Value: {response.Solver.ObjectiveValue}");
    Console.WriteLine($"      - Sections Scheduled: {response.Summary.SectionsScheduled} / {response.Summary.SectionsTotal}");
    Console.WriteLine($"      - Total Teaching Slots Scheduled: {response.Summary.TotalSlotsScheduled}");
    Console.WriteLine($"      - Teachers Involved: {response.Summary.TeachersInvolved}");
    Console.WriteLine($"      - Data Conflicts Logged: {response.DataConflicts.Count}");

    // Export output JSON
    var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "full_school_timetable_output.json");
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    var jsonStr = JsonSerializer.Serialize(response, jsonOptions);
    File.WriteAllText(outputPath, jsonStr);

    Console.WriteLine($"\n>> Timetable Output JSON successfully written to:\n   {outputPath}\n");

    return;
}

// Otherwise, run Web API Server
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "School Timetable Solver API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

Console.WriteLine("School Timetable Solver API starting on http://localhost:5000 ...");
app.Run();
