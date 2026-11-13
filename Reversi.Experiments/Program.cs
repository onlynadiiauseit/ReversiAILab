using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Reversi.Core;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
bool quiescenceExamples = args.Contains("--quiescence");
string output = args.FirstOrDefault(argument => argument != "--quiescence") ?? "results";
Directory.CreateDirectory(output);
if (quiescenceExamples)
{
    RunQuiescenceExamples(output);
    return;
}
var positions = new Dictionary<string, Board>
{
    {
        "opening",
        new Board()
    }
};
// Deterministic midgame/endgame positions obtained by replaying legal moves, not hand-invented boards.
Board b = new();
int turn = 0;
while (!b.IsTerminal && turn < 57)
{
    var legal = b.LegalMoves(b.Turn);
    b = b.Play(legal.Count == 0 ? null : legal[(turn * 7 + 3) % legal.Count]);
    turn++;
    if (turn == 22)
        positions["midgame"] = b.Clone();
    if (turn == 50)
        positions["endgame"] = b.Clone();
}

var measurements = new List<Measurement>();
using var csv = new StreamWriter(
    Path.Combine(output, "experiments.csv"),
    false,
    new UTF8Encoding(false));
csv.WriteLine("position,depth,method,ordering,evaluation,run,best_move,score,nodes,leaves,cutoffs,milliseconds");
foreach (var (name, pos) in positions)
    foreach (int depth in new[] { 2, 3, 4, 5 })
        foreach (var mode in new[] { EvaluationMode.DiscDifference, EvaluationMode.Positional })
            foreach (var order in new[] { Ordering.Natural, Ordering.BestFirst, Ordering.WorstFirst })
            {
                var baseline = new SearchEngine().FindBest(pos, depth, false, order, mode, 0);
                // Warm both methods outside recorded measurements and alternate measurement order.
                var warm = new SearchEngine().FindBest(pos, depth, true, order, mode, 0);
                if (warm.Score != baseline.Score)
                    throw new Exception("Warmup score mismatch");
                for (int run = 1; run <= 3; run++)
                    foreach (bool ab in run % 2 == 1 ? new[] { false, true }

                    : new[] { true, false })
                    {
                        var result = new SearchEngine().FindBest(pos, depth, ab, order, mode, 0);
                        if (result.Score != baseline.Score || (ab && result.Nodes > baseline.Nodes))
                            throw new Exception($"Incorrect search: {name} d={depth} {order} {mode}");
                        measurements.Add(new(
                                name,
                                depth,
                                ab ? "alphabeta" : "minimax",
                                order.ToString(),
                                mode.ToString(),
                                result));
                        csv.WriteLine($"{name},{depth},{(ab ? "alphabeta" : "minimax")},{order},{mode},{run},{result.Move?.ToString() ?? "PASS"},{result.Score},{result.Nodes},{result.Leaves},{result.Cutoffs},{result.Milliseconds:F4}");
                    }

                csv.Flush();
                Console.WriteLine($"{name} d={depth} {mode} {order}: {baseline.Nodes} -> {warm.Nodes} nodes");
            }

var options = new JsonSerializerOptions
{
    WriteIndented = true
};
var summary = measurements.GroupBy(x => (x.Position, x.Depth, x.Ordering, x.Evaluation)).Select(g =>
{
    var plain = g.Where(x => x.Method == "minimax").Select(x => x.Result).ToArray();
    var ab = g.Where(x => x.Method == "alphabeta").Select(x => x.Result).ToArray();
    return new
    {
        g.Key.Position,
        g.Key.Depth,
        g.Key.Ordering,
        g.Key.Evaluation,
        Score = plain[0].Score,
        MinimaxNodes = plain[0].Nodes,
        AlphaBetaNodes = ab[0].Nodes,
        ReductionPercent = 100.0 * (1.0 - (double)ab[0].Nodes / plain[0].Nodes),
        MinimaxMedianMs = plain.Select(x => x.Milliseconds).Order().ElementAt(1),
        AlphaBetaMedianMs = ab.Select(x => x.Milliseconds).Order().ElementAt(1),
        AlphaBetaCutoffs = ab[0].Cutoffs
    };
}).ToArray();
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, options));
File.WriteAllText(
    Path.Combine(output, "environment.json"),
    JsonSerializer.Serialize(
        new
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Framework = RuntimeInformation.FrameworkDescription,
            LogicalProcessors = Environment.ProcessorCount,
            BuildConfiguration = "Release (run with -c Release)",
            Measurements = measurements.Count,
            Warmup = "One unrecorded search per method and configuration",
            Repeats = 3,
            MethodOrder = "Alternates by repeat",
            DepthConvention = "Transitions including forced passes",
            CutoffConvention = "Only when a remaining child is skipped"
        },
        options));
// A small trace exported for the report. Postorder entries include IDs and parent IDs.
var traced = new SearchEngine().FindBest(
    positions["midgame"],
    3,
    true,
    Ordering.BestFirst,
    EvaluationMode.Positional,
    10000);
File.WriteAllText(
    Path.Combine(output, "tree_midgame.json"),
    JsonSerializer.Serialize(
        traced.Trace,
        new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(
    Path.Combine(output, "positions.txt"),
    string.Join(
        "\n\n",
        positions.Select(x => $"{x.Key} (turn {(x.Value.Turn == 1 ? "B" : "W")})\n{x.Value}")));
if (traced.Trace.Count != traced.Nodes)
    throw new Exception("Report trace unexpectedly truncated");
Console.WriteLine($"Completed {measurements.Count} verified measurements. Results written to " + Path.GetFullPath(output));

void RunQuiescenceExamples(string directory)
{
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
    var riskyPass = Board.FromRows(new[]
    {
        ".WBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB",
        "BBBBBBBB"
    }, -1);
    var examples = new[]
    {
        (Name: "quiet", Board: new Board()),
        (Name: "risky_corner", Board: riskyCorner),
        (Name: "risky_pass", Board: riskyPass)
    };

    using var csv = new StreamWriter(
        Path.Combine(directory, "quiescence.csv"), false, new UTF8Encoding(false));
    csv.WriteLine("position,kind,depth,extra_depth,method,move,score,nodes,extensions,milliseconds");

    foreach (var example in examples)
    {
        foreach (int extraDepth in new[] { 0, 2 })
        {
            var plain = new SearchEngine().FindBest(
                example.Board, 1, false, Ordering.BestFirst, traceLimit: 0,
                quiescenceDepth: extraDepth);
            var optimized = new SearchEngine().FindBest(
                example.Board, 1, true, Ordering.BestFirst, traceLimit: 1000,
                quiescenceDepth: extraDepth);

            if (plain.Score != optimized.Score || optimized.Nodes > plain.Nodes)
                throw new Exception("Quiescence search mismatch: " + example.Name);

            var measurements = new[]
            {
                (Method: "minimax", Result: plain),
                (Method: "alphabeta", Result: optimized)
            };
            foreach (var measurement in measurements)
            {
                var result = measurement.Result;
                csv.WriteLine($"{example.Name},{PositionAnalysis.Classify(example.Board)},1,{extraDepth},{measurement.Method},{result.Move?.ToString() ?? "PASS"},{result.Score},{result.Nodes},{result.Extensions},{result.Milliseconds:F4}");
            }

            File.WriteAllText(
                Path.Combine(directory, $"quiescence_{example.Name}_{extraDepth}.json"),
                JsonSerializer.Serialize(optimized.Trace, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{example.Name}: {PositionAnalysis.Classify(example.Board)}, extra={extraDepth}, score={optimized.Score}, nodes={optimized.Nodes}, extensions={optimized.Extensions}");
        }
    }
}

sealed record Measurement(string Position, int Depth, string Method, string Ordering, string Evaluation, SearchResult Result);
