using System.Diagnostics;
namespace Reversi.Core;

public enum Ordering { Natural, BestFirst, WorstFirst }
public sealed record TraceEntry(int Id, int? Parent, int Ply, string Player, string Move, int Score, int AlphaBefore, int BetaBefore, int AlphaAfter, int BetaAfter, bool Cutoff, string Reason);
public sealed record SearchResult(Move? Move, int Score, long Nodes, long Leaves, long Cutoffs, double Milliseconds, List<TraceEntry> Trace);
public sealed class SearchEngine
{
    readonly List<TraceEntry> trace = new(); long nodes, leaves, cutoffs; int nextId; sbyte max; CancellationToken token;
    public SearchResult FindBest(Board board, int depth, bool useAlphaBeta, Ordering order = Ordering.Natural, EvaluationMode evaluation = EvaluationMode.Positional, int traceLimit = 2500, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board); if (depth < 1 || depth > 12) throw new ArgumentOutOfRangeException(nameof(depth)); if (traceLimit < 0) throw new ArgumentOutOfRangeException(nameof(traceLimit)); if (!Enum.IsDefined(order)) throw new ArgumentOutOfRangeException(nameof(order)); if (!Enum.IsDefined(evaluation)) throw new ArgumentOutOfRangeException(nameof(evaluation)); token = cancellationToken; max = board.Turn; nodes = leaves = cutoffs = 0; nextId = 0; trace.Clear(); var watch = Stopwatch.StartNew();
        var (score, move) = Visit(board, depth, int.MinValue, int.MaxValue, useAlphaBeta, order, evaluation, null, "ROOT", 0, traceLimit); watch.Stop(); return new(move, score, nodes, leaves, cutoffs, watch.Elapsed.TotalMilliseconds, new(trace));
    }
    (int, Move?) Visit(Board b, int depth, int alpha, int beta, bool ab, Ordering order, EvaluationMode mode, int? parent, string moveName, int ply, int traceLimit)
    {
        token.ThrowIfCancellationRequested(); int id = nextId++; nodes++; int origA = alpha, origB = beta; bool maximizing = b.Turn == max;
        if (b.IsTerminal || depth == 0) { leaves++; int val = b.IsTerminal ? Evaluation.Utility(b, max) : Evaluation.Estimate(b, max, mode); Log(val, false, b.IsTerminal ? "terminal" : "depth limit"); return (val, null); }
        var moves = b.LegalMoves(b.Turn);
        if (moves.Count == 0) { var (passScore, _) = Visit(b.Play(null), depth - 1, alpha, beta, ab, order, mode, id, "PASS", ply + 1, traceLimit); Log(passScore, false, "forced pass"); return (passScore, null); }
        IEnumerable<Move> sorted = moves;
        if (order != Ordering.Natural) { var ranked = moves.Select(m => (m, score: Evaluation.Estimate(b.Play(m), max, mode))); sorted = (maximizing ? (order == Ordering.BestFirst ? ranked.OrderByDescending(x => x.score) : ranked.OrderBy(x => x.score)) : (order == Ordering.BestFirst ? ranked.OrderBy(x => x.score) : ranked.OrderByDescending(x => x.score))).ThenBy(x => x.m.Row).ThenBy(x => x.m.Col).Select(x => x.m).ToList(); }
        int best = maximizing ? int.MinValue : int.MaxValue; Move? chosen = null; bool cutoff = false;
        int remaining = moves.Count; foreach (var move in sorted)
        {
            remaining--; var (v, _) = Visit(b.Play(move), depth - 1, alpha, beta, ab, order, mode, id, move.ToString(), ply + 1, traceLimit);
            if (maximizing ? (v > best) : (v < best)) { best = v; chosen = move; }
            if (ab) { if (maximizing) alpha = Math.Max(alpha, best); else beta = Math.Min(beta, best); if (alpha >= beta && remaining > 0) { cutoffs++; cutoff = true; break; } }
        }
        Log(best, cutoff, cutoff ? "alpha >= beta" : "expanded"); return (best, chosen);
        // Reserve by preorder ID, retaining the root and all ancestors even when truncated.
        void Log(int value, bool cut, string reason) { if (id < traceLimit) trace.Add(new TraceEntry(id, parent, ply, maximizing ? "MAX" : "MIN", moveName, value, origA, origB, alpha, beta, cut, reason)); }
    }
}
