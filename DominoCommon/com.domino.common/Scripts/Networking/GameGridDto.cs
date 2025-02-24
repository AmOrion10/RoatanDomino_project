using System.Collections.Generic;

namespace DominoCommon.com.domino.common.Scripts.Networking
{
    public class GameGridDto
    {
        public const int EmptySpotValue = -1;
        public int Row { get; }
        public int Col { get; }
        public List<List<int>> Grid { get; } = new List<List<int>>();

        public GameGridDto(int nRow, int nCol)
        {
            for (int i = 0; i < nRow; i++) // Define the grid size
            {
                Grid.Add(new List<int>());
                for (int j = 0; j < nCol; j++)
                {
                    Grid[i].Add(EmptySpotValue);
                }
            }
            Row = nRow;
            Col = nCol;
        }

        public List<int> this[int i]
        {
            get => Grid[i];
            set => Grid[i] = value;
        }
    }
}
