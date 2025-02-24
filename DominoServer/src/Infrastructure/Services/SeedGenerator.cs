using Application.Interfaces;

namespace Infrastructure.Services;

public class SeedGenerator : ISeedGenerator
{
    private readonly Random rand = new();
    public int GetNewSeed()
    {
        return rand.Next();
    }
}

