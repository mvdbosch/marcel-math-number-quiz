using System;
using System.Collections.Generic;
using System.Linq;

namespace MarcelMathNumberQuiz
{
    public enum ProblemType
    {
        FractionDivideByInteger,
        FractionMultiplyByInteger,
        FractionAdd,
        FractionSubtract,
        FractionMultiply,
        FractionDivide,
        FractionSimplify,
        PercentageOfNumber,
        PartOfNumber,
        RatioProportion,
        SquaresRoots,
        OrderOfOperations,
        // Extra:
        NegativesWithBrackets,
        MixedToImproper,
        ImproperToMixed,
        WorkRateTogether,
        // NIEUW:
        ProportionSameUnknown   // a/? = ?/b  (of ?/a = b/?)
    }

    public record Question(
        string QuestionText,
        string CorrectAnswer,
        IReadOnlyList<string> Options,
        string Explanation,
        ProblemType Type
    );

    // Kleine fraction helper (altijd vereenvoudigd)
    public readonly struct Frac : IEquatable<Frac>
    {
        public int N { get; }   // teller
        public int D { get; }   // noemer (>0)

        public Frac(int numerator, int denominator)
        {
            if (denominator == 0) throw new DivideByZeroException();
            if (denominator < 0) { numerator = -numerator; denominator = -denominator; }
            int g = Gcd(Math.Abs(numerator), denominator);
            N = numerator / g;
            D = denominator / g;
        }

        public static Frac FromInt(int x) => new Frac(x, 1);

        public static Frac operator +(Frac a, Frac b) => new Frac(a.N * b.D + b.N * a.D, a.D * b.D);
        public static Frac operator -(Frac a, Frac b) => new Frac(a.N * b.D - b.N * a.D, a.D * b.D);
        public static Frac operator *(Frac a, Frac b) => new Frac(a.N * b.N, a.D * b.D);
        public static Frac operator /(Frac a, Frac b)
        {
            if (b.N == 0) throw new DivideByZeroException();
            return new Frac(a.N * b.D, a.D * b.N);
        }

        public override string ToString() => D == 1 ? $"{N}" : $"{N}/{D}";
        public bool Equals(Frac other) => N == other.N && D == other.D;
        public override int GetHashCode() => HashCode.Combine(N, D);

        public (int whole, Frac proper) ToMixed()
        {
            int whole = N / D;
            int rem = Math.Abs(N % D);
            return (whole, new Frac(rem, D));
        }

        public static int Gcd(int a, int b)
        {
            while (b != 0) { int t = a % b; a = b; b = t; }
            return Math.Max(a, 1);
        }
    }

    public class HfmVitQuestionGenerator
    {
        private readonly Random _rng;
        public HfmVitQuestionGenerator(int? seed = null) => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Format decimal als NL-weergave: 16 of 15,5 of 15,25
        private static string FmtDec(decimal v)
        {
            var s = v % 1 == 0 ? v.ToString("0") : v.ToString("0.##");
            return s.Replace('.', ','); // NL-komma
        }

        // Shuffle helper
        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public Question Generate(ProblemType? forcedType = null)
        {
            var type = forcedType ?? RandomEnum<ProblemType>();
            return type switch
            {
                ProblemType.FractionDivideByInteger => GenFractionDivideByInteger(),
                ProblemType.FractionMultiplyByInteger => GenFractionMultiplyByInteger(),
                ProblemType.FractionAdd => GenFractionAddSub(add: true),
                ProblemType.FractionSubtract => GenFractionAddSub(add: false),
                ProblemType.FractionMultiply => GenFractionMulDiv(multiply: true),
                ProblemType.FractionDivide => GenFractionMulDiv(multiply: false),
                ProblemType.FractionSimplify => GenFractionSimplify(),
                ProblemType.PercentageOfNumber => GenPercentageOfNumber(),
                ProblemType.PartOfNumber => GenPartOfNumber(),
                ProblemType.RatioProportion => GenRatioProportion(),
                ProblemType.SquaresRoots => GenSquaresRoots(),
                ProblemType.OrderOfOperations => GenOrderOfOperations(),
                ProblemType.NegativesWithBrackets => GenNegativesWithBrackets(),
                ProblemType.MixedToImproper => GenMixedToImproper(),
                ProblemType.ImproperToMixed => GenImproperToMixed(),
                ProblemType.WorkRateTogether => GenWorkRateTogether(),
                // NIEUW:
                ProblemType.ProportionSameUnknown => GenProportionSameUnknown(),
                _ => GenFractionDivideByInteger()
            };
        }

        // ----------------- Generators -----------------
        private Question GenFractionDivideByInteger()
        {
            var (n, d) = RandProperFraction();
            int k = RandIn(2, 9);
            Frac ans = new Frac(n, d) / Frac.FromInt(k);

            var q = $"{n}/{d} ÷ {k} = ?";
            var expl = $"Delen door {k} = vermenigvuldigen met 1/{k}: {n}/{d} × 1/{k} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                new Frac(n * k, d).ToString(),
                new Frac(n, d * k * k).ToString(),
                new Frac(d, n * k).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.FractionDivideByInteger);
        }

        private Question GenFractionMultiplyByInteger()
        {
            var (n, d) = RandProperFraction();
            int k = RandIn(2, 9);
            Frac ans = new Frac(n, d) * Frac.FromInt(k);

            var q = $"{n}/{d} × {k} = ?";
            var expl = $"Vermenigvuldig teller met {k}: {n}×{k}/{d} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                new Frac(n, d * k).ToString(),
                new Frac(n * k * k, d).ToString(),
                new Frac(d * k, n).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.FractionMultiplyByInteger);
        }

        private Question GenFractionAddSub(bool add)
        {
            var a = RandNiceFraction();
            var b = RandNiceFraction();
            if (a.D == b.D && a.N == b.N) b = new Frac(b.N + 1, b.D);

            Frac ans = add ? a + b : a - b;
            string op = add ? "+" : "−";
            var q = $"{a} {op} {b} = ?";
            var expl = $"Maak gelijke noemers en vereenvoudig: {a} {op} {b} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                new Frac(add ? a.N + b.N : a.N - b.N, a.D).ToString(),
                new Frac(add ? a.N + b.N : a.N - b.N, b.D).ToString(),
                new Frac(add ? a.N * b.D + b.N * a.D : a.N * b.D - b.N * a.D, a.D + b.D).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, add ? ProblemType.FractionAdd : ProblemType.FractionSubtract);
        }

        private Question GenFractionMulDiv(bool multiply)
        {
            var a = RandNiceFraction();
            var b = RandNiceFraction();
            Frac ans = multiply ? a * b : a / b;
            string op = multiply ? "×" : "÷";
            var q = $"{a} {op} {b} = ?";
            var expl = multiply
                ? $"Tellers× en noemers× → {ans}."
                : $"Delen door breuk = keer omgekeerde: {a} × {new Frac(b.D, b.N)} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                new Frac(a.N * b.D, a.D * b.N).ToString(),
                new Frac(a.N * b.N, a.D + b.D).ToString(),
                new Frac(a.N + b.N, a.D * b.D).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, multiply ? ProblemType.FractionMultiply : ProblemType.FractionDivide);
        }

        private Question GenFractionSimplify()
        {
            int g = RandIn(2, 12);
            int n = g * RandIn(2, 9);
            int d = g * RandIn(2, 9);
            var original = $"{n}/{d}";
            Frac ans = new Frac(n, d);

            var q = $"Vereenvoudig: {original}";
            var expl = $"Deel teller/noemer door {Frac.Gcd(n, d)} → {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                $"{n}/{d}",
                new Frac(n / 2, d / 2).ToString(),
                new Frac(n / 3, d).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.FractionSimplify);
        }

        private Question GenPercentageOfNumber()
        {
            // voorbeeldpercenten, voeg gerust meer toe
            int p = new[] { 5, 10, 12, 15, 20, 25, 30, 40, 50, 60, 75, 80, 90 }[_rng.Next(13)];
            int baseN = _rng.Next(40, 401);  // 40..400

            decimal correct = baseN * p / 100m;
            string correctS = FmtDec(correct);

            // Gebruik HashSet met stringvergelijking na formattering,
            // zodat we geen duplicaten krijgen door afronding/format.
            var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        correctS
    };

            // Typische afleiderpatronen
            void AddOpt(decimal v)
            {
                var s = FmtDec(v);
                if (!opts.Contains(s)) opts.Add(s);
            }

            // ±1 (afrond/inschattingsfout)
            AddOpt(correct + 1m);
            AddOpt(correct - 1m);

            // ±5 en ±10 %-punt verwisseling
            AddOpt(baseN * (p + 5) / 100m);
            AddOpt(baseN * (p - 5) / 100m);
            AddOpt(baseN * (p + 10) / 100m);
            AddOpt(baseN * (p - 10) / 100m);

            // Denkfout: delen door p i.p.v. 100
            if (p != 0) AddOpt(baseN / (decimal)p);

            // Vul tot 4 opties met kleine variaties rond het juiste antwoord,
            // steeds checkend op duplicaat na formattering.
            int guard = 0;
            while (opts.Count < 4 && guard++ < 50)
            {
                // variatie: ±(0.5 .. 2.5)
                decimal delta = (decimal)_rng.Next(5, 26) / 10m;
                AddOpt(correct + (_rng.Next(2) == 0 ? -delta : delta));
            }

            // Zorg dat we exact 4 opties hebben (1 correct + 3 afleiders)
            var list = new List<string>(opts);
            Shuffle(list, _rng);

            if (!list.Contains(correctS))
                list[0] = correctS; // veiligheidsnet (zou niet moeten gebeuren)

            // Beperk tot 4 en shuffle nog een keer
            if (list.Count > 4) list = list.Take(4).ToList();
            Shuffle(list, _rng);

            string qText = $"{p}% van {baseN} = ?";
            string explanation = $"{baseN} × {p}/100 = {correctS}.";

            return new Question(qText, correctS, list, explanation, ProblemType.PercentageOfNumber);
        }


        private Question GenPartOfNumber()
        {
            var (n, d) = RandProperFraction(maxDen: 12);
            int whole = RandIn(48, 240, step: 6);
            whole = (whole / d) * d;
            int ans = whole * n / d;

            string q = $"{n}/{d} van {whole} = ?";
            string expl = $"{whole} × {n}/{d} = {whole * n}/{d} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                (whole * d / n).ToString(),
                (whole * n).ToString(),
                (whole / d).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.PartOfNumber);
        }

        private Question GenRatioProportion()
        {
            int a = RandIn(2, 12);
            int b = RandIn(2, 12);
            int d = RandIn(8, 40);
            int lcm = Lcm(b, RandIn(2, 6));
            d = Math.Max(lcm, (d / lcm) * lcm);
            int x = a * d / b;

            string q = $"{a}:{b} = x:{d}.  Wat is x?";
            string expl = $"x = {a}×{d}/{b} = {x}.";

            var options = UniqueOptions(
                x.ToString(),
                (b * d / a).ToString(),
                (a + d).ToString(),
                (a * b).ToString()
            );
            return new Question(q, x.ToString(), options, expl, ProblemType.RatioProportion);
        }

        private Question GenSquaresRoots()
        {
            bool root = _rng.NextDouble() < 0.5;
            if (root)
            {
                int n = new[] { 81, 100, 121, 144, 169, 196, 225, 256, 289, 324 }.OrderBy(_ => _rng.Next()).First();
                int ans = (int)Math.Round(Math.Sqrt(n));
                string q = $"√{n} = ?";
                var options = UniqueOptions(
                    ans.ToString(), (ans - 1).ToString(), (ans + 1).ToString(), (n / 2).ToString()
                );
                return new Question(q, ans.ToString(), options, $"√{n} = {ans} omdat {ans}² = {n}.", ProblemType.SquaresRoots);
            }
            else
            {
                int n = RandIn(9, 18);
                int ans = n * n;
                string q = $"{n}² = ?";
                var options = UniqueOptions(
                    ans.ToString(), (n * (n + 1)).ToString(), (n * (n - 1)).ToString(), (2 * n).ToString()
                );
                return new Question(q, ans.ToString(), options, $"{n}² = {n}×{n} = {ans}.", ProblemType.SquaresRoots);
            }
        }

        private Question GenOrderOfOperations()
        {
            int a = RandIn(5, 20);
            int b = RandIn(2, 9);
            int c = RandIn(2, 9);
            int d = RandIn(1, 10);

            int ans = a + b * c - d;

            string q = $"{a} + {b} × {c} − {d} = ?";
            string expl = $"Eerst ×: {b}×{c} = {b * c}; dan {a} + {b * c} − {d} = {ans}.";

            var options = UniqueOptions(
                ans.ToString(),
                (a + b + c - d).ToString(),
                ((a + b) * c - d).ToString(),
                (a + b * c + d).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.OrderOfOperations);
        }

        // ----------------- NIEUW: a/? = ?/b -----------------
        private Question GenProportionSameUnknown()
        {
            // Kies oplossing x, en kies a als een delers van x^2 (zodat b = x^2/a geheel is)
            int x = RandIn(4, 20);
            int xsq = x * x;

            // Bepaal alle delers van x^2 (excl. 1 en x^2 voor iets interessantere opgaven)
            var divisors = Enumerable.Range(2, xsq - 2)
                                     .Where(d => xsq % d == 0)
                                     .ToList();
            if (divisors.Count == 0) divisors.Add(2); // safety

            int a = divisors[_rng.Next(divisors.Count)];
            int b = xsq / a;

            bool mirrored = _rng.NextDouble() < 0.5;
            string q = mirrored
                ? $"Los op: ?/{a} = {b}/?"
                : $"Los op: {a}/? = ?/{b}";

            string expl = $"Noem het onbekende x. Dan geldt x² = {a}·{b} = {xsq} ⇒ x = √{xsq} = {x}.";

            var options = UniqueOptions(
                x.ToString(),        // juist
                a.ToString(),        // verwar met a
                b.ToString(),        // verwar met b
                (2 * x).ToString()   // te groot (×2)
            );

            return new Question(q, x.ToString(), options, expl, ProblemType.ProportionSameUnknown);
        }

        // ----------------- Extra typen van eerder -----------------
        private Question GenNegativesWithBrackets()
        {
            int a = RandIn(2, 12);
            int b = RandIn(2, 9);
            int c = RandIn(1, 9);
            int d = RandIn(1, 9);

            bool variant = _rng.NextDouble() < 0.5;
            int ans;
            string q;
            string expl;

            if (variant)
            {
                ans = -a + b * (c - d);
                q = $"−{a} + {b} × ({c} − {d}) = ?";
                expl = $"Eerst haakjes: ({c}−{d}) = {c - d}; dan {b}×{c - d}; daarna optellen met −{a} → {ans}.";
            }
            else
            {
                ans = (a - b) - (-c) * d;
                q = $"({a} − {b}) − (−{c}) × {d} = ?";
                expl = $"(−{c})×{d} = −{c * d}. Dus ({a}−{b}) − (−{c * d}) = ({a - b}) + {c * d} = {ans}.";
            }

            var options = UniqueOptions(
                ans.ToString(),
                (-ans).ToString(),
                (ans + RandIn(-3, 3)).ToString(),
                (a + b * (c - d)).ToString()
            );
            return new Question(q, ans.ToString(), options, expl, ProblemType.NegativesWithBrackets);
        }

        private static string FormatMixed(int whole, Frac proper)
            => proper.N == 0 ? $"{whole}" : (whole == 0 ? proper.ToString() : $"{whole} {proper}");

        private Question GenMixedToImproper()
        {
            int whole = RandIn(1, 9);
            var (n, d) = RandProperFraction(maxDen: 12);
            var proper = new Frac(n, d);
            var improper = new Frac(whole * d + n, d);

            string q = $"Zet om naar onzuivere breuk: {FormatMixed(whole, proper)} = ?";
            string expl = $"{whole} {n}/{d} = ({whole}×{d}+{n})/{d} = {improper}.";

            var options = UniqueOptions(
                improper.ToString(),
                new Frac(whole * n + d, d).ToString(),
                new Frac(whole * d - n, d).ToString(),
                $"{whole}{proper}"
            );
            return new Question(q, improper.ToString(), options, expl, ProblemType.MixedToImproper);
        }

        private Question GenImproperToMixed()
        {
            int d = RandIn(3, 12);
            int k = RandIn(1, 5);
            int n = d * k + RandIn(1, d - 1);
            var f = new Frac(n, d);
            var (w, prop) = f.ToMixed();

            string q = $"Zet om naar gemengde breuk: {f} = ?";
            string expl = $"{n}/{d} = {w} en rest {prop.N}/{prop.D} → {FormatMixed(w, prop)}.";

            var options = UniqueOptions(
                FormatMixed(w, prop),
                new Frac(n - d, d).ToString(),
                $"{w}/{prop}",
                $"{prop} {w}"
            );
            return new Question(q, FormatMixed(w, prop), options, expl, ProblemType.ImproperToMixed);
        }

        private Question GenWorkRateTogether()
        {
            int a = new[] { 2, 3, 4, 5 }.OrderBy(_ => _rng.Next()).First();
            int b = new[] { 3, 4, 6, 8, 9, 10 }.OrderBy(_ => _rng.Next()).First();
            int k = new[] { 1, 2, 3 }.OrderBy(_ => _rng.Next()).First();
            int t1 = a * k;
            int t2 = b * k;

            int denom = a + b;
            int numer = a * b * k;
            int t = (numer % denom == 0) ? numer / denom : (a * b * k * 2) / (a + b);
            string unit = "uur";

            string q = $"Persoon A kan een klus in {t1} {unit} doen en persoon B in {t2} {unit}. Hoe lang duurt het samen (constante snelheid)?";
            string expl = $"1/t = 1/{t1} + 1/{t2} ⇒ t = {t1}×{t2}/({t1}+{t2}) = {t} {unit}.";

            var options = UniqueOptions(
                $"{t} {unit}",
                $"{t1 + t2} {unit}",
                $"{Math.Max(t1, t2)} {unit}",
                $"{t1 * t2} {unit}"
            );
            return new Question(q, $"{t} {unit}", options, expl, ProblemType.WorkRateTogether);
        }

        // ----------------- Utilities -----------------
        private static int Lcm(int a, int b) => a / Frac.Gcd(a, b) * b;

        private (int n, int d) RandProperFraction(int maxDen = 15)
        {
            int d = RandIn(3, maxDen);
            int n = RandIn(1, d - 1);
            return (n, d);
        }

        private Frac RandNiceFraction()
        {
            int d = RandIn(2, 12);
            int n = RandIn(-12, 12);
            if (n == 0) n = 1;
            return new Frac(n, d);
        }

        private int RandIn(int minInclusive, int maxInclusive, int step = 1)
        {
            int count = ((maxInclusive - minInclusive) / step) + 1;
            return minInclusive + step * _rng.Next(count);
        }

        private T RandomEnum<T>() where T : Enum
        {
            var values = Enum.GetValues(typeof(T));
            return (T)values.GetValue(_rng.Next(values.Length));
        }

        // Altijd 4 unieke opties, daarna schudden
        private IReadOnlyList<string> UniqueOptions(params string[] seeds)
        {
            string Norm(string s) =>
                (s ?? "").Trim().Replace(" ", "").Replace(",", ".").ToLowerInvariant();

            var opts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                var key = Norm(s);
                if (seen.Add(key)) opts.Add(s);
            }

            foreach (var s in seeds) TryAdd(s);

            while (opts.Count < 4)
            {
                var baseStr = opts.Count > 0 ? opts[^1] : seeds.FirstOrDefault() ?? "0";

                string MakeVariant(string s)
                {
                    var t = s.Trim();
                    bool hasHour = t.EndsWith(" uur", StringComparison.OrdinalIgnoreCase);
                    var core = hasHour ? t[..^4].Trim() : t;

                    if (int.TryParse(core, out int iv))
                    {
                        int delta;
                        string candidate;
                        do
                        {
                            delta = RandIn(-4, 4);
                            if (delta == 0) delta = 1;
                            candidate = (iv + delta) + (hasHour ? " uur" : "");
                        } while (seen.Contains(Norm(candidate)));
                        return candidate;
                    }

                    var parts = core.Split('/');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int a) &&
                        int.TryParse(parts[1], out int b) && b != 0)
                    {
                        int na = a, nb = b;
                        if (_rng.NextDouble() < 0.5)
                        {
                            do { na = a + (_rng.Next(2) == 0 ? -1 : 1); } while (na == 0);
                        }
                        else
                        {
                            do { nb = b + (_rng.Next(2) == 0 ? -1 : 1); } while (nb == 0);
                        }
                        var v = new Frac(na, nb).ToString();
                        if (!seen.Contains(Norm(v))) return v;
                    }

                    string alt = t + "'";
                    int tries = 0;
                    while (seen.Contains(Norm(alt)) && tries++ < 5) alt += "'";
                    return alt;
                }

                TryAdd(MakeVariant(baseStr));

                if (opts.Count < 4 && seeds.Length > 0)
                    TryAdd(MakeVariant(seeds[0]));
            }

            for (int i = opts.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (opts[i], opts[j]) = (opts[j], opts[i]);
            }
            return opts;
        }
    }
}