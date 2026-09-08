namespace TimetableSolver.Solver;

public class SolverConfiguration
{
    public double TimeLimitSeconds { get; set; } = 300;
    public int NumSearchWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount);
    public bool LogSearchProgress { get; set; } = false;
}
