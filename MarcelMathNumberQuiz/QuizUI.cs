using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MarcelMathNumberQuiz
{

    public enum FeedbackMode
    {
        Off = 0,       // geen feedback
        WrongOnly = 1, // alleen bij fout (default)
        All = 2        // goed + fout
    }

    public class QuizUi
    {
        public QuizUi()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Marcel's nummer en reken quiz";
            Console.CursorVisible = false;
        }

        /* =======================
           Globale UI status
           ======================= */

        // Feedback mode
        public FeedbackMode Feedback { get; private set; } = FeedbackMode.WrongOnly;
        public bool ShouldShowFeedbackOnCorrect() => Feedback == FeedbackMode.All;
        public bool ShouldShowFeedbackOnWrong() => Feedback != FeedbackMode.Off;
        public string FeedbackModeLabel()
            => Feedback switch
            {
                FeedbackMode.Off => "Uit",
                FeedbackMode.WrongOnly => "Fout",
                FeedbackMode.All => "Alles",
                _ => "Fout"
            };

        // Navigatie flags
        public bool LastActionWasBack { get; private set; } = false;
        public bool LastActionWasTimeout { get; private set; } = false;

        // Timer
        private bool _timerEnabled = false;
        private DateTime? _deadlineUtc = null;

        public void SetTimer(bool enabled, TimeSpan? total)
        {
            _timerEnabled = enabled;
            _deadlineUtc = (enabled && total.HasValue) ? DateTime.UtcNow + total.Value : null;
            LastActionWasTimeout = false;
        }

        public TimeSpan? GetRemaining()
        {
            if (!_timerEnabled || _deadlineUtc == null) return null;
            var rem = _deadlineUtc.Value - DateTime.UtcNow;
            if (rem <= TimeSpan.Zero) return TimeSpan.Zero;
            return rem;
        }

        /* =======================
           Thema & primitieven
           ======================= */

        public struct Panel { public int X, Y, W, H; public string Title; public Panel(int x, int y, int w, int h, string t) { X = x; Y = y; W = w; H = h; Title = t ?? ""; } }

        // blauw / wit thema
        private readonly ConsoleColor BorderTitleColor = ConsoleColor.White;
        private readonly ConsoleColor TextColor = ConsoleColor.White;
        private readonly ConsoleColor HintBg = ConsoleColor.DarkBlue;
        private readonly ConsoleColor HintFg = ConsoleColor.White;
        private readonly ConsoleColor HighlightBg = ConsoleColor.DarkBlue;
        private readonly ConsoleColor HighlightFg = ConsoleColor.White;

        public void Screen(string title, string hint)
        {
            Console.Clear();
            DrawBox(0, 0, Console.BufferWidth, Console.BufferHeight, title, BorderTitleColor);

            // hintbar onder
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            Console.BackgroundColor = HintBg; Console.ForegroundColor = HintFg;
            Console.Write(hint.PadRight(Console.BufferWidth));
            Console.ForegroundColor = oldF; Console.BackgroundColor = oldB;
        }

        public void Box(Panel p) => DrawBox(p.X, p.Y, p.W, p.H, p.Title, BorderTitleColor);
        public void Box(Panel p, ConsoleColor titleColor) => DrawBox(p.X, p.Y, p.W, p.H, p.Title, titleColor);

        public void DrawBox(int x, int y, int w, int h, string title, ConsoleColor titleColor)
        {
            var horiz = '─'; var vert = '│';
            var tl = '┌'; var tr = '┐'; var bl = '└'; var br = '┘';

            Set(x, y);
            Console.Write(tl); Console.Write(new string(horiz, Math.Max(0, w - 2))); Console.Write(tr);
            for (int i = 1; i < h - 1; i++)
            {
                Set(x, y + i);
                Console.Write(vert);
                Console.Write(new string(' ', Math.Max(0, w - 2)));
                Console.Write(vert);
            }
            Set(x, y + h - 1);
            Console.Write(bl); Console.Write(new string(horiz, Math.Max(0, w - 2))); Console.Write(br);

            if (!string.IsNullOrWhiteSpace(title))
            {
                var old = Console.ForegroundColor;
                Console.ForegroundColor = titleColor;
                Set(x + 2, y); Console.Write($" {title} ");
                Console.ForegroundColor = old;
            }
        }

        public void Write(Panel p, int row, string text, bool highlight = false, bool padRight = false)
        {
            int maxLen = Math.Max(1, p.W - 4);
            string s = text.Length > maxLen ? text[..maxLen] : text;
            if (padRight) s = s.PadRight(maxLen);

            Set(p.X + 2, p.Y + 1 + row);

            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            Console.ForegroundColor = TextColor;
            if (highlight) { Console.BackgroundColor = HighlightBg; Console.ForegroundColor = HighlightFg; }
            Console.Write(s);
            Console.BackgroundColor = oldB; Console.ForegroundColor = oldF;
        }

        public void WriteAt(int x, int y, string text, ConsoleColor? fg = null, ConsoleColor? bg = null, int padWidth = 0)
        {
            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            if (bg.HasValue) Console.BackgroundColor = bg.Value;
            if (fg.HasValue) Console.ForegroundColor = fg.Value;

            if (padWidth > 0 && text.Length < padWidth)
                text = text + new string(' ', padWidth - text.Length);

            Set(x, y);
            Console.Write(text);

            Console.ForegroundColor = oldF; Console.BackgroundColor = oldB;
        }

        public void WriteColored(Panel p, int row, string text, ConsoleColor fg, ConsoleColor bg, bool padRight = false)
        {
            int maxLen = Math.Max(1, p.W - 4);
            string s = text.Length > maxLen ? text[..maxLen] : text;
            if (padRight) s = s.PadRight(maxLen);

            Set(p.X + 2, p.Y + 1 + row);
            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            Console.ForegroundColor = fg; Console.BackgroundColor = bg;
            Console.Write(s);
            Console.ForegroundColor = oldF; Console.BackgroundColor = oldB;
        }

        public void WriteWrapped(Panel p, int startRow, string text)
        {
            string cleaned = (text ?? string.Empty).Replace("\r", "");
            var paragraphs = cleaned.Split('\n');

            int row = startRow;
            int width = Math.Max(1, p.W - 4);

            foreach (var para in paragraphs)
            {
                foreach (var line in WrapLine(para, width))
                {
                    if (row >= p.H - 2) return;
                    Write(p, row, line, padRight: true);
                    row++;
                }
                if (row < p.H - 2 && para != paragraphs[^1])
                {
                    Write(p, row, "", padRight: true);
                    row++;
                }
            }
        }

        private static IEnumerable<string> WrapLine(string text, int width)
        {
            var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = "";
            foreach (var w in words)
            {
                if (line.Length == 0) { line = w; continue; }
                if (line.Length + 1 + w.Length > width) { yield return line; line = w; }
                else line += " " + w;
            }
            if (line.Length > 0) yield return line;
        }

        public string Prompt(string prompt, string defaultValue = "")
        {
            Console.CursorVisible = true;
            var pan = new Panel(6, 5, Console.BufferWidth - 12, 6, "Invoer");
            Box(pan);
            Write(pan, 0, prompt, padRight: true);
            Set(pan.X + 2, pan.Y + 3);
            Console.BackgroundColor = HighlightBg; Console.ForegroundColor = HighlightFg;
            string init = defaultValue ?? "";
            Console.Write(init + new string(' ', Math.Max(0, pan.W - 6 - init.Length)));
            Set(pan.X + 2, pan.Y + 3);
            string line = Console.ReadLine() ?? "";
            Console.BackgroundColor = ConsoleColor.Black; Console.ForegroundColor = TextColor;
            Console.CursorVisible = false;
            return string.IsNullOrWhiteSpace(line) ? defaultValue : line;
        }

        public void Toast(string msg)
        {
            var x = Math.Max(0, (Console.BufferWidth - msg.Length) / 2 - 2);
            var y = 1;
            Set(x, y);
            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            Console.BackgroundColor = HighlightBg; Console.ForegroundColor = HighlightFg;
            Console.Write(" " + msg + " ");
            Console.ForegroundColor = oldF; Console.BackgroundColor = oldB;
            System.Threading.Thread.Sleep(900);
        }

        public static string Bar(int percent, int width)
        {
            percent = Math.Clamp(percent, 0, 100);
            int filled = (int)Math.Round(width * (percent / 100.0));
            return "[" + new string('█', filled) + new string('░', Math.Max(0, width - filled)) + "]";
        }

        public bool Same(string a, string b)
        {
            static string N(string x) => (x ?? "").Trim().Replace(" ", "").Replace(",", ".").ToLowerInvariant();
            return N(a) == N(b);
        }

        private static void Set(int x, int y)
        {
            x = Math.Clamp(x, 0, Math.Max(0, Console.BufferWidth - 1));
            y = Math.Clamp(y, 0, Math.Max(0, Console.BufferHeight - 1));
            Console.SetCursorPosition(x, y);
        }

        private void CycleFeedbackMode()
        {
            Feedback = (FeedbackMode)(((int)Feedback + 1) % 3);
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        }

        // Footer rechts-onder (hintbalk)
        public void DrawFooterRight(string text)
        {
            var oldF = Console.ForegroundColor; var oldB = Console.BackgroundColor;
            Console.BackgroundColor = HintBg; Console.ForegroundColor = HintFg;

            int x = Math.Max(0, Console.BufferWidth - text.Length);
            int y = Console.BufferHeight - 1;
            Set(x, y);
            Console.Write(text);

            Console.ForegroundColor = oldF; Console.BackgroundColor = oldB;
        }

        /* =======================
           Vraagpaneel
           ======================= */

        private int _answerPointer = 0;

        public int QuestionPane(
            string title,
            string question,
            IReadOnlyList<string> options,
            int index, int total, int score,
            Func<TimeSpan?> getRemaining,
            double avgSeconds)
        {
            _answerPointer = Math.Clamp(_answerPointer, 0, options.Count - 1);

            while (true)
            {
                Screen(title, "↑/↓ of A–D: kiezen   Enter: bevestigen   F: feedback-stand   Q/Esc: menu");

                var head = new Panel(2, 3, Console.BufferWidth - 4, 3, "");
                Box(head);
                int pct = (int)Math.Round(100.0 * (index - 1) / Math.Max(1, total));
                var rem = getRemaining?.Invoke();
                string timeStr = rem.HasValue ? $"  Tijd: {FormatTime(rem.Value)}" : "";
                Write(head, 0, $"Score: {score}/{index - 1}   Voortgang: {Bar(pct, 36)} {pct,3}%{timeStr}");

                var q = new Panel(2, 7, Console.BufferWidth - 4, 6, "Vraag");
                Box(q);
                WriteWrapped(q, 0, question);

                var a = new Panel(2, 14, Console.BufferWidth - 4, 9, "Antwoorden");
                Box(a);

                var letters = new[] { "A", "B", "C", "D" };
                for (int i = 0; i < options.Count; i++)
                {
                    bool focus = (i == _answerPointer);
                    Write(a, i, $"  {letters[i]})  {options[i]}", highlight: focus, padRight: true);
                }

                DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");

                // input-loop met live timer
                while (true)
                {
                    var r = getRemaining?.Invoke();
                    if (r.HasValue && r.Value <= TimeSpan.Zero)
                    {
                        LastActionWasTimeout = true;
                        return _answerPointer;
                    }

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) { LastActionWasBack = true; return _answerPointer; }
                        if (key.Key == ConsoleKey.UpArrow) { _answerPointer = Math.Max(0, _answerPointer - 1); break; }
                        if (key.Key == ConsoleKey.DownArrow) { _answerPointer = Math.Min(options.Count - 1, _answerPointer + 1); break; }
                        if (key.Key == ConsoleKey.F)
                        {
                            CycleFeedbackMode();
                            Toast($"Feedback-stand: {FeedbackModeLabel()}");
                            DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");
                            break;
                        }
                        if (key.Key == ConsoleKey.Enter) { LastActionWasBack = false; return _answerPointer; }
                        if (key.Key >= ConsoleKey.A && key.Key <= ConsoleKey.D)
                        {
                            int idx = key.Key - ConsoleKey.A;
                            if (idx >= 0 && idx < options.Count) { LastActionWasBack = false; return idx; }
                        }
                    }
                    else
                    {
                        if (r.HasValue)
                        {
                            Box(head);
                            int pct2 = (int)Math.Round(100.0 * (index - 1) / Math.Max(1, total));
                            string timeStr2 = $"  Tijd: {FormatTime(r.Value)}";
                            Write(head, 0, $"Score: {score}/{index - 1}   Voortgang: {Bar(pct2, 36)} {pct2,3}%{timeStr2}");
                            DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");
                        }
                        System.Threading.Thread.Sleep(120);
                    }
                }
            }
        }

        /* =======================
           Split-screen feedback
           ======================= */

        public void SplitFeedback(
            string title,
            string question,
            IReadOnlyList<string> options,
            int selectedIndex,
            string correctText,
            string explanation,
            int index, int total, int score,
            bool isCorrect,
            Func<TimeSpan?> getRemaining,
            double avgSeconds)
        {
            Screen(title, "Enter: volgende   Q/Esc: terug naar menu   F: feedback-stand");

            var head = new Panel(2, 3, Console.BufferWidth - 4, 3, "");
            Box(head);
            int pct = (int)Math.Round(100.0 * (index - 1) / Math.Max(1, total));
            var rem = getRemaining?.Invoke();
            string timeStr = rem.HasValue ? $"  Tijd: {FormatTime(rem.Value)}" : "";
            Write(head, 0, $"Score: {score}/{index - 1}   Voortgang: {Bar(pct, 36)} {pct,3}%{timeStr}");

            int split = (Console.BufferWidth - 6) / 2;
            int leftX = 2;
            int rightX = leftX + split + 2;
            int leftW = split;
            int rightW = Console.BufferWidth - rightX - 2;

            var q = new Panel(leftX, 7, leftW, 6, "Vraag");
            Box(q);
            WriteWrapped(q, 0, question);

            var a = new Panel(leftX, 14, leftW, Console.BufferHeight - 18, "Antwoorden");
            Box(a);

            var letters = new[] { "A", "B", "C", "D" };
            int correctIdx = -1;
            for (int i = 0; i < options.Count; i++)
                if (Same(options[i], correctText)) { correctIdx = i; break; }

            for (int i = 0; i < options.Count; i++)
            {
                bool isUser = (i == selectedIndex);
                bool isRight = (i == correctIdx);

                string badge = isRight ? " ✓" : (isUser && !isCorrect ? " ✗" : "");
                string line = $"  {letters[i]})  {options[i]}{badge}";

                if (isRight)
                    WriteColored(a, i, line, ConsoleColor.Green, ConsoleColor.Black, padRight: true);
                else if (isUser && !isCorrect)
                    WriteColored(a, i, line, ConsoleColor.Red, ConsoleColor.Black, padRight: true);
                else
                    Write(a, i, line, padRight: true);
            }

            var rTitle = isCorrect ? "Feedback – GOED" : "Feedback – FOUT";
            var rColor = isCorrect ? ConsoleColor.Green : ConsoleColor.Red;
            var r = new Panel(rightX, 7, rightW, Console.BufferHeight - 11, rTitle);
            Box(r, rColor);

            string line1 = $"Jouw keuze: {letters[selectedIndex]} = {options[selectedIndex]}";
            string line2 = $"Juist: {correctText}";
            WriteWrapped(r, 0, line1);
            WriteWrapped(r, 2, line2);

            if (!string.IsNullOrWhiteSpace(explanation))
                WriteWrapped(r, 4, explanation);

            DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");

            while (true)
            {
                var rr = getRemaining?.Invoke();
                if (rr.HasValue && rr.Value <= TimeSpan.Zero)
                {
                    LastActionWasTimeout = true; return;
                }

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key is ConsoleKey.Q or ConsoleKey.Escape) { LastActionWasBack = true; return; }
                    if (key.Key == ConsoleKey.F)
                    {
                        CycleFeedbackMode();
                        DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");
                    }
                    else if (key.Key == ConsoleKey.Enter) { LastActionWasBack = false; return; }
                }
                else
                {
                    if (rr.HasValue)
                    {
                        Box(head);
                        int pct2 = (int)Math.Round(100.0 * (index - 1) / Math.Max(1, total));
                        string timeStr2 = $"  Tijd: {FormatTime(rr.Value)}";
                        Write(head, 0, $"Score: {score}/{index - 1}   Voortgang: {Bar(pct2, 36)} {pct2,3}%{timeStr2}");
                        DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");
                    }
                    System.Threading.Thread.Sleep(120);
                }
            }
        }

        /* =======================
           Samenvatting & Timer
           ======================= */

        public void SummaryPane(int total, int score, double avgSeconds)
        {
            Screen("Samenvatting", "Enter: terug naar menu");
            var p = new Panel(2, 3, Console.BufferWidth - 4, 9, "Score");
            Box(p);
            int pct = (int)Math.Round(100.0 * score / Math.Max(1, total));
            Write(p, 0, $"Totaal goed: {score}/{total}  ({pct}%)");
            Write(p, 2, $"Voortgang: {Bar(100, 36)} 100%");
            Write(p, 4, $"Gem. antwoordtijd: {avgSeconds:0.0}s");
            DrawFooterRight($"Gem: {avgSeconds:0.0}s | Feedback: {FeedbackModeLabel()}");
            Console.ReadKey(true);
        }

        public void TimeUpPane()
        {
            Screen("Tijd is om", "Enter: score tonen");
            var p = new Panel(6, 5, Console.BufferWidth - 12, 5, "Timer");
            Box(p, ConsoleColor.Red);
            Write(p, 1, "De ingestelde tijd is verstreken.");
            DrawFooterRight($"Feedback: {FeedbackModeLabel()}");
            Console.ReadKey(true);
        }

        /* =======================
           ASCII Smiley-art
           ======================= */

        // Tekent de grote ASCII-smiley. Gezicht (o/$) geel, tong (") rood, rest huidkleur.
        public void DrawSmiley(int x, int y)
        {
            string[] art =
            {
        "  , ; ,   .-'\"\"\"'-.   , ; ,",
        "  \\\\|/  .'         '.  \\\\|//",
        "   \\-;-/   ()   ()   \\-;-/",
        "   // ;               ; \\\\",
        "  //__; :.         .; ;__\\\\",
        " `-----\\\\'.'-.....-'.'/-----'",
        "        '.'.-.-,_.'.'",
        "          '(  (..-'"
    };

            var old = Console.ForegroundColor;
            for (int i = 0; i < art.Length; i++)
            {
                Console.SetCursorPosition(x, y + i);
                foreach (char c in art[i])
                {
                    // Eyes () in yellow
                    if (c == '(' || c == ')')
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    // Mouth / quotes in red
                    else if (c == '\'' || c == '-')
                        Console.ForegroundColor = ConsoleColor.Red;
                    // Default face/outline
                    else
                        Console.ForegroundColor = ConsoleColor.DarkYellow;

                    Console.Write(c);
                }
            }
            Console.ForegroundColor = old;
        }

    }
}