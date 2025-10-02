using MarcelMathNumberQuiz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public enum SquaresMode
{
    Roots,      // √n = ?
    Squares,    // n^2 = ?
    Mix
}

public class SquaresQuiz
{
    private readonly QuizUi _ui;
    public SquaresQuiz(QuizUi ui) { _ui = ui; }

    // Instellingen
    private int _minBase = 1;
    private int _maxBase = 30;
    private int _total = 20;
    private int? _seed = null;
    private bool _timerEnabled = false;
    private int _timerMinutes = 15;

    // UI-selectielijst links (0 = roots, 1 = squares)
    private static readonly string[] TypeLabels =
    {
        "wortels (√n = ?)",
        "kwadraten (n² = ?)"
    };

    public void Run()
    {
        // standaard: beide aan (Mix)
        var chosen = new HashSet<int> { 0, 1 };
        int ptr = 0;

        while (true)
        {
            _ui.Screen("Kwadraten & Wortels – instellingen",
                "↑/↓: nav  Spatie: toggle  A: alles/geen  ←/→: #vragen  R: bereik  S: seed  T: timer  J/K: duur−/+  Enter: start  B: terug");

            // Linkerpaneel: types
            int half = Console.BufferWidth / 2;
            var left = new QuizUi.Panel(2, 3, half - 4, Console.BufferHeight - 8, "Vraagtype");
            _ui.Box(left);

            int visible = left.H - 2;
            int top = Math.Clamp(ptr - visible / 2, 0, Math.Max(0, TypeLabels.Length - visible));
            for (int i = 0; i < visible && top + i < TypeLabels.Length; i++)
            {
                int idx = top + i;
                bool sel = chosen.Contains(idx);
                bool focus = (idx == ptr);
                _ui.Write(left, i, $" {(sel ? "[x]" : "[ ]")} {TypeLabels[idx]}", highlight: focus, padRight: true);
            }

            // Afgeleide modus voor weergave rechts
            var mode = DeriveMode(chosen);

            // Rechterpaneel: instellingen
            var right = new QuizUi.Panel(half + 2, 3, Console.BufferWidth - (half + 4), 18, "Instellingen");
            _ui.Box(right);
            _ui.Write(right, 0, $"Aantal vragen:  {_total,3}  (←/→)");
            _ui.Write(right, 2, $"Modus:  {ModeLabel(mode)}  (links met Spatie wisselen)");
            _ui.Write(right, 4, $"Bereik (R):  n ∈ [{_minBase} .. {_maxBase}]");
            _ui.Write(right, 6, $"Seed (S):  {(_seed.HasValue ? _seed.ToString() : "-")}");
            _ui.Write(right, 8, $"Timer (T):  {(_timerEnabled ? "Aan" : "Uit")}");
            _ui.Write(right, 10, $"Duur (J/K):  {TimeSpan.FromMinutes(Math.Max(1, _timerMinutes)):mm\\:ss}");
            _ui.Write(right, 12, $"Feedback (F in quiz):  {_ui.FeedbackModeLabel()}");
            _ui.Write(right, 16, $"Start: (Enter)    Terug: (B)");

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.B) return;

            if (key.Key == ConsoleKey.UpArrow) ptr = Math.Max(0, ptr - 1);
            else if (key.Key == ConsoleKey.DownArrow) ptr = Math.Min(TypeLabels.Length - 1, ptr + 1);
            else if (key.Key == ConsoleKey.Spacebar)
            {
                if (chosen.Contains(ptr)) chosen.Remove(ptr); else chosen.Add(ptr);
                if (chosen.Count == 0) chosen.Add(ptr); // altijd minstens 1
            }
            else if (key.Key == ConsoleKey.A)
            {
                if (chosen.Count == TypeLabels.Length) chosen.Clear();
                else chosen = new HashSet<int>(Enumerable.Range(0, TypeLabels.Length));
                if (chosen.Count == 0) chosen.Add(0);
            }
            else if (key.Key == ConsoleKey.LeftArrow) _total = Math.Max(1, _total - 1);
            else if (key.Key == ConsoleKey.RightArrow) _total = Math.Min(200, _total + 1);
            else if (key.Key == ConsoleKey.S)
            {
                var s = _ui.Prompt("Geef (optioneel) een integer seed:", _seed?.ToString() ?? "");
                _seed = int.TryParse(s, out int v) ? v : null;
            }
            else if (key.Key == ConsoleKey.R)
            {
                var a = _ui.Prompt("Minimale basis n (bijv. 1):", _minBase.ToString());
                var b = _ui.Prompt("Maximale basis n (bijv. 30):", _maxBase.ToString());
                if (int.TryParse(a, out int amin) && int.TryParse(b, out int bmax) && amin >= 1 && bmax >= amin)
                {
                    _minBase = amin; _maxBase = bmax;
                }
                else _ui.Toast("Ongeldig bereik.");
            }
            else if (key.Key == ConsoleKey.T) _timerEnabled = !_timerEnabled;
            else if (key.Key == ConsoleKey.J) _timerMinutes = Math.Max(1, _timerMinutes - 1);
            else if (key.Key == ConsoleKey.K) _timerMinutes = Math.Min(180, _timerMinutes + 1);
            else if (key.Key == ConsoleKey.Enter)
            {
                StartQuiz(mode);
                return;
            }
        }
    }

    private static SquaresMode DeriveMode(HashSet<int> chosen)
    {
        bool roots = chosen.Contains(0);
        bool squares = chosen.Contains(1);
        if (roots && squares) return SquaresMode.Mix;
        if (roots) return SquaresMode.Roots;
        return SquaresMode.Squares;
    }

    private static string ModeLabel(SquaresMode m) =>
        m switch
        {
            SquaresMode.Roots => "Wortels (√n = ?)",
            SquaresMode.Squares => "Kwadraten (n² = ?)",
            _ => "Mix (wortels + kwadraten)"
        };

    private void StartQuiz(SquaresMode mode)
    {
        var rnd = _seed.HasValue ? new Random(_seed.Value + 9001) : new Random();

        _ui.SetTimer(_timerEnabled, _timerEnabled ? TimeSpan.FromMinutes(_timerMinutes) : (TimeSpan?)null);

        int score = 0;
        var totalAnswerTime = TimeSpan.Zero;
        int answeredCount = 0;

        TimeSpan? RemainingProvider() => _ui.GetRemaining();

        for (int i = 1; i <= _total; i++)
        {
            // kies feitelijke vraagmodus
            var actual = mode == SquaresMode.Mix
                ? (rnd.Next(2) == 0 ? SquaresMode.Roots : SquaresMode.Squares)
                : mode;

            // genereer vraag
            MakeQuestion(rnd, actual, out string qText, out string correct, out List<string> options, out string explanation);

            var sw = Stopwatch.StartNew();

            int sel = _ui.QuestionPane(
                $"Kwadraten & Wortels – Vraag {i}/{_total}",
                qText, options, i, _total, score,
                RemainingProvider, avgSeconds: AverageSeconds(totalAnswerTime, answeredCount));

            if (_ui.LastActionWasTimeout)
            {
                sw.Stop();
                totalAnswerTime += sw.Elapsed; answeredCount++;
                _ui.TimeUpPane();
                _ui.SummaryPane(i - 1 >= 0 ? i - 1 : 0, score, AverageSeconds(totalAnswerTime, answeredCount));
                return;
            }

            sw.Stop();
            totalAnswerTime += sw.Elapsed; answeredCount++;

            bool isCorrect = _ui.Same(options[sel], correct);
            if (isCorrect)
            {
                score++;
                if (_ui.ShouldShowFeedbackOnCorrect())
                {
                    _ui.SplitFeedback($"Kwadraten & Wortels – Vraag {i}/{_total}",
                        qText, options, sel, correct, explanation,
                        i, _total, score, isCorrect: true,
                        RemainingProvider, AverageSeconds(totalAnswerTime, answeredCount));

                    if (_ui.LastActionWasBack) return;
                    if (_ui.LastActionWasTimeout)
                    {
                        _ui.TimeUpPane();
                        _ui.SummaryPane(i, score, AverageSeconds(totalAnswerTime, answeredCount));
                        return;
                    }
                }
            }
            else
            {
                if (_ui.ShouldShowFeedbackOnWrong())
                {
                    _ui.SplitFeedback($"Kwadraten & Wortels – Vraag {i}/{_total}",
                        qText, options, sel, correct, explanation,
                        i, _total, score, isCorrect: false,
                        RemainingProvider, AverageSeconds(totalAnswerTime, answeredCount));

                    if (_ui.LastActionWasBack) return;
                    if (_ui.LastActionWasTimeout)
                    {
                        _ui.TimeUpPane();
                        _ui.SummaryPane(i, score, AverageSeconds(totalAnswerTime, answeredCount));
                        return;
                    }
                }
            }
        }

        _ui.SummaryPane(_total, score, AverageSeconds(totalAnswerTime, answeredCount));
    }

    /* =================== Vraaggenerator =================== */

    private void MakeQuestion(Random rnd, SquaresMode mode,
        out string qText, out string correct, out List<string> options, out string explanation)
    {
        int n = rnd.Next(_minBase, _maxBase + 1);
        if (mode == SquaresMode.Roots)
        {
            int n2 = n * n;
            qText = $"√{n2} = ?";
            correct = n.ToString();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correct };
            void Add(int v) { if (v >= Math.Max(1, _minBase / 2) && v <= _maxBase * 2) set.Add(v.ToString()); }

            Add(n + 1); Add(n - 1); Add(n + 2); Add(n - 2);
            Add(n * 2); Add(Math.Max(1, n / 2));

            options = set.OrderBy(_ => rnd.Next()).Take(4).ToList();
            options = options.OrderBy(_ => rnd.Next()).ToList();

            explanation = $"Omdat {n} × {n} = {n2}, is √{n2} = {n}.";
        }
        else // Squares
        {
            int n2 = n * n;
            qText = $"{n}² = ?";
            correct = n2.ToString();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correct };
            int prev = (n - 1) * (n - 1);
            int next = (n + 1) * (n + 1);

            void Add(int v) { if (v > 0) set.Add(v.ToString()); }

            Add(prev);
            Add(next);
            Add(n2 - (2 * n - 1));
            Add(n2 + (2 * n + 1));

            options = set.OrderBy(_ => rnd.Next()).Take(4).ToList();
            options = options.OrderBy(_ => rnd.Next()).ToList();

            explanation = $"{n} × {n} = {n2}. (Kwadraten veranderen met sprongen van 2n±1.)";
        }

        // Safety: vul tot 4 opties
        while (options.Count < 4)
        {
            int wiggle = rnd.Next(1, 5) * (rnd.Next(2) == 0 ? -1 : 1);
            if (int.TryParse(correct, out int c))
            {
                string cand = (c + wiggle).ToString();
                if (!options.Contains(cand, StringComparer.OrdinalIgnoreCase))
                    options.Add(cand);
            }
            else break;
        }
    }

    private static double AverageSeconds(TimeSpan total, int count)
        => Math.Round(total.TotalSeconds / Math.Max(1, count), 1);
}
