namespace Domain.Game;

public class DominoStack
{
    private const int NumberOfDomino = 7;

    private readonly List<Domino> dominoes = [];
    private readonly Random random;

    public DominoStack(int? seed = null)
    {
        random = seed is null ? new Random() : new Random(seed.Value);
        for (int i = 0; i < NumberOfDomino; i++)
        {
            for (int j = i; j < NumberOfDomino; j++)
            {
                dominoes.Add(new Domino(i, j));
            }
        }
    }

    public Domino? Draw()
    {
        if (IsEmpty()) return null;

        int randomIndex = random.Next(dominoes.Count);
        Domino domino = dominoes[randomIndex];
        dominoes.RemoveAt(randomIndex);
        return domino;
    }

    public int GetDominoInStack()
    {
        return dominoes.Count;
    }

    public void Add(Domino domino)
    {
        dominoes.Add(domino);
    }

    public bool IsEmpty()
    {
        return dominoes.Count == 0;
    }
}
