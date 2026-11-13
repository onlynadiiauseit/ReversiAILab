namespace Reversi.Core;

public enum EvaluationMode
{
    DiscDifference,
    Positional
}

public static class Evaluation
{
    static readonly int[] Weights =
    {
        120, -20,  20,   5,   5,  20, -20, 120,
        -20, -40,  -5,  -5,  -5,  -5, -40, -20,
         20,  -5,  15,   3,   3,  15,  -5,  20,
          5,  -5,   3,   3,   3,   3,  -5,   5,
          5,  -5,   3,   3,   3,   3,  -5,   5,
         20,  -5,  15,   3,   3,  15,  -5,  20,
        -20, -40,  -5,  -5,  -5,  -5, -40, -20,
        120, -20,  20,   5,   5,  20, -20, 120
    };

    public static int Utility(Board b, sbyte max)
    {
        ArgumentNullException.ThrowIfNull(b);
        Board.ValidateSide(max);
        if (!b.IsTerminal)
            throw new ArgumentException("Only terminal positions have utility");
        int d = b.Count(max) - b.Count((sbyte)-max);
        return d > 0 ? 100000 + d : d < 0 ? -100000 + d : 0;
    }

    public static int Estimate(Board b, sbyte max, EvaluationMode mode)
    {
        ArgumentNullException.ThrowIfNull(b);
        Board.ValidateSide(max);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (b.IsTerminal)
            return Utility(b, max);
        int discs = b.Count(max) - b.Count((sbyte)-max);
        if (mode == EvaluationMode.DiscDifference)
            return discs;
        int positional = 0;
        for (int i = 0; i < 64; i++)
            positional += Weights[i] * b.Cells[i] * max;
        int mobility = b.LegalMoves(max).Count - b.LegalMoves((sbyte)-max).Count;
        int[] corners =
        {
            0,
            7,
            56,
            63
        };
        int cornerDiff = corners.Sum(i => b.Cells[i] * max);
        // Potential mobility: empty squares adjacent to opponent discs (distinct squares).
        int potential(sbyte player)
        {
            int count = 0;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (b.Cells[r * 8 + c] != 0)
                        continue;
                    bool near = false;
                    for (int dr = -1; dr <= 1 && !near; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int nr = r + dr, nc = c + dc;
                            if (nr >= 0 && nr < 8 && nc >= 0 && nc < 8 && b.Cells[nr * 8 + nc] == -player)
                                near = true;
                        }

                    if (near)
                        count++;
                }

            return count;
        }

        int pm = potential(max) - potential((sbyte)-max);
        int occupied = b.Count(1) + b.Count(-1);
        // Disc count matters more in endgame; corners/mobility dominate early and middle game.
        int discWeight = occupied < 44 ? 1 : occupied < 56 ? 3 : 12;
        return positional + 25 * mobility + 45 * cornerDiff + 5 * pm + discWeight * discs;
    }
}
