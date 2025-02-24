using Application.Interfaces;

namespace CommonTests.Fixtures;

public class SeedGeneratorMock : ISeedGenerator
{
    public int Seed { get; set; }

    public int GetNewSeed()
    {
        return Seed;
    }
}
