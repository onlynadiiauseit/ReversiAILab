namespace Reversi.Core;

public readonly record struct Move(int Row, int Col)
{
    public override string ToString() => $"{(char)('A' + Col)}{Row + 1}";
}

public sealed class Board
{
    public const int N = 8;
    readonly sbyte[] cells;

    public IReadOnlyList<sbyte> Cells { get; }

    public sbyte Turn { get; private set; }

    public int Passes { get; private set; }

    public Board()
    {
        cells = new sbyte[64];
        cells[27] = -1;
        cells[28] = 1;
        cells[35] = 1;
        cells[36] = -1;
        Cells = Array.AsReadOnly(cells);
        Turn = 1;
    }

    private Board(sbyte[] c, sbyte t, int passes)
    {
        cells = c;
        Cells = Array.AsReadOnly(c);
        Turn = t;
        Passes = passes;
    }

    public Board Clone() => new((sbyte[])cells.Clone(), Turn, Passes);

    public static Board FromRows(string[] rows, sbyte turn = 1, int passes = 0)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ValidateSide(turn);
        if (passes < 0)
            throw new ArgumentOutOfRangeException(nameof(passes));
        if (rows.Length != 8 || rows.Any(x => x is null || x.Length != 8))
            throw new ArgumentException("8 rows x 8 characters required");
        var c = new sbyte[64];
        for (int r = 0; r < 8; r++)
            for (int k = 0; k < 8; k++)
                c[r * 8 + k] = rows[r][k] switch
                {
                    'B' => (sbyte)1,
                    'W' => (sbyte)-1,
                    '.' => (sbyte)0,
                    _ => throw new ArgumentException("Use B, W, .")
                };
        return new(c, turn, passes);
    }

    public static void ValidateSide(sbyte side)
    {
        if (side is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(side));
    }

    public int Count(sbyte side)
    {
        ValidateSide(side);
        return Cells.Count(v => v == side);
    }

    static readonly (int dr, int dc)[] Directions =
    {
        (-1, -1),
        (-1, 0),
        (-1, 1),
        (0, -1),
        (0, 1),
        (1, -1),
        (1, 0),
        (1, 1)
    };

    static bool Inside(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;

    public List<int> Flips(Move m, sbyte side)
    {
        ValidateSide(side);
        var flips = new List<int>();
        if (!Inside(m.Row, m.Col) || Cells[m.Row * 8 + m.Col] != 0)
            return flips;
        foreach (var (dr, dc) in Directions)
        {
            int r = m.Row + dr, c = m.Col + dc;
            var segment = new List<int>();
            while (Inside(r, c) && Cells[r * 8 + c] == -side)
            {
                segment.Add(r * 8 + c);
                r += dr;
                c += dc;
            }

            if (segment.Count > 0 && Inside(r, c) && Cells[r * 8 + c] == side)
                flips.AddRange(segment);
        }

        return flips;
    }

    bool Captures(Move m, sbyte side)
    {
        if (!Inside(m.Row, m.Col) || Cells[m.Row * 8 + m.Col] != 0)
            return false;
        foreach (var (dr, dc) in Directions)
        {
            int r = m.Row + dr, c = m.Col + dc, count = 0;
            while (Inside(r, c) && Cells[r * 8 + c] == -side)
            {
                count++;
                r += dr;
                c += dc;
            }

            if (count > 0 && Inside(r, c) && Cells[r * 8 + c] == side)
                return true;
        }

        return false;
    }

    public List<Move> LegalMoves(sbyte side)
    {
        ValidateSide(side);
        var output = new List<Move>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var m = new Move(r, c);
                if (Captures(m, side))
                    output.Add(m);
            }

        return output;
    }

    public bool HasLegalMove(sbyte side)
    {
        ValidateSide(side);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                if (Captures(new Move(r, c), side))
                    return true;
        return false;
    }

    public bool IsTerminal => !HasLegalMove(1) && !HasLegalMove(-1);

    public Board Play(Move? move)
    {
        if (IsTerminal)
            throw new InvalidOperationException("Game ended");
        var result = Clone();
        if (move is null)
        {
            if (LegalMoves(Turn).Count > 0)
                throw new InvalidOperationException("Cannot pass with legal moves");
            result.Turn = (sbyte)-Turn;
            result.Passes++;
            return result;
        }

        var flips = Flips(move.Value, Turn);
        if (flips.Count == 0)
            throw new ArgumentException("Illegal move");
        result.cells[move.Value.Row * 8 + move.Value.Col] = Turn;
        foreach (int i in flips)
            result.cells[i] = Turn;
        result.Turn = (sbyte)-Turn;
        result.Passes = 0;
        return result;
    }

    public string Key() => string.Concat(Cells.Select(x => x == 1 ? 'B' : x == -1 ? 'W' : '.')) + (Turn == 1 ? 'B' : 'W');

    public override string ToString() => string.Join(
        Environment.NewLine,
        Enumerable.Range(0, 8)
        .Select(r => new string(Enumerable.Range(0, 8)
        .Select(c => Cells[r * 8 + c] == 1 ? 'B' : Cells[r * 8 + c] == -1 ? 'W' : '.')
        .ToArray())));
}
