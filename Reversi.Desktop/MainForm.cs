using System.Drawing;
using System.Text;
using System.Text.Json;
using Reversi.Core;

namespace Reversi.Desktop;

public sealed class MainForm : Form
{
    Board board = new();
    readonly BoardView view = new();
    readonly ComboBox algo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 148
    };
    readonly ComboBox evaluation = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 148
    };
    readonly ComboBox ordering = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 148
    };
    readonly NumericUpDown depth = new()
    {
        Minimum = 1,
        Maximum = 8,
        Value = 4,
        Width = 70
    };
    readonly Label status = new()
    {
        AutoSize = true
    };
    readonly TextBox details = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10)
    };
    readonly Button ai = new()
    {
        Text = "Хід AI",
        AutoSize = true
    };
    readonly Button restart = new()
    {
        Text = "Нова гра",
        AutoSize = true
    };
    readonly Button compare = new()
    {
        Text = "Порівняти алгоритми",
        AutoSize = true
    };
    readonly Button trace = new()
    {
        Text = "Дерево та alpha/beta",
        AutoSize = true
    };
    SearchResult? last;

    public MainForm()
    {
        Text = "Reversi AI Lab — Черниченко Надія, ТТП-42";
        Width = 1130;
        Height = 800;
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        algo.Items.AddRange(new object[] { "Alpha-Beta", "Minimax" });
        algo.SelectedIndex = 0;
        evaluation.Items.AddRange(Enum.GetNames<EvaluationMode>());
        evaluation.SelectedIndex = 1;
        ordering.Items.AddRange(Enum.GetNames<Ordering>());
        ordering.SelectedIndex = 1;
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(12)
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        Controls.Add(main);
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        main.Controls.Add(left, 0, 0);
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        top.Controls.Add(restart);
        top.Controls.Add(ai);
        top.Controls.Add(new Label
        {
            Text = "Глибина",
            AutoSize = true,
            Padding = new Padding(5, 7, 0, 0)
        });
        top.Controls.Add(depth);
        left.Controls.Add(top, 0, 0);
        view.Dock = DockStyle.Fill;
        view.Board = board;
        view.MoveRequested = MakeMove;
        left.Controls.Add(view, 0, 1);
        left.Controls.Add(status, 0, 2);
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.Controls.Add(right, 1, 0);
        var settings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        settings.Controls.Add(new Label
        {
            Text = "Алгоритм",
            AutoSize = true
        });
        settings.Controls.Add(algo);
        settings.Controls.Add(new Label
        {
            Text = "Оцінка",
            AutoSize = true
        });
        settings.Controls.Add(evaluation);
        settings.Controls.Add(new Label
        {
            Text = "Порядок",
            AutoSize = true
        });
        settings.Controls.Add(ordering);
        right.Controls.Add(settings, 0, 0);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        actions.Controls.Add(compare);
        actions.Controls.Add(trace);
        right.Controls.Add(actions, 0, 1);
        right.Controls.Add(details, 0, 2);
        cancel.Click += (_, _) => searchCancellation?.Cancel();
        top.Controls.Add(cancel);
        restart.Click += (_, _) =>
        {
            board = new Board();
            last = null;
            RefreshBoard();
            details.Clear();
        };
        ai.Click += async (_, _) => await AiMove();
        compare.Click += async (_, _) => await Compare();
        trace.Click += (_, _) => ShowTrace();
        FormClosing += (_, _) => searchCancellation?.Cancel();
        RefreshBoard();
    }

    readonly Button cancel = new()
    {
        Text = "Скасувати",
        AutoSize = true,
        Enabled = false
    };
    CancellationTokenSource? searchCancellation;

    void SetBusy(bool busy)
    {
        ai.Enabled = restart.Enabled = compare.Enabled = trace.Enabled = algo.Enabled = evaluation.Enabled = ordering.Enabled = depth.Enabled = view.Enabled = !busy;
        cancel.Enabled = busy;
        UseWaitCursor = busy;
    }

    Ordering Order => (Ordering)ordering.SelectedIndex;

    EvaluationMode Eval => (EvaluationMode)evaluation.SelectedIndex;

    void RefreshBoard()
    {
        view.Board = board;
        view.Invalidate();
        var m = board.LegalMoves(board.Turn);
        status.Text = $"Чорні: {board.Count(1)}   Білі: {board.Count(-1)}   Хід: {(board.Turn == 1 ? "чорні" : "білі")}   Ходів: {m.Count}" + (board.IsTerminal ? "  |  ГРА ЗАВЕРШЕНА" : "");
    }

    void MakeMove(Move move)
    {
        if (board.IsTerminal || searchCancellation != null)
            return;
        try
        {
            board = board.Play(move);
            last = null;
            details.Clear();
            NormalizePass();
            RefreshBoard();
        }
        catch (ArgumentException)
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    void NormalizePass()
    {
        while (!board.IsTerminal && board.LegalMoves(board.Turn).Count == 0)
            board = board.Play(null);
    }

    async Task AiMove()
    {
        if (board.IsTerminal || searchCancellation != null)
            return;
        NormalizePass();
        var position = board;
        int selectedDepth = (int)depth.Value;
        bool ab = algo.SelectedIndex == 0;
        var order = Order;
        var eval = Eval;
        using var cts = new CancellationTokenSource();
        searchCancellation = cts;
        SetBusy(true);
        details.Text = "Пошук триває… Можна скасувати обчислення.";
        try
        {
            var result = await Task.Run(() => new SearchEngine().FindBest(position, selectedDepth, ab, order, eval, 20000, cts.Token));
            if (IsDisposed || Disposing)
                return;
            cts.Token.ThrowIfCancellationRequested();
            last = result;
            if (last.Move is not null)
                board = position.Play(last.Move.Value);
            NormalizePass();
            RefreshBoard();
            details.Text = $"Хід: {last.Move?.ToString() ?? "PASS"}\r\nОцінка MAX: {last.Score}\r\nВузлів: {last.Nodes}\r\nЛистків: {last.Leaves}\r\nВідтинань: {last.Cutoffs}\r\nЧас: {last.Milliseconds:F2} мс\r\n\r\nОберіть «Дерево та alpha/beta» для трасування.";
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
                details.Text = "Пошук скасовано.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                details.Text = "Помилка пошуку: " + ex.Message;
        }
        finally
        {
            searchCancellation = null;
            if (!IsDisposed && !Disposing)
                SetBusy(false);
        }
    }

    async Task Compare()
    {
        if (searchCancellation != null)
            return;
        if (board.IsTerminal)
        {
            details.Text = "Гра завершена: почніть нову.";
            return;
        }

        var position = board;
        int maxDepth = Math.Min(5, (int)depth.Value);
        var order = Order;
        var eval = Eval;
        using var cts = new CancellationTokenSource();
        searchCancellation = cts;
        SetBusy(true);
        details.Text = "Порівняння триває…";
        try
        {
            string report = await Task.Run(() =>
            {
                var sb = new StringBuilder();
                foreach (int d in Enumerable.Range(1, maxDepth))
                {
                    var plain = new SearchEngine().FindBest(position, d, false, order, eval, 0, cts.Token);
                    var optimized = new SearchEngine().FindBest(position, d, true, order, eval, 0, cts.Token);
                    sb.AppendLine($"Глибина {d}: Minimax {plain.Nodes} вузлів / {plain.Milliseconds:F1} мс; Alpha-Beta {optimized.Nodes} вузлів / {optimized.Milliseconds:F1} мс; відтинань {optimized.Cutoffs}; оцінки {plain.Score}/{optimized.Score}; збіг: {plain.Score == optimized.Score}");
                }

                return sb.ToString();
            });
            if (!IsDisposed && !Disposing)
            {
                cts.Token.ThrowIfCancellationRequested();
                details.Text = report;
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing)
                details.Text = "Порівняння скасовано.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
                details.Text = "Помилка порівняння: " + ex.Message;
        }
        finally
        {
            searchCancellation = null;
            if (!IsDisposed && !Disposing)
                SetBusy(false);
        }
    }

    void ShowTrace()
    {
        if (last == null)
        {
            details.Text = "Спочатку натисніть «Хід AI».";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("ПОСТФІКСНИЙ ОБХІД; parent показує зв'язки дерева. CUT означає відсічене піддерево.");
        foreach (var x in last.Trace.Take(500))
            sb.AppendLine($"{new string(' ', Math.Min(x.Ply, 10) * 2)}#{x.Id} ← {x.Parent?.ToString() ?? "root"} {x.Player} {x.Move,-5} score={x.Score,7} α:{x.AlphaBefore}→{x.AlphaAfter} β:{x.BetaBefore}→{x.BetaAfter} {(x.Cutoff ? "*** CUT ***" : "")} {x.Reason}");
        details.Text = sb.ToString();
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "reversi_tree.json"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            File.WriteAllText(
                dialog.FileName,
                JsonSerializer.Serialize(last.Trace, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class BoardView : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Board Board { get; set; } = new();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<Move>? MoveRequested { get; set; }

    public BoardView()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 75, 53);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int cell = Math.Max(1, Math.Min((Width - 30) / 8, (Height - 30) / 8));
        int ox = (Width - 8 * cell) / 2, oy = (Height - 8 * cell) / 2;
        var allowed = Board.IsTerminal ? new List<Move>() : Board.LegalMoves(Board.Turn);
        using var dark = new SolidBrush(Color.FromArgb(22, 28, 28));
        using var white = new SolidBrush(Color.GhostWhite);
        using var green = new SolidBrush(Color.FromArgb(47, 129, 82));
        using var hint = new SolidBrush(Color.FromArgb(165, 219, 176));
        using var gridPen = new Pen(Color.FromArgb(24, 69, 44), 1);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                int x = ox + c * cell, y = oy + r * cell;
                g.FillRectangle(green, x, y, cell, cell);
                g.DrawRectangle(gridPen, x, y, cell, cell);
                var v = Board.Cells[r * 8 + c];
                if (v != 0)
                {
                    g.FillEllipse(
                        v == 1 ? dark : white,
                        x + cell * .12f,
                        y + cell * .12f,
                        cell * .76f,
                        cell * .76f);
                }
                else if (allowed.Contains(new Move(r, c)))
                {
                    g.FillEllipse(hint, x + cell * .43f, y + cell * .43f, cell * .14f, cell * .14f);
                }
            }

        using var font = new Font("Segoe UI", 9);
        for (int i = 0; i < 8; i++)
        {
            g.DrawString(
                ((char)('A' + i))
                .ToString(),
                font,
                Brushes.White,
                ox + i * cell + cell * .4f,
                oy - 20);
            g.DrawString(
                (i + 1)
                .ToString(),
                font,
                Brushes.White,
                ox - 18,
                oy + i * cell + cell * .35f);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int cell = Math.Max(1, Math.Min((Width - 30) / 8, (Height - 30) / 8));
        int ox = (Width - 8 * cell) / 2, oy = (Height - 8 * cell) / 2;
        int col = (e.X - ox) / cell, row = (e.Y - oy) / cell;
        if (e.X >= ox && e.Y >= oy && row >= 0 && row < 8 && col >= 0 && col < 8)
            MoveRequested?.Invoke(new Move(row, col));
    }
}
