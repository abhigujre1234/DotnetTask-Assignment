using TimetableSolver.Models.Domain;

namespace TimetableSolver.Data;

public interface IDataLoader
{
    NormalizedSchoolDataset LoadFullDataset(string? rootDirectory = null);
    NormalizedSchoolDataset LoadSampleDataset(string? rootDirectory = null);
}

