using Application.Interfaces;

namespace Infrastructure.Services;

public class RandomGenerator : IRandomGenerator
{
    private readonly Random random = new();

    public int Next(int minValue, int maxValue)
    {
        return random.Next(minValue, maxValue);
    }
}