using System.Diagnostics;
using Domain.Exceptions;

namespace Domain.Game;

[DebuggerDisplay("{Side1}, {Side2}")]
public class Domino
{
    private const int MinimumSideValue = 0;
    private const int MaximumSideValue = 6;

    public int Side1 { get; }
    public int Side2 { get; }

    public Domino(int side1, int side2)
    {
        if (side1 < MinimumSideValue || side1 > MaximumSideValue || side2 < MinimumSideValue || side2 > MaximumSideValue)
        {
            throw new DominoValueException("The value for the Domino is out of bound.");
        }

        Side1 = side1;
        Side2 = side2;
    }

    public int SumOfSides() => Side1 + Side2;

    public override bool Equals(object? obj)
    {
        return obj is Domino domino && domino.Side1 == Side1 && domino.Side2 == Side2;
    }

    public bool IsDouble()
    {
        return Side1 == Side2;
    }
}
