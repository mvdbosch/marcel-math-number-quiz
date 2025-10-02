using MarcelMathNumberQuiz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Console;

/* ===========================================================
   Program entry
   =========================================================== */

class Program
{
    static void Main()
    {
        try
        {
            new App().Run();
        }
        finally
        {
            ResetColor();
            CursorVisible = true;
            Clear();
        }
    }
}

/* ===========================================================
   APP (main menu + routers)
   =========================================================== */

public class App
{
    private readonly QuizUi _ui = new QuizUi();

    public void Run()
    {
        while (true)
        {
            var item = MainMenu();
            if (item == 0) return;

            switch (item)
            {
                case 1:
                    new MathQuiz(_ui).Run();                                  // HfmVitQuestionGenerator
                    break;
                case 2:
                    new SequenceQuiz(_ui).Run();                              // IQSequences
                    break;
                case 3:
                    new BasicMentalQuiz(_ui, new MentalMathGenerator()).Run();// MentalMath
                    break;
                case 4:
                    new SquaresQuiz(_ui).Run();              // Kwadraten & Wortels
                    break;
            }
        }
    }

    private int MainMenu()
    {
        string title = "Marcel's nummer en reken quiz";
        var options = new[]
        {
            "⏵ Rekensommen (breuken, %, verhoudingen)",
            "⏵ Nummerreeksen (IQSequences)",
            "⏵ Hoofdrekenen (optel/af/×/÷/macht/wortel)",
            "⏵ Kwadraten & Wortels (n², √n)",
            "⏻ Afsluiten"
        };
        int sel = 0;

        // breedte van de ASCII art (ongeveer) om het menu rechts netjes te plaatsen
        const int artWidth = 54;

        while (true)
        {
            _ui.Screen(title, "↑/↓: kiezen   Enter: openen   Q/Esc: afsluiten   (F: feedback-stand in quiz)");
            var panel = new QuizUi.Panel(2, 2, BufferWidth - 4, BufferHeight - 4, "Hoofdmenu");
            _ui.Box(panel);

            // 1) ASCII-smiley links
            int artX = panel.X + 3;
            int artY = panel.Y + 2;
            _ui.DrawSmiley(artX, artY);

            // 2) Opties rechts naast de art
            int rightStartCol = artX + artWidth + 2;
            int firstRow = 3; // binnen panel
            for (int i = 0; i < options.Length; i++)
            {
                bool focus = i == sel;
                _ui.WriteAt(rightStartCol, panel.Y + firstRow + i * 2,
                            $"  {options[i]}",
                            focus ? ConsoleColor.White : ConsoleColor.White,
                            focus ? ConsoleColor.DarkBlue : (ConsoleColor?)null,
                            padWidth: panel.X + panel.W - 3 - rightStartCol);
            }

            var key = ReadKey(true);
            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) return 0;
            if (key.Key == ConsoleKey.UpArrow) sel = Math.Max(0, sel - 1);
            else if (key.Key == ConsoleKey.DownArrow) sel = Math.Min(options.Length - 1, sel + 1);
            else if (key.Key == ConsoleKey.Enter)
            {
                if (sel == options.Length - 1) return 0;
                return sel + 1;
            }
        }
    }
}

/* ===========================================================
   QUIZ: REKENSOMMEN (HFMvit-stijl)
   =========================================================== */

public class MathQuiz
{
    private readonly QuizUi _ui;
    public MathQuiz(QuizUi ui) { _ui = ui; }

    public void Run()
    {
        var allTypes = Enum.GetValues(typeof(ProblemType)).Cast<ProblemType>().ToList();
        var chosen = new HashSet<int>(Enumerable.Range(0, allTypes.Count)); // default: alles
        int total = 10;
        int? seed = null;
        int ptr = 0;

        // Timer settings (default uit, 15 minuten)
        bool timerEnabled = false;
        int timerMinutes = 15;

        while (true)
        {
            _ui.Screen("Rekensommen – categorieën & instellingen",
                       "↑/↓: nav  Spatie: toggle  ←/→: #vragen  S: seed  A: alles/geen  T: timer aan/uit  J/K: duur−/+  Enter: start  B: terug");

            var left = new QuizUi.Panel(2, 3, (BufferWidth / 2) - 3, BufferHeight - 13, "Categorieën");
            _ui.Box(left);

            int visible = left.H - 2;
            int top = Math.Clamp(ptr - visible / 2, 0, Math.Max(0, allTypes.Count - visible));
            for (int i = 0; i < visible && top + i < allTypes.Count; i++)
            {
                bool sel = chosen.Contains(top + i);
                bool focus = (top + i == ptr);
                _ui.Write(left, i, $" {(sel ? "[x]" : "[ ]")} {allTypes[top + i]}", highlight: focus, padRight: true);
            }

            var right = new QuizUi.Panel((BufferWidth / 2) + 1, 3, BufferWidth - (BufferWidth / 2) - 3, 15, "Instellingen");
            _ui.Box(right);
            _ui.Write(right, 0, $"Aantal vragen:  {total,3}  (←/→)");
            _ui.Write(right, 2, $"Seed: {(seed.HasValue ? seed.ToString() : "-")}  (S)");
            _ui.Write(right, 4, $"Alles/geen: (A)");
            _ui.Write(right, 6, $"Timer (T): {(timerEnabled ? "Aan" : "Uit")}");
            _ui.Write(right, 8, $"Duur (J/K): {TimeSpan.FromMinutes(Math.Max(1, timerMinutes)):mm\\:ss}");
            _ui.Write(right, 10, $"Feedback (F tijdens quiz): {_ui.FeedbackModeLabel()}");
            _ui.Write(right, 12, $"Start: (Enter)   Terug: (B)");

            var key = ReadKey(true);
            if (key.Key == ConsoleKey.B) return;

            if (key.Key == ConsoleKey.UpArrow) ptr = Math.Max(0, ptr - 1);
            else if (key.Key == ConsoleKey.DownArrow) ptr = Math.Min(allTypes.Count - 1, ptr + 1);
            else if (key.Key == ConsoleKey.Spacebar)
            {
                if (chosen.Contains(ptr)) chosen.Remove(ptr); else chosen.Add(ptr);
            }
            else if (key.Key == ConsoleKey.A)
            {
                if (chosen.Count == allTypes.Count) chosen.Clear();
                else chosen = new HashSet<int>(Enumerable.Range(0, allTypes.Count));
            }
            else if (key.Key == ConsoleKey.LeftArrow) total = Math.Max(1, total - 1);
            else if (key.Key == ConsoleKey.RightArrow) total = Math.Min(200, total + 1);
            else if (key.Key == ConsoleKey.S)
            {
                var s = _ui.Prompt("Geef (optioneel) een integer seed:", seed?.ToString() ?? "");
                seed = int.TryParse(s, out int v) ? v : null;
            }
            else if (key.Key == ConsoleKey.T) timerEnabled = !timerEnabled;
            else if (key.Key == ConsoleKey.J) timerMinutes = Math.Max(1, timerMinutes - 1);
            else if (key.Key == ConsoleKey.K) timerMinutes = Math.Min(180, timerMinutes + 1);
            else if (key.Key == ConsoleKey.Enter)
            {
                if (chosen.Count == 0) { _ui.Toast("Kies eerst tenminste één categorie."); continue; }
                StartQuiz(allTypes.Where((t, i) => chosen.Contains(i)).ToList(), total, seed, timerEnabled, timerMinutes);
                return;
            }
        }
    }

    private void StartQuiz(List<ProblemType> types, int total, int? seed, bool timerEnabled, int timerMinutes)
    {
        var gen = seed.HasValue ? new HfmVitQuestionGenerator(seed) : new HfmVitQuestionGenerator();
        var rnd = seed.HasValue ? new Random(seed.Value + 1337) : new Random();

        _ui.SetTimer(timerEnabled, timerEnabled ? TimeSpan.FromMinutes(timerMinutes) : (TimeSpan?)null);

        int score = 0;
        var totalAnswerTime = TimeSpan.Zero;
        int answeredCount = 0;

        TimeSpan? RemainingProvider() => _ui.GetRemaining();

        for (int i = 1; i <= total; i++)
        {
            var q = gen.Generate(types[rnd.Next(types.Count)]);
            var options = q.Options;

            var sw = Stopwatch.StartNew();

            // Geen q.Type meer in de titel (niet verklappen)
            int sel = _ui.QuestionPane(
                $"Rekensommen – Vraag {i}/{total}",
                q.QuestionText, options, i, total, score,
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

            bool isCorrect = _ui.Same(options[sel], q.CorrectAnswer);
            if (isCorrect)
            {
                score++;
                if (_ui.ShouldShowFeedbackOnCorrect())
                {
                    _ui.SplitFeedback($"Rekensommen – Vraag {i}/{total}",
                        q.QuestionText, options, sel, q.CorrectAnswer, q.Explanation,
                        i, total, score, isCorrect: true,
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
                    _ui.SplitFeedback($"Rekensommen – Vraag {i}/{total}",
                        q.QuestionText, options, sel, q.CorrectAnswer, q.Explanation,
                        i, total, score, isCorrect: false,
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

        _ui.SummaryPane(total, score, AverageSeconds(totalAnswerTime, answeredCount));
    }

    private static double AverageSeconds(TimeSpan total, int count)
        => Math.Round(total.TotalSeconds / Math.Max(1, count), 1);
}

/* ===========================================================
   QUIZ: NUMMERREEKSEN (IQSequences)
   =========================================================== */

public class SequenceQuiz
{
    private readonly QuizUi _ui;
    public SequenceQuiz(QuizUi ui) { _ui = ui; }

    // Available type keys from IQSequences (adjust if you trimmed/renamed any)
    private static readonly string[] SeqTypeKeys =
    {
        "arithmetic","geometric","increasing_diffs","alt_add_sub","alt_doubling",
        "interleaved_two_aps","interleaved_mixed_poly","fibonacci_like","primes",
        "polygonal","quadratic","repeating_diff","power_steps","digit_product","poly_k"
    };

    public void Run()
    {
        var chosen = new HashSet<int>(Enumerable.Range(0, SeqTypeKeys.Length)); // default: all
        string difficulty = "medium";    // easy | medium | hard
        string askMode = "both";      // next | missing | both
        int total = 10;
        int? seed = null;
        int ptr = 0;

        bool timerEnabled = false;
        int timerMinutes = 15;

        while (true)
        {
            _ui.Screen("Nummerreeksen – categorieën & instellingen",
                       "↑/↓: nav  Spatie: toggle  ←/→: #vragen  D: moeilijkheid  M: vraagtype  S: seed  A: alles/geen  T: timer  J/K: duur−/+  Enter: start  B: terug");

            var left = new QuizUi.Panel(2, 3, (Console.BufferWidth / 2) - 3, Console.BufferHeight - 15, "Reeks-typen");
            _ui.Box(left);

            int visible = left.H - 2;
            int top = Math.Clamp(ptr - visible / 2, 0, Math.Max(0, SeqTypeKeys.Length - visible));
            for (int i = 0; i < visible && top + i < SeqTypeKeys.Length; i++)
            {
                bool sel = chosen.Contains(top + i);
                bool focus = (top + i == ptr);
                _ui.Write(left, i, $" {(sel ? "[x]" : "[ ]")} {SeqTypeKeys[top + i]}", highlight: focus, padRight: true);
            }

            var right = new QuizUi.Panel((Console.BufferWidth / 2) + 1, 3, Console.BufferWidth - (Console.BufferWidth / 2) - 3, 17, "Instellingen");
            _ui.Box(right);
            _ui.Write(right, 0, $"Aantal vragen:  {total,3}  (←/→)");
            _ui.Write(right, 2, $"Moeilijkheid (D): {difficulty}");
            _ui.Write(right, 4, $"Vraagtype (M): {askMode}");
            _ui.Write(right, 6, $"Seed: {(seed.HasValue ? seed.ToString() : "-")}  (S)");
            _ui.Write(right, 8, $"Alles/geen: (A)");
            _ui.Write(right, 10, $"Timer (T): {(timerEnabled ? "Aan" : "Uit")}");
            _ui.Write(right, 12, $"Duur (J/K): {TimeSpan.FromMinutes(Math.Max(1, timerMinutes)):mm\\:ss}");
            _ui.Write(right, 14, $"Feedback (F tijdens quiz): {_ui.FeedbackModeLabel()}");
            _ui.Write(right, 16, $"Start: (Enter)   Terug: (B)");

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.B) return;

            if (key.Key == ConsoleKey.UpArrow) ptr = Math.Max(0, ptr - 1);
            else if (key.Key == ConsoleKey.DownArrow) ptr = Math.Min(SeqTypeKeys.Length - 1, ptr + 1);
            else if (key.Key == ConsoleKey.Spacebar)
            {
                if (chosen.Contains(ptr)) chosen.Remove(ptr); else chosen.Add(ptr);
            }
            else if (key.Key == ConsoleKey.A)
            {
                if (chosen.Count == SeqTypeKeys.Length) chosen.Clear();
                else chosen = new HashSet<int>(Enumerable.Range(0, SeqTypeKeys.Length));
            }
            else if (key.Key == ConsoleKey.LeftArrow) total = Math.Max(1, total - 1);
            else if (key.Key == ConsoleKey.RightArrow) total = Math.Min(200, total + 1);
            else if (key.Key == ConsoleKey.D)
            {
                difficulty = difficulty switch { "easy" => "medium", "medium" => "hard", _ => "easy" };
            }
            else if (key.Key == ConsoleKey.M)
            {
                askMode = askMode switch { "next" => "missing", "missing" => "both", _ => "next" };
            }
            else if (key.Key == ConsoleKey.S)
            {
                var s = _ui.Prompt("Geef (optioneel) een integer seed:", seed?.ToString() ?? "");
                seed = int.TryParse(s, out int v) ? v : null;
            }
            else if (key.Key == ConsoleKey.T) timerEnabled = !timerEnabled;
            else if (key.Key == ConsoleKey.J) timerMinutes = Math.Max(1, timerMinutes - 1);
            else if (key.Key == ConsoleKey.K) timerMinutes = Math.Min(180, timerMinutes + 1);
            else if (key.Key == ConsoleKey.Enter)
            {
                if (chosen.Count == 0) { _ui.Toast("Kies eerst tenminste één type."); continue; }
                StartQuiz(chosen.Select(i => SeqTypeKeys[i]).ToList(), total, seed, difficulty, askMode, timerEnabled, timerMinutes);
                return;
            }
        }
    }

    private void StartQuiz(
        List<string> typeKeys, int total, int? seed,
        string difficulty, string askMode,
        bool timerEnabled, int timerMinutes)
    {
        var rnd = seed.HasValue ? new Random(seed.Value + 4242) : new Random();

        _ui.SetTimer(timerEnabled, timerEnabled ? TimeSpan.FromMinutes(timerMinutes) : (TimeSpan?)null);

        int score = 0;
        var totalAnswerTime = TimeSpan.Zero;
        int answeredCount = 0;

        TimeSpan? RemainingProvider() => _ui.GetRemaining();

        for (int i = 1; i <= total; i++)
        {
            // pick a type key
            var typeKey = typeKeys[rnd.Next(typeKeys.Count)];

            // ✅ Correct API from IQSequences.cs
            // We pass a single selected type via the `types` parameter
            var gr = IQSequences.GenerateNumberSequence(
                difficulty: difficulty,
                types: new[] { typeKey },
                seed: seed,
                askMode: askMode
            );

            // Build four options (includes the correct one) using IQSequences helper
            var numericOptions = IQSequences.MakeChoices(gr, k: 4, seed: seed);
            var options = numericOptions.Select(v => v.ToString()).ToList();

            // Combine explanation fields
            string expl = (string.IsNullOrWhiteSpace(gr.Uitleg) ? "" : gr.Uitleg)
                        + (string.IsNullOrWhiteSpace(gr.Herken) ? "" : " | " + gr.Herken)
                        + (string.IsNullOrWhiteSpace(gr.Voorbeeld) ? "" : " | " + gr.Voorbeeld);

            var sw = Stopwatch.StartNew();

            int sel = _ui.QuestionPane(
                $"Nummerreeksen – Vraag {i}/{total}",
                gr.Vraag, options, i, total, score,
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

            bool isCorrect = _ui.Same(options[sel], gr.Antwoord.ToString());
            if (isCorrect)
            {
                score++;
                if (_ui.ShouldShowFeedbackOnCorrect())
                {
                    _ui.SplitFeedback($"Nummerreeksen – Vraag {i}/{total}",
                        gr.Vraag, options, sel, gr.Antwoord.ToString(), expl,
                        i, total, score, isCorrect: true,
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
                    _ui.SplitFeedback($"Nummerreeksen – Vraag {i}/{total}",
                        gr.Vraag, options, sel, gr.Antwoord.ToString(), expl,
                        i, total, score, isCorrect: false,
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

        _ui.SummaryPane(total, score, AverageSeconds(totalAnswerTime, answeredCount));
    }

    private static double AverageSeconds(TimeSpan total, int count)
        => Math.Round(total.TotalSeconds / Math.Max(1, count), 1);
}

/* ===========================================================
   QUIZ: Hoofdrekenen (MentalMath)
   =========================================================== */

public class BasicMentalQuiz
{
    private readonly QuizUi _ui;
    private readonly IMentalQuestionProvider _provider;

    public BasicMentalQuiz(QuizUi ui, IMentalQuestionProvider provider)
    {
        _ui = ui;
        _provider = provider;
    }

    public void Run()
    {
        var types = Enum.GetValues(typeof(MentalType)).Cast<MentalType>().ToList();
        var chosen = new HashSet<int>(Enumerable.Range(0, types.Count));
        string difficulty = "medium";
        int total = 10;
        int? seed = null;
        int ptr = 0;

        bool timerEnabled = false;
        int timerMinutes = 15;

        while (true)
        {
            _ui.Screen("Hoofdrekenen – categorieën & instellingen",
                       "↑/↓: nav  Spatie: toggle  ←/→: #vragen  D: moeilijkheid  S: seed  A: alles/geen  T: timer  J/K: duur−/+  Enter: start  B: terug");

            var left = new QuizUi.Panel(2, 3, (BufferWidth / 2) - 3, BufferHeight - 15, "Bewerkingen");
            _ui.Box(left);

            int visible = left.H - 2;
            int top = Math.Clamp(ptr - visible / 2, 0, Math.Max(0, types.Count - visible));
            for (int i = 0; i < visible && top + i < types.Count; i++)
            {
                bool sel = chosen.Contains(top + i);
                bool focus = (top + i == ptr);
                _ui.Write(left, i, $" {(sel ? "[x]" : "[ ]")} {types[top + i]}", highlight: focus, padRight: true);
            }

            var right = new QuizUi.Panel((BufferWidth / 2) + 1, 3, BufferWidth - (BufferWidth / 2) - 3, 17, "Instellingen");
            _ui.Box(right);
            _ui.Write(right, 0, $"Aantal vragen:  {total,3}  (←/→)");
            _ui.Write(right, 2, $"Moeilijkheid (D): {difficulty}");
            _ui.Write(right, 4, $"Seed: {(seed.HasValue ? seed.ToString() : "-")}  (S)");
            _ui.Write(right, 6, $"Alles/geen: (A)");
            _ui.Write(right, 8, $"Timer (T): {(timerEnabled ? "Aan" : "Uit")}");
            _ui.Write(right, 10, $"Duur (J/K): {TimeSpan.FromMinutes(Math.Max(1, timerMinutes)):mm\\:ss}");
            _ui.Write(right, 12, $"Feedback (F tijdens quiz): {_ui.FeedbackModeLabel()}");
            _ui.Write(right, 14, $"Start: (Enter)   Terug: (B)");

            var key = ReadKey(true);
            if (key.Key == ConsoleKey.B) return;

            if (key.Key == ConsoleKey.UpArrow) ptr = Math.Max(0, ptr - 1);
            else if (key.Key == ConsoleKey.DownArrow) ptr = Math.Min(types.Count - 1, ptr + 1);
            else if (key.Key == ConsoleKey.Spacebar)
            {
                if (chosen.Contains(ptr)) chosen.Remove(ptr); else chosen.Add(ptr);
            }
            else if (key.Key == ConsoleKey.A)
            {
                if (chosen.Count == types.Count) chosen.Clear();
                else chosen = new HashSet<int>(Enumerable.Range(0, types.Count));
            }
            else if (key.Key == ConsoleKey.LeftArrow) total = Math.Max(1, total - 1);
            else if (key.Key == ConsoleKey.RightArrow) total = Math.Min(200, total + 1);
            else if (key.Key == ConsoleKey.D)
            {
                difficulty = difficulty switch { "easy" => "medium", "medium" => "hard", _ => "easy" };
            }
            else if (key.Key == ConsoleKey.S)
            {
                var s = _ui.Prompt("Geef (optioneel) een integer seed:", seed?.ToString() ?? "");
                seed = int.TryParse(s, out int v) ? v : null;
            }
            else if (key.Key == ConsoleKey.T) timerEnabled = !timerEnabled;
            else if (key.Key == ConsoleKey.J) timerMinutes = Math.Max(1, timerMinutes - 1);
            else if (key.Key == ConsoleKey.K) timerMinutes = Math.Min(180, timerMinutes + 1);
            else if (key.Key == ConsoleKey.Enter)
            {
                if (chosen.Count == 0) { _ui.Toast("Kies eerst tenminste één bewerking."); continue; }
                StartQuiz(types.Where((t, i) => chosen.Contains(i)).ToList(), total, seed, difficulty, timerEnabled, timerMinutes);
                return;
            }
        }
    }

    private void StartQuiz(List<MentalType> types, int total, int? seed, string difficulty, bool timerEnabled, int timerMinutes)
    {
        var rnd = seed.HasValue ? new Random(seed.Value + 777) : new Random();
        var provider = _provider;

        _ui.SetTimer(timerEnabled, timerEnabled ? TimeSpan.FromMinutes(timerMinutes) : (TimeSpan?)null);

        int score = 0;
        var totalAnswerTime = TimeSpan.Zero;
        int answeredCount = 0;

        TimeSpan? RemainingProvider() => _ui.GetRemaining();

        for (int i = 1; i <= total; i++)
        {
            var type = types[rnd.Next(types.Count)];
            var q = provider.Generate(difficulty, type);
            var options = q.Options;

            var sw = Stopwatch.StartNew();

            int sel = _ui.QuestionPane(
                $"Hoofdrekenen – Vraag {i}/{total}",
                q.QuestionText, options, i, total, score,
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

            bool isCorrect = _ui.Same(options[sel], q.CorrectAnswer);
            if (isCorrect)
            {
                score++;
                if (_ui.ShouldShowFeedbackOnCorrect())
                {
                    _ui.SplitFeedback($"Hoofdrekenen – Vraag {i}/{total}",
                        q.QuestionText, options, sel, q.CorrectAnswer, q.Explanation,
                        i, total, score, isCorrect: true,
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
                    _ui.SplitFeedback($"Hoofdrekenen – Vraag {i}/{total}",
                        q.QuestionText, options, sel, q.CorrectAnswer, q.Explanation,
                        i, total, score, isCorrect: false,
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

        _ui.SummaryPane(total, score, AverageSeconds(totalAnswerTime, answeredCount));
    }

    private static double AverageSeconds(TimeSpan total, int count)
        => Math.Round(total.TotalSeconds / Math.Max(1, count), 1);
}
