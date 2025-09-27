using System;
using System.Collections.Generic;
using System.Linq;

namespace MarcelMathNumberQuiz
{
    // -------------------- Immutable containers (long API) --------------------
    public record SequenceQuestion(
        string Type,
        string Vraag,
        List<long> Reeks,
        long Antwoord,
        string Uitleg,
        Dictionary<string, object> Meta
    );

    public record GenerationResult(
        string Type,
        string Vraag,
        List<long> Reeks,
        long Antwoord,
        string Uitleg,
        string Herken,
        string Voorbeeld,
        Dictionary<string, object> Meta
    );

    public record ClassificationResult(
        string TypeKey,
        string TypeLabel,
        string Herken,
        string Voorbeeld,
        long? Next,
        double Confidence
    );

    // -------------------- Immutable containers (double API – nieuw) --------------------
    public record SequenceQuestionD(
        string Type,
        string Vraag,
        List<double> Reeks,
        double Antwoord,
        string Uitleg,
        Dictionary<string, object> Meta
    );

    public record GenerationResultD(
        string Type,
        string Vraag,
        List<double> Reeks,
        double Antwoord,
        string Uitleg,
        string Herken,
        string Voorbeeld,
        Dictionary<string, object> Meta
    );

    public record ClassificationResultD(
        string TypeKey,
        string TypeLabel,
        string Herken,
        string Voorbeeld,
        double? Next,
        double Confidence
    );

    public static class IQSequences
    {
        // -------------------- constants/random --------------------
        private static readonly Random s_random = new Random();
        private const long ABS_CAP = 1_000_000_000L;

        // -------------------- difficulty ranges --------------------
        private sealed class Ranges
        {
            public (int Min, int Max) Start;
            public (int Min, int Max) Step;
            public (int Min, int Max) Len;
            public (int Min, int Max) Ratio;
            public (int Min, int Max) Noise;
        }

        private static Ranges ChooseDifficultyRanges(string? difficulty)
        {
            string d = (difficulty ?? "medium").ToLowerInvariant();
            if (d != "easy" && d != "medium" && d != "hard") d = "medium";

            if (d == "easy")
            {
                return new Ranges
                {
                    Start = (1, 15),
                    Step = (1, 7),
                    Len = (5, 6),
                    Ratio = (2, 4),
                    Noise = (0, 0)
                };
            }
            if (d == "hard")
            {
                return new Ranges
                {
                    Start = (5, 80),
                    Step = (3, 25),
                    Len = (7, 8),
                    Ratio = (2, 6),
                    Noise = (0, 0)
                };
            }
            return new Ranges
            {
                Start = (1, 35),
                Step = (2, 12),
                Len = (6, 7),
                Ratio = (2, 5),
                Noise = (0, 0)
            };
        }

        // -------------------- helpers --------------------
        private static int RandIn(int a, int b, Random? rng = null)
            => (rng ?? s_random).Next(a, b + 1);

        private static int PickLen(Ranges r, Random? rng = null)
            => RandIn(r.Len.Min, r.Len.Max, rng);

        private static string FormatVraagNextLong(List<long> seq)
            => "Welke waarde komt als volgende?\r\n\r\nReeks: " + string.Join(", ", seq) + ", ...";

        private static string FormatVraagMissingLong(List<long> seq, int missingIndex)
        {
            var shown = seq.Select((v, i) => i == missingIndex ? "?" : v.ToString());
            return "Welke waarde ontbreekt?\r\n\r\nReeks: " + string.Join(", ", shown);
        }

        private static string Fr(double x)
        {
            string s = x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (s.Contains("E") || s.Contains("e"))
                return x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(x % 1.0) < 1e-12) return ((long)Math.Round(x)).ToString();
            return x.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FormatVraagNextDouble(List<double> seq)
            => "Welke waarde komt als volgende?\r\n\r\nReeks: " + string.Join(", ", seq.Select(Fr)) + ", ...";

        private static string FormatVraagMissingDouble(List<double> seq, int missingIndex)
        {
            var shown = seq.Select((v, i) => i == missingIndex ? "?" : Fr(v));
            return "Welke waarde ontbreekt?\r\n\r\nReeks: " + string.Join(", ", shown);
        }

        private static List<long> Diffs(List<long> seq)
        {
            var outp = new List<long>();
            for (int i = 0; i + 1 < seq.Count; i++) outp.Add(seq[i + 1] - seq[i]);
            return outp;
        }

        private static List<double> Ratios(List<long> seq)
        {
            var outp = new List<double>();
            for (int i = 0; i + 1 < seq.Count; i++)
            {
                double a = seq[i];
                double b = seq[i + 1];
                if (a == 0.0) outp.Add(double.PositiveInfinity);
                else outp.Add(b / a);
            }
            return outp;
        }

        private static bool IsConst(IEnumerable<double> vals, double tol = 1e-9)
        {
            var list = vals.ToList();
            if (list.Count <= 1) return true;
            double x = list[0];
            for (int i = 1; i < list.Count; i++)
                if (Math.Abs(list[i] - x) > tol) return false;
            return true;
        }

        private static bool IsIntSqrt(long x)
        {
            if (x < 0) return false;
            long r = (long)Math.Floor(Math.Sqrt(x));
            return r * r == x;
        }

        private static List<long> SecondDiffs(List<long> seq)
        {
            var d = Diffs(seq);
            return d.Count >= 2 ? Diffs(d) : new List<long>();
        }

        private static int Period(List<long> vals, int maxP = 6)
        {
            int n = vals.Count;
            for (int p = 2; p <= Math.Min(maxP, n / 2); p++)
            {
                bool ok = true;
                for (int i = 0; i < n - p; i++)
                {
                    if (vals[i] != vals[i + p]) { ok = false; break; }
                }
                if (ok) return p;
            }
            return 0;
        }

        private static bool IsPrime(long n)
        {
            if (n < 2) return false;
            if (n % 2 == 0) return n == 2;
            long r = (long)Math.Sqrt(n);
            for (long f = 3; f <= r; f += 2)
                if (n % f == 0) return false;
            return true;
        }

        private static long NextPrime(long x)
        {
            long n = x + 1;
            while (!IsPrime(n)) n++;
            return n;
        }

        private static string DiffsFmt(IEnumerable<long> lst, int maxn = 4)
        {
            var arr = lst.ToList();
            var part = arr.Take(maxn).ToList();
            string s = string.Join(", ", part);
            return s + (arr.Count > maxn ? "..." : "");
        }

        private static long PowLong(long a, int e)
        {
            long res = 1;
            long baseVal = a;
            int exp = e;
            while (exp > 0)
            {
                if ((exp & 1) == 1)
                {
                    checked
                    {
                        res *= baseVal;
                        if (Math.Abs(res) > ABS_CAP) return res;
                    }
                }
                exp >>= 1;
                if (exp > 0)
                {
                    checked
                    {
                        baseVal *= baseVal;
                        if (Math.Abs(baseVal) > ABS_CAP) return baseVal;
                    }
                }
            }
            return res;
        }

        // Digit-product helpers
        private static long DigitProduct(long n)
        {
            n = Math.Abs(n);
            if (n == 0) return 0;
            long prod = 1;
            while (n > 0)
            {
                prod *= (n % 10);
                n /= 10;
            }
            return prod;
        }
        private static string MulDigitsText(long n)
        {
            var s = Math.Abs(n).ToString();
            return string.Join("*", s.Select(ch => ch.ToString()));
        }

        // -------------------- Polynomial helpers (difference table) --------------------
        private static bool IsConstLong(IReadOnlyList<long> xs)
        {
            if (xs.Count < 2) return false;
            for (int i = 1; i < xs.Count; i++)
                if (xs[i] != xs[0]) return false;
            return true;
        }

        private static List<List<long>> BuildDifferenceTable(List<long> seq, int maxOrder = 9)
        {
            var table = new List<List<long>>();
            table.Add(seq.ToList());
            for (int k = 1; k <= maxOrder; k++)
            {
                var prev = table[k - 1];
                if (prev.Count < 2) break;
                var diff = new List<long>(prev.Count - 1);
                for (int i = 0; i + 1 < prev.Count; i++)
                    diff.Add(prev[i + 1] - prev[i]);
                table.Add(diff);
            }
            return table;
        }

        private static long NextByDifferenceTable(List<List<long>> table)
        {
            long next = table[0][^1];
            for (int level = 1; level < table.Count; level++)
                next += table[level][^1];
            return next;
        }

        private static int DetectPolynomialDegree(List<long> seq, int maxOrder, out List<List<long>> table)
        {
            table = BuildDifferenceTable(seq, maxOrder);
            for (int k = 1; k < table.Count; k++)
            {
                if (IsConstLong(table[k]))
                    return k;
            }
            return 0;
        }

        // -------------------- recognition tip & example --------------------
        private static string RecognitionTip(string key, Dictionary<string, object> meta)
        {
            key = (key ?? "").ToLowerInvariant();
            if (key == "arithmetic")
                return "We doen er telkens hetzelfde bij of halen hetzelfde eraf. Formule: an = a1 + (n-1)*d en a(n+1) = an + d.";
            if (key == "geometric")
                return "We vermenigvuldigen steeds met dezelfde factor r (kan ook negatief). Formule: an = a1*r^(n-1) en a(n+1) = an*r.";
            if (key == "geometric_fractional")
                return "Meetkundig met niet-gehele factor r. De verhoudingen zijn gelijk (bijv. ×2,5). a(n+1)=an*r.";
            if (key == "increasing_diffs")
                return "De sprongen nemen elke stap met k toe. Delta_n = Delta_1 + (n-1)*k.";
            if (key == "alt_add_sub")
                return "Om-en-om +a, -b.";
            if (key == "alt_doubling")
                return "Sprongen verdubbelen in absolute waarde en het teken wisselt: Δ(n+1) = -2·Δ(n).";
            if (key == "interleaved_two_aps")
                return "Twee door elkaar: oneven posities en even posities zijn aparte rekenkundige rijen.";
            if (key == "interleaved_mixed_poly")
                return "Vervlochten: odd volgt n^k; even volgt AP of verdubbelende deltas.";
            if (key == "fibonacci_like")
                return "Elke term is de som van de vorige twee.";
            if (key == "primes")
                return "Alle termen zijn priemgetallen.";
            if (key == "polygonal")
            {
                string vorm = meta.TryGetValue("vorm", out var v) ? Convert.ToString(v) ?? "" : "";
                if (vorm == "triangular")
                    return "Driehoeksgetallen: Tn = n(n+1)/2.";
                else
                    return "Kwadraten: n^2.";
            }
            if (key == "quadratic")
                return "Tweede verschillen zijn constant. an = a*n^2 + b*n + c.";
            if (key == "repeating_diff")
                return "Verschillen herhalen met vaste periode p.";
            if (key == "power_steps")
                return "Sprongen zijn n^2 of 2^n.";
            if (key == "digit_product")
                return "Volgende term = product van de cijfers van de vorige.";
            if (key == "poly_k")
                return "k-de verschillen zijn constant (polynoom).";
            return "";
        }

        private static string RecognitionExample(List<long> seq, string key, Dictionary<string, object> meta, long? value = null)
        {
            key = (key ?? "").ToLowerInvariant();
            var s = seq.Take(6).ToList();
            var d = Diffs(s);
            var dd = SecondDiffs(s);
            string text = "";

            switch (key)
            {
                case "arithmetic":
                    text = "Verschillen: " + DiffsFmt(d) + " -> allemaal gelijk.";
                    break;
                case "geometric":
                    {
                        var r = Ratios(s);
                        string fr(double x) => double.IsInfinity(x) ? "Inf" : Math.Round(x, 2).ToString().TrimEnd('0').TrimEnd('.');
                        text = "Verhoudingen: " + string.Join(", ", r.Take(4).Select(fr)) + (r.Count > 4 ? "..." : "") + " -> gelijk.";
                        break;
                    }
                case "increasing_diffs":
                case "quadratic":
                    text = "Verschillen: " + DiffsFmt(d) + " | 2e verschillen: " + DiffsFmt(dd) + " -> 2e verschillen gelijk.";
                    break;
                case "alt_add_sub":
                    text = "Verschillen: " + DiffsFmt(d) + " -> om-en-om.";
                    break;
                case "alt_doubling":
                    text = "Verschillen: " + DiffsFmt(d) + " -> tekenwissel en |Δ| verdubbelt.";
                    break;
                case "interleaved_two_aps":
                    {
                        var odd = s.Where((_, i) => i % 2 == 0).ToList();
                        var even = s.Where((_, i) => i % 2 == 1).ToList();
                        var d1 = Diffs(odd); var d2 = Diffs(even);
                        text = "Odd-verschillen: " + DiffsFmt(d1) + " | Even-verschillen: " + DiffsFmt(d2) + " -> beide vast.";
                        break;
                    }
                case "repeating_diff":
                    {
                        int p = Period(d);
                        text = "Verschillen: " + DiffsFmt(d) + " -> herhaalpatroon (p=" + (p > 0 ? p.ToString() : "?") + ").";
                        break;
                    }
                case "power_steps":
                    text = "Verschillen: " + DiffsFmt(d) + " -> n^2 of 2^n.";
                    break;
                case "digit_product":
                    {
                        var s6 = s.ToList();
                        var parts = new List<string>();
                        for (int i = 0; i + 1 < s6.Count && i < 3; i++)
                            parts.Add($"{s6[i]} -> {MulDigitsText(s6[i])} = {s6[i + 1]}");
                        text = "Check: " + string.Join("; ", parts);
                        break;
                    }
                case "poly_k":
                    {
                        var table = BuildDifferenceTable(s, 9);
                        string layers = string.Join(" | ", table
                            .Take(Math.Min(4, table.Count))
                            .Select((lvl, idx) => (idx == 0 ? "Δ^0" : $"Δ^{idx}") + ": " + DiffsFmt(lvl)));
                        text = layers + " -> hogere orde verschillen constant.";
                        break;
                    }
                default:
                    text = "";
                    break;
            }

            if (value.HasValue)
            {
                string mode = meta.TryGetValue("ask_mode", out var m) ? Convert.ToString(m) ?? "" : "";
                if (mode == "missing" && meta.TryGetValue("missing_index", out var miObj))
                {
                    int pos = Convert.ToInt32(miObj) + 1;
                    text += " -> Ontbrekend (positie " + pos + ") = " + value.Value;
                }
                else
                {
                    text += " -> Volgende = " + value.Value;
                }
            }
            return text;
        }

        private static string RecognitionExampleD(List<double> seq, string key, Dictionary<string, object> meta, double? value = null)
        {
            key = (key ?? "").ToLowerInvariant();
            string text = "";
            if (key == "geometric_fractional")
            {
                var ratios = new List<double>();
                for (int i = 0; i + 1 < seq.Count; i++)
                {
                    double a = seq[i];
                    double b = seq[i + 1];
                    ratios.Add(a == 0 ? double.PositiveInfinity : b / a);
                }
                string rr = string.Join(", ", ratios.Take(4).Select(x => Math.Round(x, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)));
                text = "Verhoudingen: " + rr + (ratios.Count > 4 ? "..." : "") + " -> gelijk.";
            }
            if (value.HasValue) text += " -> Volgende = " + Fr(value.Value);
            return text;
        }

        // -------------------- ASK MODE helpers --------------------
        private static string ChooseAskMode(string askMode, Random? rng)
        {
            string mode = (askMode ?? "both").ToLowerInvariant();
            if (mode == "next" || mode == "missing") return mode;
            return (RandIn(0, 1, rng) == 0) ? "next" : "missing";
        }

        private static int ChooseMissingIndex(int count, Random? rng)
        {
            if (count <= 3) return 1;
            return RandIn(1, count - 2, rng);
        }

        // -------------------- generators (long API) --------------------
        private delegate SequenceQuestion GenFunc(string difficulty, Random? rng, string askMode);

        private static SequenceQuestion PostProcessForAskMode(
            string type,
            List<long> seq,
            long nextAnswer,
            string baseUitleg,
            Dictionary<string, object> meta,
            Random? rng,
            string askModeParam)
        {
            string mode = ChooseAskMode(askModeParam, rng);

            if (mode == "next")
            {
                meta["ask_mode"] = "next";
                meta["next_pos"] = seq.Count + 1;
                string vraag = FormatVraagNextLong(seq);
                string uitleg = baseUitleg + " (Vraag: volgende waarde.)";
                return new SequenceQuestion(type, vraag, seq, nextAnswer, uitleg, meta);
            }
            else
            {
                int idx = ChooseMissingIndex(seq.Count, rng);
                long missingVal = seq[idx];
                meta["ask_mode"] = "missing";
                meta["missing_index"] = idx;
                meta["missing_pos"] = idx + 1;
                string vraag = FormatVraagMissingLong(seq, idx);
                string uitleg = baseUitleg + $" (Vraag: ontbrekend op positie {idx + 1} = {missingVal}.)";
                return new SequenceQuestion(type, vraag, seq, missingVal, uitleg, meta);
            }
        }

        private static SequenceQuestion GenArithmetic(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);
            long d = (RandIn(0, 1, rng) == 0 ? 1 : -1) * RandIn(r.Step.Min, r.Step.Max, rng);
            var seq = Enumerable.Range(0, n).Select(i => a0 + i * d).Select(x => (long)x).ToList();
            long ansNext = a0 + n * d;
            long last = seq[^1];

            string uitleg = $"Rekenkundig: elke stap +/- hetzelfde verschil d. Voorbeeld: d = {d}. Volgende = {last} + {d} = {ansNext}.";

            var meta = new Dictionary<string, object> { ["d"] = d, ["type_key"] = "arithmetic" };
            return PostProcessForAskMode("rekenkundig (vast verschil)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenGeometric(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a0 = RandIn(1, Math.Max(2, r.Start.Max / 2), rng);
            long ratio = (RandIn(0, 1, rng) == 0 ? 1 : -1) * RandIn(r.Ratio.Min, r.Ratio.Max, rng);

            var seq = new List<long>();
            long cur = a0;
            seq.Add(cur);
            bool overflow = false;
            for (int i = 1; i < n; i++)
            {
                try
                {
                    checked
                    {
                        cur = cur * ratio;
                        if (Math.Abs(cur) > ABS_CAP) { overflow = true; break; }
                        seq.Add(cur);
                    }
                }
                catch { overflow = true; break; }
            }
            if (overflow) return GenGeometric(difficulty, rng, askMode);

            long ansNext = seq[^1] * ratio;
            long last = seq[^1];

            string uitleg = $"Meetkundig (gehele factor): r = {ratio}. Volgende = {last} * {ratio} = {ansNext}.";

            var meta = new Dictionary<string, object> { ["r"] = ratio, ["type_key"] = "geometric" };
            return PostProcessForAskMode("meetkundig (vaste factor)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenIncreasingDiffs(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);
            long step0 = RandIn(1, r.Step.Max / 2, rng);
            long stepInc = RandIn(1, Math.Max(2, r.Step.Max / 3), rng);

            var diffs = Enumerable.Range(0, n).Select(i => step0 + i * stepInc).Select(x => (long)x).ToList();
            var seq = new List<long> { a0 };
            for (int i = 0; i < n - 1; i++) seq.Add(seq[^1] + diffs[i]);
            long nextDiff = diffs[^1];
            long ansNext = seq[^1] + nextDiff;

            string uitleg = $"Toenemende verschillen: Δ start {diffs[0]}, elke stap +{stepInc}. Volgende Δ = {nextDiff}; volgende term = {ansNext}.";

            var meta = new Dictionary<string, object> { ["step0"] = step0, ["plus_per_step"] = stepInc, ["type_key"] = "increasing_diffs" };
            return PostProcessForAskMode("toenemende verschillen", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenAltAddSub(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);
            long add = RandIn(2, r.Step.Max, rng);
            long sub = RandIn(1, Math.Max(1, r.Step.Max / 2), rng);

            var seq = new List<long> { a0 };
            for (int i = 1; i < n; i++)
            {
                long delta = (i % 2 == 1) ? add : -sub;
                seq.Add(seq[^1] + delta);
            }
            long nextDelta = (n % 2 == 1) ? add : -sub;
            long ansNext = seq[^1] + nextDelta;

            string uitleg = $"Afwisselend: +{add}, -{sub}, ... Volgende term = {ansNext}.";

            var meta = new Dictionary<string, object> { ["+"] = add, ["-"] = sub, ["type_key"] = "alt_add_sub" };
            return PostProcessForAskMode("afwisselend +a, -b", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenAltDoubling(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);

            int startPow = RandIn(3, 5, rng); // 2^3..2^5
            long step = 1L << startPow;
            int sign = RandIn(0, 1, rng) == 0 ? 1 : -1;

            var seq = new List<long> { a0 };
            for (int i = 1; i < n; i++)
            {
                long delta = sign * step;
                seq.Add(seq[^1] + delta);
                step <<= 1;
                sign = -sign;
            }
            long nextDelta = sign * step;
            long ansNext = seq[^1] + nextDelta;

            string uitleg = $"Δ(n+1) = -2·Δ(n). Volgende Δ = {(nextDelta >= 0 ? "+" : "")}{nextDelta} ⇒ volgende term = {ansNext}.";

            var meta = new Dictionary<string, object> { ["type_key"] = "alt_doubling" };
            return PostProcessForAskMode("stapgroottes 2^n met tekenwisseling", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenInterleavedTwoAps(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            if (n % 2 == 0) n -= 1;

            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);
            long b0 = RandIn(r.Start.Min, r.Start.Max, rng);

            // d1/d2 mogen ook 0 zijn (constante subreeks)
            long d1 = (RandIn(0, 1, rng) == 0 ? 1 : -1) * RandIn(0, Math.Max(1, r.Step.Max / 2), rng);
            long d2 = (RandIn(0, 1, rng) == 0 ? 1 : -1) * RandIn(0, Math.Max(1, r.Step.Max / 2), rng);

            var s1 = Enumerable.Range(0, (n + 1) / 2).Select(i => a0 + i * d1).Select(x => (long)x).ToList();
            var s2 = Enumerable.Range(0, n / 2).Select(i => b0 + i * d2).Select(x => (long)x).ToList();

            var seq = new List<long>();
            for (int i = 0; i < n; i++)
                seq.Add(i % 2 == 0 ? s1[i / 2] : s2[i / 2]);

            int k = n / 2; // next is even subsequence
            long ansNext = b0 + k * d2;

            string uitleg = $"Vervlochten: odd d1={d1}, even d2={d2}. Volgende (even) = {ansNext}.";

            var meta = new Dictionary<string, object> { ["d1"] = d1, ["d2"] = d2, ["type_key"] = "interleaved_two_aps" };
            return PostProcessForAskMode("vervlochten (2 reeksen)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenNthPowerPoly(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);

            int k = RandIn(3, 9, rng);
            long a = 1;
            int b = RandIn(0, 2, rng);
            long c = RandIn(-20, 20, rng);

            var seq = new List<long>();
            for (int i = 1; i <= n; i++)
            {
                long x = i + b;
                long pow = 1;
                for (int p = 0; p < k; p++)
                {
                    checked
                    {
                        pow *= x;
                        if (Math.Abs(pow) > ABS_CAP) { pow = x; break; }
                    }
                }
                long term = c;
                checked
                {
                    term += a * pow;
                    if (Math.Abs(term) > ABS_CAP) term = Math.Sign(term) * ABS_CAP;
                }
                seq.Add(term);
            }

            long xnext = n + 1 + b;
            long powN = 1;
            for (int p = 0; p < k; p++)
            {
                checked
                {
                    powN *= xnext;
                    if (Math.Abs(powN) > ABS_CAP) { powN = xnext; break; }
                }
            }
            long ansNext = c + a * powN;

            string uitleg =
                $"Polynoom graad {k}: an = a·(n+b)^{k} + c met a={a}, b={b}, c={c}. Volgende = {ansNext}.";

            var meta = new Dictionary<string, object>
            {
                ["type_key"] = "poly_k",
                ["k"] = k,
                ["a"] = a,
                ["b"] = b,
                ["c"] = c
            };

            return PostProcessForAskMode($"positie^{k} (polynoom graad {k})", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenInterleavedMixedPolyVsEven(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            if (n < 5) n = 5;
            if (n % 2 == 0) n += 1;

            // Odd = n^k + c
            int k = RandIn(3, 9, rng);
            int b = RandIn(0, 2, rng);
            long c = RandIn(-10, 10, rng);
            var odd = new List<long>();
            for (int i = 1; i <= (n + 1) / 2; i++)
            {
                long x = i + b;
                long pow = 1;
                for (int p = 0; p < k; p++)
                {
                    checked
                    {
                        pow *= x;
                        if (Math.Abs(pow) > ABS_CAP) { pow = x; break; }
                    }
                }
                long term = c + pow;
                if (Math.Abs(term) > ABS_CAP) term = Math.Sign(term) * ABS_CAP;
                odd.Add(term);
            }

            // Even: 0=rekenkundig, 1=verdubbelende deltas
            int evenMode = RandIn(0, 1, rng);
            var even = new List<long>();
            long even0 = RandIn(10, 50, rng);
            even.Add(even0);

            if (evenMode == 0)
            {
                long dEven = (RandIn(0, 1, rng) == 0 ? 1 : -1) * RandIn(5, 20, rng);
                for (int i = 1; i < n / 2; i++) even.Add(even[^1] + dEven);
            }
            else
            {
                long kstep = RandIn(5, 10, rng) * 5; // 10,15,20...
                long inc = kstep;
                for (int i = 1; i < n / 2; i++)
                {
                    even.Add(even[^1] + inc);
                    inc *= 2;
                }
            }

            // Interleave
            var seq = new List<long>();
            for (int i = 0; i < n; i++)
                seq.Add(i % 2 == 0 ? odd[i / 2] : even[i / 2]);

            // Next (even-subsequence)
            long ansNext;
            if (evenMode == 0)
            {
                long dEven = even.Count >= 2 ? even[^1] - even[^2] : 0;
                ansNext = even[^1] + dEven;
            }
            else
            {
                long lastDelta = even.Count >= 2 ? even[^1] - even[^2] : 0;
                ansNext = even[^1] + lastDelta * 2;
            }

            string evenLabel = evenMode == 0 ? "even rekenkundig (vast d)" : "even +k, +2k, +4k, ...";
            string uitleg =
                $"Vervlochten: odd = n^{k} + {c} (b={b}); {evenLabel}. Volgende term (even) = {ansNext}.";

            var meta = new Dictionary<string, object>
            {
                ["type_key"] = "interleaved_mixed_poly",
                ["odd_k"] = k,
                ["odd_b"] = b,
                ["odd_c"] = c,
                ["even_mode"] = evenMode
            };

            return PostProcessForAskMode("vervlochten (odd n^k, even AP/2^n-deltas)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenFibonacciLike(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a = RandIn(1, 10, rng);
            long b = RandIn(1, 10, rng);

            var seq = new List<long> { a, b };
            for (int i = 2; i < n; i++)
            {
                long next = seq[^1] + seq[^2];
                if (Math.Abs(next) > ABS_CAP) return GenFibonacciLike(difficulty, rng, askMode);
                seq.Add(next);
            }
            long ansNext = seq[^1] + seq[^2];

            string uitleg = $"Fibonacci-achtig: an = a(n-1) + a(n-2). Volgende = {ansNext}.";

            var meta = new Dictionary<string, object> { ["type_key"] = "fibonacci_like" };
            return PostProcessForAskMode("Fibonacci-achtig", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenPrimes(string difficulty, Random? rng, string askMode)
        {
            int n = PickLen(ChooseDifficultyRanges(difficulty), rng);
            int start = RandIn(2, 40, rng);

            var seq = new List<long>();
            int i = start;
            while (seq.Count < n)
            {
                if (IsPrime(i)) seq.Add(i);
                i++;
            }
            int j = i;
            while (!IsPrime(j)) j++;
            long ansNext = j;

            string uitleg = $"Priemgetallen. Volgende priem na {seq[^1]} is {ansNext}.";

            var meta = new Dictionary<string, object> { ["type_key"] = "primes" };
            return PostProcessForAskMode("priemgetallen", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenPolygonalTriangular(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            bool triangular = RandIn(0, 1, rng) == 0;

            List<long> seq;
            long ansNext;
            string typeName;
            string uitleg;

            if (triangular)
            {
                seq = Enumerable.Range(1, n).Select(i => (long)i * (i + 1) / 2).ToList();
                ansNext = (long)(n + 1) * (n + 2) / 2;
                long last = seq[^1];
                uitleg = $"Driehoeksgetallen: T{n} = {last}, T{n + 1} = {ansNext}.";
                typeName = "driehoeksgetallen";
            }
            else
            {
                seq = Enumerable.Range(1, n).Select(i => (long)i * i).ToList();
                ansNext = (long)(n + 1) * (n + 1);
                long last = seq[^1];
                uitleg = $"Kwadraten: S{n} = {last}, S{n + 1} = {ansNext}.";
                typeName = "kwadraten (n^2)";
            }

            var meta = new Dictionary<string, object> { ["vorm"] = (triangular ? "triangular" : "square"), ["type_key"] = "polygonal" };
            return PostProcessForAskMode(typeName, seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenQuadraticGeneral(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            long a = new[] { 1L, 1L, 2L, -1L }[RandIn(0, 3, rng)];
            long b = RandIn(-4, 4, rng);
            long c = RandIn(-9, 12, rng);

            var seq = Enumerable.Range(1, n).Select(i => a * i * i + b * i + c).Select(x => (long)x).ToList();
            var d = Diffs(seq);
            var dd = SecondDiffs(seq);
            long lastDelta = d.Count > 0 ? d[^1] : 0;
            long inc2 = dd.Count > 0 ? dd[^1] : 0;
            long nextDiff = lastDelta + inc2;
            long ansNext = seq[^1] + nextDiff;

            string uitleg = $"Kwadratisch: 2e verschillen constant. Volgende Δ = {nextDiff}; volgende term = {ansNext}.";

            var meta = new Dictionary<string, object> { ["a"] = a, ["b"] = b, ["c"] = c, ["type_key"] = "quadratic" };
            return PostProcessForAskMode("kwadratisch (2e verschillen vast)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenRepeatingDiff(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);

            int L = difficulty switch
            {
                "easy" => 6,
                "medium" => 6,
                "hard" => 7,
                _ => 6
            };

            int p = difficulty == "hard" ? new[] { 2, 3, 4 }[RandIn(0, 2, rng)]
                                         : new[] { 2, 3 }[RandIn(0, 1, rng)];

            while ((L - 1) < p + Math.Max(1, p - 1) && L < 8) L++;

            int baseStep = difficulty switch
            {
                "easy" => RandIn(2, 5, rng),
                "medium" => RandIn(3, 7, rng),
                "hard" => RandIn(4, 9, rng),
                _ => RandIn(2, 5, rng)
            };

            var pattern = new List<long>(p);
            for (int i = 0; i < p; i++)
            {
                int v = baseStep + (i > 0 ? RandIn(0, 1, rng) : 0);
                if (difficulty == "hard" && RandIn(0, 9, rng) == 0) v = -v;
                if (v == 0) v = 1;
                pattern.Add(v);
            }
            if (pattern.All(x => x <= 0)) pattern[0] = Math.Abs(pattern[0]);

            long a0 = RandIn(r.Start.Min, Math.Max(r.Start.Min, r.Start.Max), rng);
            if (a0 < 5) a0 = 5;

            var seq = new List<long> { a0 };
            for (int i = 1; i < L; i++)
            {
                long dlt = pattern[(i - 1) % p];
                long nxt = seq[^1] + dlt;
                if (Math.Abs(nxt) > ABS_CAP) nxt = seq[^1] + Math.Sign(dlt) * Math.Min(Math.Abs(dlt), 3);
                seq.Add(nxt);
            }

            string mode = ChooseAskMode(askMode, rng);
            string typeLbl = "herhalend verschil-patroon";
            var meta = new Dictionary<string, object>
            {
                ["pattern"] = pattern,
                ["p"] = p,
                ["type_key"] = "repeating_diff"
            };

            var diffs = new List<long>();
            for (int i = 1; i < seq.Count; i++) diffs.Add(seq[i] - seq[i - 1]);
            string patStr = string.Join(", ", pattern.Select(x => (x >= 0 ? "+" : "") + x));
            string difStr = string.Join(", ", diffs.Select(x => (x >= 0 ? "+" : "") + x));

            long nextDelta = pattern[(L - 1) % p];
            long nextVal = seq[^1] + nextDelta;

            string uitlegKern =
                $"Herhalend verschil-patroon met periode p = {p}: Δ = [{patStr}]. " +
                $"Zichtbare verschillen: {difStr}.";

            if (mode == "next")
            {
                // Toon L-1 termen en vraag de volgende
                string vraag = "Welke waarde komt als volgende?\n\nReeks: " +
                               string.Join(", ", seq.Take(L - 1)) + ", ...";
                string uitleg = $"{uitlegKern} Volgende Δ = {(nextDelta >= 0 ? "+" : "")}{nextDelta}; " +
                                $"volgende term = {nextVal}. (Vraag: volgende waarde.)";
                return new SequenceQuestion(typeLbl, vraag, seq, nextVal, uitleg, meta);
            }
            else
            {
                int hideIndex = RandIn(1, L - 2, rng);  // niet de eerste/laatste
                meta["ask_mode"] = "missing";
                meta["missing_index"] = hideIndex;
                meta["missing_pos"] = hideIndex + 1;

                var shown = seq.Select((v, i) => i == hideIndex ? "?" : v.ToString()).ToList();
                string vraag = "Welke waarde ontbreekt?\n\nReeks: " + string.Join(", ", shown);

                long correct = seq[hideIndex];
                long left = seq[hideIndex - 1];
                long need = seq[hideIndex] - left;

                string uitleg = $"{uitlegKern} Op positie {hideIndex + 1} hoort Δ = {(need >= 0 ? "+" : "")}{need}. " +
                                $"Daarmee wordt de ontbrekende waarde {left} {(need >= 0 ? "+" : "")}{need} = {correct}.";
                return new SequenceQuestion(typeLbl, vraag, seq, correct, uitleg, meta);
            }
        }



        private static SequenceQuestion GenPowerSteps(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);
            bool squares = RandIn(0, 1, rng) == 0;
            long a0 = RandIn(r.Start.Min, r.Start.Max, rng);
            var seq = new List<long> { a0 };
            string t;
            long ansNext;
            if (squares)
            {
                var diffs = Enumerable.Range(1, n).Select(i => (long)i * i).ToList();
                for (int i = 1; i < n; i++) seq.Add(seq[^1] + diffs[i - 1]);
                long nextDelta = diffs[n - 1];
                ansNext = seq[^1] + nextDelta;
                t = "stapgroottes n^2";
            }
            else
            {
                var diffs = Enumerable.Range(0, n + 1).Select(i => (long)1 << i).ToList();
                for (int i = 1; i < n; i++) seq.Add(seq[^1] + diffs[i - 1]);
                long nextDelta = diffs[n - 1];
                ansNext = seq[^1] + nextDelta;
                t = "stapgroottes 2^n";
            }

            string uitleg = squares
                ? $"Delta = 1,4,9,16,... Volgende Δ = {n * n} ⇒ volgende = {ansNext}."
                : $"Delta = 1,2,4,8,... Volgende Δ = {1L << (n - 1)} ⇒ volgende = {ansNext}.";

            var meta = new Dictionary<string, object> { ["type_key"] = "power_steps" };
            return PostProcessForAskMode(t, seq, ansNext, uitleg, meta, rng, askMode);
        }

        private static SequenceQuestion GenDigitProduct(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);

            long start = RandIn(1000, 999999, rng);
            var seq = new List<long> { start };
            for (int i = 1; i < n; i++)
                seq.Add(DigitProduct(seq[i - 1]));

            long ansNext = DigitProduct(seq[^1]);

            var chain = new List<string>();
            int steps = Math.Min(seq.Count - 1, 3);
            for (int i = 0; i < steps; i++)
                chain.Add(seq[i] + " -> " + MulDigitsText(seq[i]) + " = " + seq[i + 1]);

            string uitleg =
                "Cijferproduct: elke volgende term is het product van de cijfers van de vorige. " +
                (chain.Count > 0 ? string.Join("; ", chain) + ". " : "") +
                $"Volgende = {MulDigitsText(seq[^1])} = {ansNext}.";

            var meta = new Dictionary<string, object> { ["type_key"] = "digit_product" };
            return PostProcessForAskMode("cijferproduct", seq, ansNext, uitleg, meta, rng, askMode);
        }

        // -------------------- generator registry (long) --------------------
        private static readonly Dictionary<string, GenFunc> GEN_TYPES = new(StringComparer.OrdinalIgnoreCase)
        {
            { "arithmetic", GenArithmetic },
            { "geometric", GenGeometric },
            { "increasing_diffs", GenIncreasingDiffs },
            { "alt_add_sub", GenAltAddSub },
            { "alt_doubling", GenAltDoubling },
            { "interleaved_two_aps", GenInterleavedTwoAps },
            { "interleaved_mixed_poly", GenInterleavedMixedPolyVsEven },
            { "fibonacci_like", GenFibonacciLike },
            { "primes", GenPrimes },
            { "polygonal", GenPolygonalTriangular },
            { "quadratic", GenQuadraticGeneral },
            { "repeating_diff", GenRepeatingDiff },
            { "power_steps", GenPowerSteps },
            { "digit_product", GenDigitProduct },
            { "poly_k", GenNthPowerPoly },
        };

        // -------------------- generators (double API) --------------------
        private delegate SequenceQuestionD GenFuncD(string difficulty, Random? rng, string askMode);

        private static SequenceQuestionD PostProcessForAskModeD(
            string type,
            List<double> seq,
            double nextAnswer,
            string baseUitleg,
            Dictionary<string, object> meta,
            Random? rng,
            string askModeParam)
        {
            string mode = ChooseAskMode(askModeParam, rng);

            if (mode == "next")
            {
                meta["ask_mode"] = "next";
                meta["next_pos"] = seq.Count + 1;
                string vraag = FormatVraagNextDouble(seq);
                string uitleg = baseUitleg + " (Vraag: volgende waarde.)";
                return new SequenceQuestionD(type, vraag, seq, nextAnswer, uitleg, meta);
            }
            else
            {
                int idx = ChooseMissingIndex(seq.Count, rng);
                double missingVal = seq[idx];
                meta["ask_mode"] = "missing";
                meta["missing_index"] = idx;
                meta["missing_pos"] = idx + 1;
                string vraag = FormatVraagMissingDouble(seq, idx);
                string uitleg = baseUitleg + $" (Vraag: ontbrekend op positie {idx + 1} = {Fr(missingVal)}.)";
                return new SequenceQuestionD(type, vraag, seq, missingVal, uitleg, meta);
            }
        }

        // NEW: fractionele meetkundige generator
        private static SequenceQuestionD GenGeometricFractionalD(string difficulty, Random? rng, string askMode)
        {
            var r = ChooseDifficultyRanges(difficulty);
            int n = PickLen(r, rng);

            // Start integer, ratio uit een rationele set die mooie decimalen geeft
            double a0 = RandIn(2, 20, rng);
            var ratios = new double[] { 3.0 / 2.0, 5.0 / 2.0, 7.0 / 2.0, 5.0 / 4.0, 9.0 / 4.0, 3.0 }; // bevat 2.5
            double rr = ratios[RandIn(0, ratios.Length - 1, rng)];
            if (RandIn(0, 5, rng) == 0) rr = -rr; // soms negatief

            var seq = new List<double> { a0 };
            for (int i = 1; i < n; i++) seq.Add(seq[^1] * rr);
            double ansNext = seq[^1] * rr;

            string uitleg = $"Meetkundig (fractioneel): r = {Fr(rr)}. Volgende = {Fr(seq[^1])} × {Fr(rr)} = {Fr(ansNext)}.";
            var meta = new Dictionary<string, object> { ["r"] = rr, ["type_key"] = "geometric_fractional" };

            return PostProcessForAskModeD("meetkundig (fractionele factor)", seq, ansNext, uitleg, meta, rng, askMode);
        }

        // -------------------- generator registry (double) --------------------
        private static readonly Dictionary<string, GenFuncD> GEN_TYPES_D = new(StringComparer.OrdinalIgnoreCase)
        {
            { "geometric_fractional", GenGeometricFractionalD },
        };

        // -------------------- classifier (long API) --------------------
        public static ClassificationResult ClassifySequence(List<long> seq)
        {
            if (seq == null || seq.Count < 3)
                return new ClassificationResult("unknown", "onbekend", "", "", null, 0.0);

            var s = seq.ToList();
            var d = Diffs(s);
            var dd = SecondDiffs(s);
            var r = Ratios(s);

            // 1) arithmetic
            if (IsConst(d.Select(x => (double)x)))
            {
                long nx = s[^1] + (d.Count > 0 ? d[^1] : 0);
                string key = "arithmetic";
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "rekenkundig (vast verschil)",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.95);
            }

            // 2) geometric (gehele factor)
            var rClean = r.Where(x => !double.IsInfinity(x) && !double.IsNaN(x)).ToList();
            if (rClean.Count >= 2 && IsConst(rClean))
            {
                // check of ratio 'mooi' geheel is
                double rEst = rClean[^1];
                if (Math.Abs(rEst - Math.Round(rEst)) < 1e-9)
                {
                    string key = "geometric";
                    long nx = (long)Math.Round(s[^1] * rEst);
                    var meta = new Dictionary<string, object>();
                    return new ClassificationResult(
                        key, "meetkundig (vaste factor)",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.90);
                }
                // anders: fractioneel – we geven ronding als Next (back-compat) + dubbele waarde in Meta
                {
                    string key = "geometric_fractional";
                    double nextD = s[^1] * rEst;
                    long nx = (long)Math.Round(nextD);
                    var meta = new Dictionary<string, object>
                    {
                        ["type_key"] = key,
                        ["ratio_double"] = rEst,
                        ["next_double"] = nextD
                    };
                    string voorbeeld = $"Verhoudingen ~ {Math.Round(rEst, 4)} → fractioneel. Volgende ≈ {Fr(nextD)}.";
                    return new ClassificationResult(
                        key, "meetkundig (fractionele factor)",
                        RecognitionTip(key, meta),
                        voorbeeld,
                        nx, 0.82);
                }
            }

            // 2a) pure macht (n+b)^k + c
            {
                int[] bs = { 0, 1, 2 };
                for (int bi = 0; bi < bs.Length; bi++)
                {
                    int b = bs[bi];
                    for (int k = 3; k <= 12; k++)
                    {
                        long c = s[0] - PowLong(1 + b, k);
                        bool ok = true;
                        for (int i = 0; i < s.Count; i++)
                        {
                            long n = i + 1;
                            long expect = c + PowLong(n + b, k);
                            if (expect != s[i]) { ok = false; break; }
                        }
                        if (ok)
                        {
                            long nnext = s.Count + 1;
                            long nx = c + PowLong(nnext + b, k);
                            var meta = new Dictionary<string, object> { ["k"] = k, ["b"] = b, ["c"] = c, ["type_key"] = "poly_k" };
                            return new ClassificationResult(
                                "poly_k", $"polynoom graad {k} (pure macht)",
                                RecognitionTip("poly_k", meta),
                                RecognitionExample(s, "poly_k", meta, nx),
                                nx, 0.88);
                        }
                    }
                }
            }

            // 2b) algemene polynoom (graad ≥3)
            {
                var deg = DetectPolynomialDegree(s, 9, out var table);
                if (deg >= 3)
                {
                    long nx = NextByDifferenceTable(table);
                    string key = "poly_k";
                    var meta = new Dictionary<string, object> { ["k"] = deg, ["type_key"] = key };
                    return new ClassificationResult(
                        key, $"polynoom graad {deg}",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.82);
                }
            }

            // 3) quadratic / polygonal
            if (dd.Count >= 2 && IsConst(dd.Select(x => (double)x)))
            {
                long inc2 = dd[^1];
                long nextDiff = d.Count >= 1 ? (d[^1] + inc2) : 0;
                long nx = s[^1] + nextDiff;

                // triangular
                if (d.Count >= 3 && Enumerable.Range(0, d.Count - 1).All(i => d[i + 1] - d[i] == 1) && d[0] > 0)
                {
                    string key = "polygonal";
                    var meta = new Dictionary<string, object> { ["vorm"] = "triangular" };
                    return new ClassificationResult(
                        key, "driehoeksgetallen",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.90);
                }
                // squares
                if (s.Take(Math.Min(5, s.Count)).All(IsIntSqrt) ||
                    (d.Count >= 3 && Enumerable.Range(0, d.Count - 1).All(i => d[i + 1] - d[i] == 2) && (d[0] % 2 != 0)))
                {
                    string key = "polygonal";
                    var meta = new Dictionary<string, object> { ["vorm"] = "square" };
                    return new ClassificationResult(
                        key, "kwadraten (n^2)",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.90);
                }

                {
                    string key = "quadratic";
                    var meta = new Dictionary<string, object>();
                    return new ClassificationResult(
                        key, "kwadratisch (2e verschillen vast)",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.80);
                }
            }

            // 4) alternating +a, -b
            if (d.Count >= 2 && Enumerable.Range(0, d.Count).All(i => d[i] == d[i % 2]))
            {
                string key = "alt_add_sub";
                long nextDelta = d[d.Count % 2];
                long nx = s[^1] + nextDelta;
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "afwisselend +a, -b",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.75);
            }

            // 4b) alternating sign & doubling magnitude
            if (d.Count >= 2 && Enumerable.Range(0, d.Count - 1).All(i => d[i + 1] == -2 * d[i]))
            {
                string key = "alt_doubling";
                long nextDelta = -2 * d[^1];
                long nx = s[^1] + nextDelta;
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "stapgroottes 2^n met tekenwisseling",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.80);
            }

            // 5) interleaved two APs
            var oddSeq = s.Where((_, i) => i % 2 == 0).ToList();
            var evenSeq = s.Where((_, i) => i % 2 == 1).ToList();
            if (oddSeq.Count >= 2 && evenSeq.Count >= 1)
            {
                var d1 = Diffs(oddSeq);
                var d2 = Diffs(evenSeq);
                if ((d1.Count == 0 || IsConst(d1.Select(x => (double)x)))
                    && (d2.Count == 0 || IsConst(d2.Select(x => (double)x))))
                {
                    string key = "interleaved_two_aps";
                    long nextDelta;
                    long nx;
                    if (s.Count % 2 == 0)
                    {
                        nextDelta = d1.Count >= 1 ? d1[^1] : 0;
                        nx = oddSeq[^1] + nextDelta;
                    }
                    else
                    {
                        nextDelta = d2.Count >= 1 ? d2[^1] : 0;
                        nx = evenSeq[^1] + nextDelta;
                    }
                    var meta = new Dictionary<string, object>();
                    return new ClassificationResult(
                        key, "vervlochten (2 reeksen)",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, key, meta, nx),
                        nx, 0.70);
                }
            }

            // 5b) interleaved mixed poly vs even
            if (oddSeq.Count >= 3 && evenSeq.Count >= 2)
            {
                int degOdd = DetectPolynomialDegree(oddSeq, 9, out var tableOdd);
                var dEven = Diffs(evenSeq);
                bool evenAP = dEven.Count >= 1 && IsConst(dEven.Select(x => (double)x));
                bool evenDoubling = dEven.Count >= 2 && Enumerable.Range(0, dEven.Count - 1).All(i => dEven[i + 1] == 2 * dEven[i]);

                if (degOdd >= 3 && (evenAP || evenDoubling))
                {
                    long nextOdd = NextByDifferenceTable(tableOdd);
                    long nx;

                    if (s.Count % 2 == 1)
                    {
                        if (evenAP) nx = evenSeq[^1] + dEven[^1];
                        else nx = evenSeq[^1] + dEven[^1] * 2;
                    }
                    else
                    {
                        nx = nextOdd;
                    }

                    string key = "interleaved_mixed_poly";
                    var meta = new Dictionary<string, object> { ["type_key"] = key, ["k_odd"] = degOdd, ["even_mode"] = evenAP ? "AP" : "doubling" };
                    return new ClassificationResult(
                        key, "vervlochten (odd n^k, even AP/2^m)",
                        RecognitionTip(key, meta),
                        RecognitionExample(s, "interleaved_mixed_poly", meta, nx),
                        nx, 0.78);
                }
            }

            // 6) Fibonacci-like
            bool okFib = true;
            for (int i = 2; i < s.Count; i++)
                if (s[i] != s[i - 1] + s[i - 2]) { okFib = false; break; }

            if (okFib)
            {
                string key = "fibonacci_like";
                long nx = s[^1] + s[^2];
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "Fibonacci-achtig",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.85);
            }

            // 7) primes
            if (s.All(IsPrime))
            {
                string key = "primes";
                long nx = NextPrime(s[^1]);
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "priemgetallen",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.70);
            }

            // 8) digit_product
            bool okDigitProd = true;
            for (int i = 1; i < s.Count; i++)
                if (s[i] != DigitProduct(s[i - 1])) { okDigitProd = false; break; }

            if (okDigitProd)
            {
                string key = "digit_product";
                long nx = DigitProduct(s[^1]);
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "cijferproduct",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.85);
            }

            // 9) repeating diff pattern
            int p2 = Period(d);
            if (p2 > 0)
            {
                string key = "repeating_diff";
                long nextDelta = d[d.Count % p2];
                long nx = s[^1] + nextDelta;
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "herhalend verschil-patroon",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.65);
            }

            // 10) power steps
            if (d.Count >= 2 && Enumerable.Range(0, d.Count - 1).All(i => d[i + 1] == 2 * d[i]))
            {
                string key = "power_steps";
                long nextDelta = d[^1] * 2;
                long nx = s[^1] + nextDelta;
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "stapgroottes 2^n",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.70);
            }
            if (d.Count >= 3 && d[0] == 1 && Enumerable.Range(0, Math.Min(d.Count, 6)).All(i => d[i] == (i + 1) * (i + 1)))
            {
                string key = "power_steps";
                long nextDelta = (long)(d.Count + 1) * (d.Count + 1);
                long nx = s[^1] + nextDelta;
                var meta = new Dictionary<string, object>();
                return new ClassificationResult(
                    key, "stapgroottes n^2",
                    RecognitionTip(key, meta),
                    RecognitionExample(s, key, meta, nx),
                    nx, 0.70);
            }

            return new ClassificationResult("unknown", "onbekend", "", "", null, 0.30);
        }

        // -------------------- classifier (double API) --------------------
        public static ClassificationResultD ClassifySequenceD(List<double> seq)
        {
            if (seq == null || seq.Count < 3)
                return new ClassificationResultD("unknown", "onbekend", "", "", null, 0.0);

            // Fractionele meetkundige check
            var ratios = new List<double>();
            for (int i = 0; i + 1 < seq.Count; i++)
            {
                double a = seq[i];
                double b = seq[i + 1];
                ratios.Add(a == 0 ? double.PositiveInfinity : b / a);
            }
            var rClean = ratios.Where(x => !double.IsInfinity(x) && !double.IsNaN(x)).ToList();
            if (rClean.Count >= 2 && IsConst(rClean))
            {
                double r = rClean[^1];
                double nx = seq[^1] * r;
                string key = "geometric_fractional";
                var meta = new Dictionary<string, object> { ["r"] = r };
                string vb = RecognitionExampleD(seq, key, meta, nx);
                return new ClassificationResultD(
                    key, "meetkundig (fractionele factor)",
                    RecognitionTip(key, meta), vb, nx, 0.93);
            }

            // (optioneel) Als de reeks toevallig geheel is, kun je converteren en long-classifier aanroepen.
            // Hier houden we het lean: geen verdere double-typen.
            return new ClassificationResultD("unknown", "onbekend", "", "", null, 0.30);
        }

        // -------------------- public API (long) --------------------
        public static SequenceQuestion GenerateRawQuestion(
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            Random? rng = seed.HasValue ? new Random(seed.Value) : s_random;
            var pool = (types != null && types.Any())
                ? types.Where(t => GEN_TYPES.ContainsKey(t)).ToList()
                : GEN_TYPES.Keys.ToList();

            if (pool.Count == 0) pool = GEN_TYPES.Keys.ToList();
            string choice = pool[RandIn(0, pool.Count - 1, rng)];
            return GEN_TYPES[choice](difficulty, rng, askMode);
        }

        public static GenerationResult GenerateNumberSequence(
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            var q = GenerateRawQuestion(difficulty, types, seed, askMode);
            string key = q.Meta.TryGetValue("type_key", out var tv) ? (Convert.ToString(tv) ?? q.Type) : q.Type;

            string herken = RecognitionTip(key, q.Meta);
            string voorbeeld = RecognitionExample(q.Reeks, key, q.Meta, q.Antwoord);

            q.Meta["herken"] = herken;
            q.Meta["voorbeeld"] = voorbeeld;

            return new GenerationResult(
                q.Type, q.Vraag, q.Reeks, q.Antwoord, q.Uitleg,
                herken ?? "", voorbeeld ?? "", q.Meta);
        }

        public static List<GenerationResult> GenerateBatch(
            int n = 10,
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            var outp = new List<GenerationResult>();
            Random? rng = seed.HasValue ? new Random(seed.Value) : null;
            for (int i = 0; i < n; i++)
            {
                int? itemSeed = rng != null ? rng.Next() : (int?)null;
                outp.Add(GenerateNumberSequence(difficulty, types, itemSeed, askMode));
            }
            return outp;
        }

        public static List<long> MakeChoices(SequenceQuestion q, int k = 4, int? seed = null)
            => MakeChoicesCore(q.Type, q.Reeks, q.Antwoord, k, seed);

        public static List<long> MakeChoices(GenerationResult q, int k = 4, int? seed = null)
            => MakeChoicesCore(q.Type, q.Reeks, q.Antwoord, k, seed);

        private static List<long> MakeChoicesCore(string type, List<long> seq, long correct, int k, int? seed)
        {
            Random rng = seed.HasValue ? new Random(seed.Value) : s_random;

            var distractors = new HashSet<long>();
            long step = Math.Max(1, Math.Abs(correct) / 20);
            foreach (int d in new[] { 1, 2, 3, 5, 8 })
            {
                distractors.Add(correct + d * step);
                distractors.Add(correct - d * step);
            }

            string t = type.ToLowerInvariant();
            if (t.Contains("rekenkundig") || t.Contains("verschil"))
            {
                if (seq.Count >= 2)
                {
                    long lastDiff = seq[^1] - seq[^2];
                    distractors.Add(seq[^1] + lastDiff);
                    distractors.Add(seq[^1] - lastDiff);
                }
            }
            if (t.Contains("meetkundig") || t.Contains("factor"))
            {
                if (seq.Count >= 2 && seq[^2] != 0)
                {
                    int rEst = (int)Math.Round((double)seq[^1] / (double)seq[^2]);
                    distractors.Add(seq[^1] * rEst);
                    distractors.Add(seq[^1] * (rEst + 1));
                    distractors.Add(seq[^1] * Math.Max(1, rEst - 1));
                }
            }
            if (t.Contains("fibonacci"))
            {
                distractors.Add(seq[^1] + seq[^1]);
                long prev2 = seq.Count >= 3 ? seq[^3] : seq[^2];
                distractors.Add(seq[^1] + prev2);
            }
            if (t.Contains("kwadra") || t.Contains("n^2"))
            {
                distractors.Add(correct + 2);
                distractors.Add(correct - 2);
            }
            if (t.Contains("priem"))
            {
                distractors.Add(correct + 1);
                distractors.Add(correct + 2);
            }
            if (t.Contains("cijfer") || t.Contains("digit_product"))
            {
                long last = seq[^1];
                var ds = Math.Abs(last).ToString().Select(ch => ch - '0').ToArray();
                if (ds.Length >= 2)
                {
                    long missOne = 1;
                    for (int i = 1; i < ds.Length; i++) missOne *= ds[i];
                    distractors.Add(missOne);
                }
                long plusOne = 1;
                for (int i = 0; i < ds.Length; i++)
                {
                    int d = ds[i];
                    if (i == 0 && d < 9) d++;
                    plusOne *= d;
                }
                distractors.Add(plusOne);
            }

            distractors.Remove(correct);
            while (distractors.Count < Math.Max(3, k - 1))
                distractors.Add(correct + RandIn(-15, 15, rng));

            var list = distractors.ToList();
            Shuffle(list, rng);

            var options = list.Take(k - 1).ToList();
            options.Add(correct);
            Shuffle(options, rng);
            return options;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // -------------------- public API (double) --------------------
        public static SequenceQuestionD GenerateRawQuestionD(
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            Random? rng = seed.HasValue ? new Random(seed.Value) : s_random;
            var pool = (types != null && types.Any())
                ? types.Where(t => GEN_TYPES_D.ContainsKey(t)).ToList()
                : GEN_TYPES_D.Keys.ToList();

            if (pool.Count == 0) pool = GEN_TYPES_D.Keys.ToList();
            string choice = pool[RandIn(0, pool.Count - 1, rng)];
            return GEN_TYPES_D[choice](difficulty, rng, askMode);
        }

        public static GenerationResultD GenerateNumberSequenceD(
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            var q = GenerateRawQuestionD(difficulty, types, seed, askMode);
            string key = q.Meta.TryGetValue("type_key", out var tv) ? (Convert.ToString(tv) ?? q.Type) : q.Type;

            string herken = RecognitionTip(key, q.Meta);
            string voorbeeld = RecognitionExampleD(q.Reeks, key, q.Meta, q.Antwoord);

            q.Meta["herken"] = herken;
            q.Meta["voorbeeld"] = voorbeeld;

            return new GenerationResultD(
                q.Type, q.Vraag, q.Reeks, q.Antwoord, q.Uitleg,
                herken ?? "", voorbeeld ?? "", q.Meta);
        }

        public static List<GenerationResultD> GenerateBatchD(
            int n = 10,
            string difficulty = "medium",
            IEnumerable<string>? types = null,
            int? seed = null,
            string askMode = "both")
        {
            var outp = new List<GenerationResultD>();
            Random? rng = seed.HasValue ? new Random(seed.Value) : null;
            for (int i = 0; i < n; i++)
            {
                int? itemSeed = rng != null ? rng.Next() : (int?)null;
                outp.Add(GenerateNumberSequenceD(difficulty, types, itemSeed, askMode));
            }
            return outp;
        }

        public static List<double> MakeChoicesD(SequenceQuestionD q, int k = 4, int? seed = null)
            => MakeChoicesCoreD(q.Type, q.Reeks, q.Antwoord, k, seed);

        public static List<double> MakeChoicesD(GenerationResultD q, int k = 4, int? seed = null)
            => MakeChoicesCoreD(q.Type, q.Reeks, q.Antwoord, k, seed);

        private static List<double> MakeChoicesCoreD(string type, List<double> seq, double correct, int k, int? seed)
        {
            Random rng = seed.HasValue ? new Random(seed.Value) : s_random;

            var distractors = new HashSet<double>();
            double step = Math.Max(1.0, Math.Abs(correct) / 20.0);
            foreach (double d in new[] { 1.0, 1.5, 2.0, 2.5, 5.0 })
            {
                distractors.Add(correct + d * step);
                distractors.Add(correct - d * step);
            }

            string t = type.ToLowerInvariant();
            if (t.Contains("frac"))
            {
                if (seq.Count >= 2)
                {
                    double r = seq[^1] / seq[^2];
                    distractors.Add(seq[^1] * r * 0.9);
                    distractors.Add(seq[^1] * (r + 0.5));
                    distractors.Add(seq[^1] * Math.Max(0.1, r - 0.5));
                }
            }

            distractors.Remove(correct);
            while (distractors.Count < Math.Max(3, k - 1))
                distractors.Add(correct + RandIn(-15, 15, rng));

            var list = distractors.ToList();
            list.Sort();
            var options = list.Take(k - 1).ToList();
            options.Add(correct);
            options = options.OrderBy(_ => rng.Next()).ToList();
            return options;
        }
    }
}
