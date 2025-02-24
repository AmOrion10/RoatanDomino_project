using DominoCommon.com.domino.common.Scripts.Networking;

namespace Domain.Game;

/// <summary>
/// The MoveValidator is responsible for checking if a particular move is valid within the game's rules.
/// </summary>
public class MoveValidator(GameGridDto gridDto)
{
    /// <summary>
    /// Determines whether a horizontal placement is valid.
    /// </summary>
    /// <param name="domino">The domino to place.</param>
    /// <param name="row">The row position to check.</param>
    /// <param name="col">The column position to check.</param>
    /// <param name="previousDominoOnLeft">Whether the placement is left to right or right to left.</param>
    /// <returns>true if the move is valid; otherwise, false.</returns>
    public bool CanPlaceHorizontal(Domino domino, int row, int col, bool previousDominoOnLeft)
    {
        // TODO: extract it // vérifie que les cases autour sont libres
        if ((gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue) || (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue)) return false;

        if (previousDominoOnLeft) // previous domino is on the left
        {
            if (col + 1 > gridDto.Col - 1) return false; // vérifie que la deuxième case ne dépasse pas la bordure droite

            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

            if (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 2] != GameGridDto.EmptySpotValue)
                return false;

            if ((gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) // vérifie si le domino de gauche a un chiffre commun
                    && (gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) // vérifie si le domino de dessous a un chiffre commun
                    && (gridDto.Grid[row + 1][col] != domino.Side2 && gridDto.Grid[row + 1][col] != domino.Side1)) // vérifie si le domino de dessus a un chiffre commun
                return false;

            if (col + 2 > gridDto.Col - 1 || col + 3 > gridDto.Col - 1) return false; // vérifie qu'en posant le domino, nous n'atteignons pas la bordure droite

            if (gridDto.Grid[row][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 3] != GameGridDto.EmptySpotValue) return false; // vérifie si l'on ne boucle pas
        }
        else // previous domino is on the right
        {
            if (col - 1 <= 0) return false; // vérifie que la deuxième case ne dépasse pas la bordure gauche

            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 2] != GameGridDto.EmptySpotValue)
               return false;

            if ((gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2) // vérifie si le domino de droite a un chiffre commun
                    && (gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) // vérifie si le domino de dessous a un chiffre commun
                    && (gridDto.Grid[row + 1][col] != domino.Side2 && gridDto.Grid[row + 1][col] != domino.Side1)) // vérifie si le domino de dessus a un chiffre commun
                return false;

            if (col - 2 <= 0 || col - 3 <= 0) return false; // vérifie qu'en posant le domino, nous n'atteignons pas la bordure gauche

            if (gridDto.Grid[row][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 3] != GameGridDto.EmptySpotValue) return false; // vérifie si l'on ne boucle pas
        }
        return true;
    }

    /// <summary>
    /// Determines whether a vertical placement is valid.
    /// </summary>
    /// <param name="domino">The domino to place.</param>
    /// <param name="row">The row position to check.</param>
    /// <param name="col">The column position to check.</param>
    /// <param name="previousDominoAtTop">Whether the placement is top to bottom or bottom to top.</param>
    /// <returns>true if the move is valid; otherwise, false.</returns>
    public bool CanPlaceVertical(Domino domino, int row, int col, bool previousDominoAtTop)
    {
        // vérifie que les cases autour sont libres
        if ((gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue) || (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue && gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue)) return false;

        if (previousDominoAtTop) // previous domino is at the top
        {
            if (row + 1 > gridDto.Row - 1) return false; // vérifie que la deuxième case ne dépasse pas la bordure basse

            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

            if (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row + 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col + 1] != GameGridDto.EmptySpotValue)
                return false;

            if ((gridDto.Grid[row - 1][col] != domino.Side2 && gridDto.Grid[row - 1][col] != domino.Side1) // vérifie si le domino du dessus a un chiffre commun
                    && (gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) // vérifie si le domino de gauche a un chiffre commun
                    && (gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2)) // vérifie si le domino de droite a un chiffre commun
                return false;

            if (row + 2 > gridDto.Row - 1 || row + 3 > gridDto.Row - 1) return false; // vérifie qu'en posant le domino, nous n'atteignons pas la bordure basse

            if (gridDto.Grid[row + 2][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 3][col] != GameGridDto.EmptySpotValue) return false;
        }
        else // previous domino is at the bottom
        {
            if (row - 1 <= 0) return false; // vérifie que la deuxième case ne dépasse pas la bordure haute

            if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col + 1] != GameGridDto.EmptySpotValue)
                return false;

            if ((gridDto.Grid[row + 1][col] != domino.Side1 && gridDto.Grid[row + 1][col] != domino.Side2) // vérifie si le domino du dessous a un chiffre commun
                    && (gridDto.Grid[row][col - 1] != domino.Side1 && gridDto.Grid[row][col - 1] != domino.Side2) // vérifie si le domino de gauche a un chiffre commun
                    && (gridDto.Grid[row][col + 1] != domino.Side1 && gridDto.Grid[row][col + 1] != domino.Side2)) // vérifie si le domino de droite a un chiffre commun
                return false;

            if (row - 2 <= 0 || row - 3 <= 0) return false; // vérifie qu'en posant le domino, nous n'atteignons pas la bordure haute

            if (gridDto.Grid[row - 2][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 3][col] != GameGridDto.EmptySpotValue) return false; // vérifie si l'on ne boucle pas
        }
        return true;
    }

    /// <summary>
    /// Determines whether a double domino can be placed horizontally, considering additional criteria for double placement.
    /// </summary>
    /// <param name="domino">The domino to place, which must be a double.</param>
    /// <param name="row">The row position to check for placement.</param>
    /// <param name="col">The column position to check for placement.</param>
    /// <param name="previousDominoUp">Indicates whether the previous domino's placement is above this one.</param>
    /// <returns>true if the move is valid, otherwise false. Validation is based on the orientation and proximity to other dominos.</returns>
    public bool CanPlaceDoubleHorizontal(Domino domino, int row, int col, bool previousDominoUp)
    {
        if (row < 0 || col < 0 || row > gridDto.Row - 1 || col > gridDto.Col - 1) return false; // vérifie que la cellule principale est dans la grille

        if (row + 1 > gridDto.Row - 1 || row - 1 < 0 || col + 1 > gridDto.Col - 1 || col - 1 < 0) return false; // vérifie que les deux autres cellules sont dans la grille

        if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

        if (previousDominoUp) // previous domino is at the top
        {
            if (gridDto.Grid[row - 1][col] != domino.Side1) return false; // vérifie si le domino du dessus a un chiffre commun

            if (gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row + 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 2][col] != GameGridDto.EmptySpotValue) // A VOIR SI ON LAISSE CETTE LIGNE
                return false;
        }
        else // previous domino is at the bottom
        {
            if (gridDto.Grid[row + 1][col] != domino.Side1) return false; // vérifie si le domino du dessous a un chiffre commun

            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 2][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 2][col] != GameGridDto.EmptySpotValue) // A VOIR SI ON LAISSE CETTE LIGNE
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a double domino can be placed vertically, tailored to handle cases where dominos are perpendicular to the main line.
    /// </summary>
    /// <param name="domino">The domino to place, expected to be a double.</param>
    /// <param name="row">The row position to check.</param>
    /// <param name="col">The column position to check.</param>
    /// <param name="previousDominoLeft">Indicates whether the previous connected domino is left of the current position.</param>
    /// <returns>true if the move is valid, otherwise false. Checks include orientation and valid connections to adjacent dominos.</returns>
    public bool CanPlaceDoubleVertical(Domino domino, int row, int col, bool previousDominoLeft)
    {
        if (row < 0 || col < 0 || row > gridDto.Row - 1 || col > gridDto.Col - 1) return false; // vérifie que la cellule principale est dans la grille

        if (row + 1 > gridDto.Row - 1 || row - 1 < 0 || col + 1 > gridDto.Col - 1 || col - 1 < 0) return false; // vérifie que les deux autres cellules sont dans la grille

        if (gridDto.Grid[row][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row - 1][col] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col] != GameGridDto.EmptySpotValue) return false; // vérifie que les cellules où le domino sera posé sont libres

        if (previousDominoLeft) // previous domino is on the left
        {
            if (gridDto.Grid[row][col - 1] != domino.Side1) return false; // vérifie si le domino de gauche a un chiffre commun

            if (gridDto.Grid[row - 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col + 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col + 2] != GameGridDto.EmptySpotValue) // A VOIR SI ON LAISSE CETTE LIGNE
                return false;
        }
        else // previous domino is on the right
        {
            if (gridDto.Grid[row][col + 1] != domino.Side1) return false; // vérifie si le domino de droite a un chiffre commun

            if (gridDto.Grid[row - 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 1] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 1] != GameGridDto.EmptySpotValue // vérifie que les cellules autour du domino qui sera posé sont libres
                    || gridDto.Grid[row - 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row + 1][col - 2] != GameGridDto.EmptySpotValue || gridDto.Grid[row][col - 2] != GameGridDto.EmptySpotValue) // A VOIR SI ON LAISSE CETTE LIGNE
                return false;
        }

        return true;
    }
}