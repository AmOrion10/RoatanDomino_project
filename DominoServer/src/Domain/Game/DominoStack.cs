namespace Domain.Game;

public class DominoStack
{
    private const int NumberOfDomino = 7;

    private readonly List<Domino> dominoes = [];
    private Random random;

    public bool IsLocked { get; private set; }
    public int LockedPileCount => IsLocked ? dominoes.Count : 0;
    public int ActiveBoneyardCount => IsLocked ? 0 : dominoes.Count;

    public DominoStack(int? seed = null)
    {
        random = seed is null ? new Random() : new Random(seed.Value);
        BuildStandardDoubleSixDeck();
    }

    public void Reset(int? seed = null)
    {
        if (seed.HasValue)
        {
            random = new Random(seed.Value);
        }

        IsLocked = false;
        dominoes.Clear();
        BuildStandardDoubleSixDeck();
    }

    private void BuildStandardDoubleSixDeck()
    {
        for (int i = 0; i < NumberOfDomino; i++)
        {
            for (int j = i; j < NumberOfDomino; j++)
            {
                dominoes.Add(new Domino(i, j));
            }
        }
    }

    public bool Remove(int side1, int side2)
    {
        int index = dominoes.FindIndex(d =>
            (d.Side1 == side1 && d.Side2 == side2) ||
            (d.Side1 == side2 && d.Side2 == side1));

        if (index < 0) return false;
        dominoes.RemoveAt(index);
        return true;
    }

    public void LockRemainingPile()
    {
        IsLocked = true;
    }

    public void UnlockPile()
    {
        IsLocked = false;
    }

    public Domino? Draw()
    {
        if (IsLocked || dominoes.Count == 0) return null;

        int randomIndex = random.Next(dominoes.Count);
        Domino domino = dominoes[randomIndex];
        dominoes.RemoveAt(randomIndex);
        return domino;
    }

    public int GetDominoInStack()
    {
        return ActiveBoneyardCount;
    }

    public int TotalRemainingTiles()
    {
        return dominoes.Count;
    }

    public bool HasAnyRemainingTileWithSuit(int suit)
    {
        return dominoes.Any(d => d.Side1 == suit || d.Side2 == suit);
    }

    public void Add(Domino domino)
    {
        dominoes.Add(domino);
    }

    public bool IsEmpty()
    {
        return IsLocked || dominoes.Count == 0;
    }
}
