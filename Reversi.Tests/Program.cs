using Reversi.Core;

int pass = 0;
void Check(bool cond, string name)
{
    if (!cond)
        throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS " + name);
    pass++;
}

var b = new Board();
Check(b.LegalMoves(1).Count == 4, "opening has four legal moves");
Check(b.Count(1) == 2 && b.Count(-1) == 2, "initial discs");
var m = new Move(2, 3);
Check(b.LegalMoves(1).Contains(m), "D3 legal for black");
var child = b.Play(m);
Check(child.Count(1) == 4 && child.Count(-1) == 1, "flip in D3");
Check(b.Count(1) == 2, "board is immutable across Play");
try
{
    b.Play(new Move(0, 0));
    throw new Exception("Illegal move was accepted");
}
catch (ArgumentException)
{
    Check(true, "illegal move rejected");
}

try
{
    b.Play(null);
    throw new Exception("Illegal pass was accepted");
}
catch (InvalidOperationException)
{
    Check(true, "illegal pass rejected");
}

var full = Board.FromRows(Enumerable.Repeat("BBBBBBBB", 8).ToArray());
Check(full.IsTerminal && Evaluation.Utility(full, 1) > 0, "terminal utility win");
var tie = Board.FromRows(Enumerable.Range(0, 8)
    .Select(i => i < 4 ? "BBBBBBBB" : "WWWWWWWW")
    .ToArray());
Check(tie.IsTerminal && Evaluation.Utility(tie, 1) == 0, "terminal draw");
// Pass position: white has no move, black can place at A1.
var passBoard = Board.FromRows(
    new[] { ".WBBBBBB", "BBBBBBBB", "BBBBBBBB", "BBBBBBBB", "BBBBBBBB", "BBBBBBBB", "BBBBBBBB", "BBBBBBBB" },
    -1);
Check(
    passBoard.LegalMoves(-1).Count == 0 && passBoard.LegalMoves(1).Count == 1,
    "forced pass position");
Check(passBoard.Play(null).Turn == 1, "pass changes player");
foreach (int depth in new[] { 1, 2, 3, 4 })
    foreach (var order in Enum.GetValues<Ordering>())
        foreach (var mode in Enum.GetValues<EvaluationMode>())
        {
            var plain = new SearchEngine().FindBest(b, depth, false, order, mode, 0);
            var optimized = new SearchEngine().FindBest(b, depth, true, order, mode, 0);
            Check(
                plain.Score == optimized.Score,
                $"same minimax score d={depth} order={order} eval={mode}");
            Check(
                optimized.Nodes <= plain.Nodes,
                $"alpha beta visits no more nodes d={depth} order={order} eval={mode}");
        }

var forced = new SearchEngine().FindBest(passBoard, 3, true);
Check(forced.Move == null, "search chooses forced pass");
void Throws<T>(Action action, string name)
    where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        Check(true, name);
        return;
    }

    throw new Exception("FAILED: " + name);
}

Throws<ArgumentOutOfRangeException>(
    () => Board.FromRows(Enumerable.Repeat("........", 8)
    .ToArray(), 0),
    "invalid player rejected");
Throws<ArgumentOutOfRangeException>(() => b.LegalMoves(0), "invalid move-generation side rejected");
Throws<ArgumentOutOfRangeException>(
    () => b.Flips(new Move(2, 3), 0),
    "invalid capture side rejected");
Throws<ArgumentException>(() => Board.FromRows(new string[8]), "null rows rejected");
Throws<ArgumentException>(
    () => Board.FromRows(Enumerable.Repeat("XXXXXXXX", 8)
    .ToArray()),
    "invalid cells rejected");
Throws<ArgumentOutOfRangeException>(
    () => Board.FromRows(Enumerable.Repeat("........", 8)
    .ToArray(), 1, -1),
    "negative pass count rejected");
Throws<NotSupportedException>(
    () => ((IList<sbyte>)b.Cells)[0] = 1,
    "external board mutation rejected");
Throws<InvalidOperationException>(() => full.Play(null), "terminal pass rejected");
Throws<ArgumentException>(() => Evaluation.Utility(b, 1), "utility rejects unfinished board");
Throws<ArgumentOutOfRangeException>(
    () => new SearchEngine().FindBest(b, 0, true),
    "invalid depth rejected");
Throws<ArgumentOutOfRangeException>(
    () => new SearchEngine().FindBest(b, 2, true, (Ordering)99),
    "invalid ordering rejected");
Throws<ArgumentOutOfRangeException>(
    () => new SearchEngine().FindBest(b, 2, true, traceLimit: -1),
    "invalid trace limit rejected");
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    Throws<OperationCanceledException>(
        () => new SearchEngine().FindBest(b, 8, true, cancellationToken: cts.Token),
        "search cancellation");
}

foreach (var outside in new[]
{
    new Move(-1, 0),
    new Move(0, -1),
    new Move(8, 0),
    new Move(0, 8)
})
    Throws<ArgumentException>(() => b.Play(outside), $"out of bounds {outside.Row},{outside.Col}");
var eight = Board.FromRows(new[] { "........", ".B.B.B..", "..WWW...", ".BW.WB..", "..WWW...", ".B.B.B..", "........", "........" });
Check(eight.Flips(new Move(3, 3), 1).Count == 8, "captures all eight directions");
Check(eight.Play(new Move(3, 3)).Count(-1) == 0, "all captured lines flipped");
var blocked = Board.FromRows(new[] { "B.......", "........", "........", "........", "........", "........", "........", "........" });
Check(
    blocked.IsTerminal && blocked.Count(1) == 1,
    "game ends with empty squares when both sides blocked");
Check(Evaluation.Utility(full, -1) == -100064, "exact terminal loss utility");
Check(passBoard.Play(null).Play(new Move(0, 0)).Passes == 0, "move resets pass counter");
foreach (var mode in Enum.GetValues<EvaluationMode>())
    Check(
        Evaluation.Estimate(child, 1, mode) == -Evaluation.Estimate(child, -1, mode),
        $"antisymmetric evaluation {mode}");
long Perft(Board position, int remaining)
{
    if (remaining == 0 || position.IsTerminal)
        return 1;
    var legal = position.LegalMoves(position.Turn);
    return legal.Count == 0
        ? Perft(position.Play(null), remaining - 1)
        : legal.Sum(move => Perft(position.Play(move), remaining - 1));
}

long[] expected =
{
    4,
    12,
    56,
    244,
    1396,
    8200
};
for (int d = 1; d <= expected.Length; d++)
    Check(Perft(b, d) == expected[d - 1], $"opening perft depth {d}");
var truncated = new SearchEngine().FindBest(b, 4, true, traceLimit: 5);
Check(
    truncated.Trace.Count == 5 && truncated.Trace.Any(x => x.Id == 0 && x.Parent == null),
    "bounded trace retains root");
Check(
    truncated.Trace.All(x => x.Parent is null || truncated.Trace.Any(p => p.Id == x.Parent)),
    "bounded trace retains parents");
var completeTrace = new SearchEngine().FindBest(b, 3, true, traceLimit: 100000);
Check(completeTrace.Trace.Count == completeTrace.Nodes, "complete trace covers every visited node");
Check(
    completeTrace.Trace.Count(x => x.Cutoff) == completeTrace.Cutoffs,
    "trace cutoff count matches statistics");
Check(
    completeTrace.Trace.Where(x => x.Cutoff)
    .All(x => x.AlphaAfter >= x.BetaAfter),
    "cutoff bounds valid");
var single = new SearchEngine().FindBest(passBoard.Play(null), 1, true);
Check(single.Cutoffs == 0, "single child is never counted as pruning");
int Exact(Board position, sbyte root)
{
    if (position.IsTerminal)
        return Evaluation.Utility(position, root);
    var moves = position.LegalMoves(position.Turn);
    if (moves.Count == 0)
        return Exact(position.Play(null), root);
    var values = moves.Select(move => Exact(position.Play(move), root));
    return position.Turn == root ? values.Max() : values.Min();
}

for (int seed = 0; seed < 8; seed++)
{
    var random = new Random(seed);
    Board position = new();
    bool valid = true;
    int transitions = 0;
    bool checkedLate = false;
    while (!position.IsTerminal)
    {
        var legal = position.LegalMoves(position.Turn);
        string key = position.Key();
        int occupied = position.Count(1) + position.Count(-1);
        Move? move = legal.Count == 0 ? null : legal[random.Next(legal.Count)];
        var next = position.Play(move);
        valid &= position.Key() == key && next.Turn == -position.Turn && next.Count(1) + next.Count(-1) == occupied + (move is null ? 0 : 1);
        if (!checkedLate && occupied >= 60)
        {
            checkedLate = true;
            int exact = Exact(position, position.Turn);
            foreach (var mode in Enum.GetValues<EvaluationMode>())
                foreach (var order in Enum.GetValues<Ordering>())
                {
                    var result = new SearchEngine().FindBest(position, 12, true, order, mode, 0);
                    Check(result.Score == exact, $"exact endgame seed={seed} {mode} {order}");
                    if (result.Move is { } chosen)
                        Check(
                            Exact(position.Play(chosen), position.Turn) == exact,
                            $"chosen endgame move optimal seed={seed} {mode} {order}");
                }
        }

        position = next;
        if (++transitions > 120)
            throw new Exception("Game failed to terminate");
    }

    Check(valid && transitions >= 60, $"full legal game invariants seed={seed}");
}

Check(PositionAnalysis.Classify(b) == PositionKind.Quiet, "opening is quiet");
Check(PositionAnalysis.Classify(passBoard) == PositionKind.Risky, "forced pass is risky");
Check(PositionAnalysis.Classify(passBoard.Play(null)) == PositionKind.Risky, "available corner is risky");
Check(PositionAnalysis.Classify(full) == PositionKind.Terminal, "terminal position classification");
Throws<ArgumentNullException>(() => PositionAnalysis.Classify(null!), "null position analysis rejected");
Throws<ArgumentOutOfRangeException>(
    () => new SearchEngine().FindBest(b, 1, true, quiescenceDepth: -1),
    "negative extension depth rejected");
Throws<ArgumentOutOfRangeException>(
    () => new SearchEngine().FindBest(b, 1, true, quiescenceDepth: 5),
    "excessive extension depth rejected");

var quietPlain = new SearchEngine().FindBest(b, 1, true);
var quietExtended = new SearchEngine().FindBest(b, 1, true, quiescenceDepth: 2);
Check(quietPlain.Score == quietExtended.Score && quietPlain.Nodes == quietExtended.Nodes,
    "quiet horizon uses the heuristic immediately");
Check(quietExtended.Extensions == 0 && quietExtended.Trace.Any(x => x.Reason == "quiet position"),
    "quiet leaves are identified in the trace");

var riskyCorner = Board.FromRows(new[]
{
    ".WBBBBB.",
    "BBBBBBBW",
    "BBBBBBBB",
    "BBBBBBBB",
    "BBBBBBBB",
    "BBBBBBBB",
    "BBBBBBBB",
    "BBBBBBBB"
});
var resolved = new SearchEngine().FindBest(riskyCorner, 1, true, quiescenceDepth: 2);
Check(resolved.Score == Exact(riskyCorner, riskyCorner.Turn),
    "corner continuation resolves the tactical ending exactly");
Check(resolved.Extensions > 0 && resolved.Trace.Any(x => x.Ply > 1),
    "risky leaves extend beyond normal depth");
var bounded = new SearchEngine().FindBest(riskyCorner, 1, true, quiescenceDepth: 1);
Check(bounded.Trace.Max(x => x.Ply) <= 2 && bounded.Trace.Any(x => x.Reason == "quiescence limit"),
    "extension budget strictly bounds the horizon");
var resolvedPass = new SearchEngine().FindBest(passBoard, 1, true, quiescenceDepth: 2);
Check(resolvedPass.Move is null && resolvedPass.Score == -100064,
    "forced pass continuation preserves root perspective");
Check(resolvedPass.Trace.Any(x => x.Reason == "terminal"),
    "terminal utility takes priority during extension");

foreach (var position in new[] { b, riskyCorner, passBoard })
    foreach (var mode in Enum.GetValues<EvaluationMode>())
        foreach (var order in Enum.GetValues<Ordering>())
        {
            var plain = new SearchEngine().FindBest(position, 1, false, order, mode, 0,
                quiescenceDepth: 2);
            var optimized = new SearchEngine().FindBest(position, 1, true, order, mode, 1000,
                quiescenceDepth: 2);
            Check(plain.Score == optimized.Score && optimized.Nodes <= plain.Nodes,
                $"same extended minimax value {PositionAnalysis.Classify(position)} {mode} {order}");
            Check(optimized.Trace.All(x => x.Ply <= 3), "extended search respects maximum ply");
        }

using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    Throws<OperationCanceledException>(
        () => new SearchEngine().FindBest(riskyCorner, 1, true,
            cancellationToken: cts.Token, quiescenceDepth: 2),
        "extended search cancellation");
}

Console.WriteLine($"ALL {pass} TESTS PASSED");
