using Application.Interfaces;

namespace CommonTests.Fixtures;

public class RandomGeneratorMock : IRandomGenerator
{
    private List<int> randomsNext = [1, 2, 3, 4, 5, 6, 7];
    private int count;

    public int Next(int minValue, int maxValue)
    {
        return randomsNext[count++ % randomsNext.Count];
    }

    public void SetRandomsNext(List<int> randoms)
    {
        randomsNext = randoms;
    }
}