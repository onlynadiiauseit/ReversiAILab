namespace Reversi.Core;

public enum PositionKind
{
    Quiet,
    Risky,
    Terminal
}

public static class PositionAnalysis
{
    static readonly Move[] Corners =
    {
        new(0, 0),
        new(0, 7),
        new(7, 0),
        new(7, 7)
    };

    // A tactical heuristic: an immediate corner opportunity or a forced pass
    // can make a static evaluation at the depth boundary premature.
    public static PositionKind Classify(Board board)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (board.IsTerminal)
            return PositionKind.Terminal;

        if (!board.HasLegalMove(board.Turn))
            return PositionKind.Risky;

        foreach (var corner in Corners)
        {
            if (board.Cells[corner.Row * Board.N + corner.Col] != 0)
                continue;

            if (board.Flips(corner, 1).Count > 0 || board.Flips(corner, -1).Count > 0)
                return PositionKind.Risky;
        }

        return PositionKind.Quiet;
    }
}
