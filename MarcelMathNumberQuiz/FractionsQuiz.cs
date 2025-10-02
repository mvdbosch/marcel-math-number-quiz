using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Type-categorieën die in het menu getoond worden.
/// </summary>
public enum ProblemType
{
    // Procenten
    PercentageOfNumber,         // p% van N = ?
    Percentage_FindPercent,     // ?% van N = M
    Percentage_FindBase,        // p% van ? = M
    Percentage_WhatPercent,     // A is ?% van B

    // Breuken i.c.m. hele getallen
    Fraction_Multiply_ByInt,    // a/b × n
    Fraction_Add_Int,           // a/b ± n

    // Nieuwe breuk-varianten
    Fraction_Multiply,          // a/b × c/d
    Fraction_Divide,            // a/b ÷ c/d
    Fraction_Add,               // a/b + c/d
    Fraction_Subtract,          // a/b − c/d

    // Verhoudingen
    Ratio_CrossSolve,           // a/? = ?/b

    // Algemene rekenexpressie
    Arithmetic_Expression
}

/// <summary>
/// Vraagmodel
/// </summary>
public class Question
{
    public string QuestionText { get; }
    public List<string> Options { get; }
    public string CorrectAnswer { get; }
    public string Explanation { get; }
    public ProblemType Type { get; }

    public Question(string questionText, string correctAnswer, IEnumerable<string> options, string explanation, ProblemType type)
    {
        QuestionText = questionText;
        CorrectAnswer = correctAnswer;
        Options = options.ToList();
        Explanation = explanation ?? "";
        Type = type;
    }
}

/// <summary>
/// Generator voor rekensommen (procenten, breuken, verhoudingen, etc.)
/// </summary>
public class HfmVitQuestionGenerator
{
    private readonly Random _rng;
    public HfmVitQuestionGenerator(int? seed = null) => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

    /// <summary>Publieke entry</summary>
    public Question Generate(ProblemType type) =>
        type switch
        {
            // Procenten
            ProblemType.PercentageOfNumber => GenPercentageOfNumber(),
            ProblemType.Percentage_FindPercent => GenFindPercentGivenResult(),
            ProblemType.Percentage_FindBase => GenFindBaseGivenPercent(),
            ProblemType.Percentage_WhatPercent => GenWhatPercentOfNumber(),

            // Breuken met int
            ProblemType.Fraction_Multiply_ByInt => GenFractionTimesInteger(),
            ProblemType.Fraction_Add_Int => GenFractionPlusMinusInteger(),

            // Nieuwe breuk-varianten
            ProblemType.Fraction_Multiply => GenFractionTimesFraction(),
            ProblemType.Fraction_Divide => GenFractionDivideFraction(),
            ProblemType.Fraction_Add => GenFractionPlusFraction(),
            ProblemType.Fraction_Subtract => GenFractionMinusFraction(),

            // Verhoudingen
            ProblemType.Ratio_CrossSolve => GenRatioCrossSolve(),

            // Algemene expressie
            ProblemType.Arithmetic_Expression => GenArithmeticExpression(),

            _ => GenPercentageOfNumber()
        };

    /* ================== Helpers ================== */

    // NL-weergave: integer zonder komma; anders max 2 decimalen; '.' -> ','
    private static string FmtDec(decimal v)
    {
        string s = v % 1m == 0m ? v.ToString("0", CultureInfo.InvariantCulture)
                                : v.ToString("0.##", CultureInfo.InvariantCulture);
        return s.Replace('.', ',');
    }
    private static string FmtPct(decimal p)
    {
        string s = p % 1m == 0m ? p.ToString("0", CultureInfo.InvariantCulture)
                                : p.ToString("0.##", CultureInfo.InvariantCulture);
        return s.Replace('.', ',');
    }
    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }
    private static (int num, int den) Simplify(int num, int den)
    {
        if (den == 0) return (num, 1);
        int g = Gcd(num, den);
        num /= g; den /= g;
        if (den < 0) { den = -den; num = -num; }
        return (num, den);
    }
    private static string FractionToNiceDecimal(int num, int den)
    {
        var (n, d) = Simplify(num, den);
        decimal v = d == 0 ? 0m : (decimal)n / d;
        return FmtDec(v);
    }
    private (int n, int d) RandProperFraction(int minDen = 3, int maxDen = 12, bool allowNegative = false)
    {
        int d = _rng.Next(minDen, maxDen + 1);
        int n = _rng.Next(1, d);
        if (allowNegative && _rng.Next(2) == 0) n = -n;
        return Simplify(n, d);
    }
    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

    /* ============= 1) p% van N = ? ============= */
    private Question GenPercentageOfNumber()
    {
        int[] P = { 5, 10, 12, 15, 20, 25, 30, 33, 40, 50, 60, 66, 75, 80, 90 };
        int p = P[_rng.Next(P.Length)];
        int baseN = _rng.Next(40, 401);

        decimal correct = baseN * p / 100m;
        string correctS = FmtDec(correct);

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void Add(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        Add(correct + 1m); Add(correct - 1m);
        Add(baseN * (p + 5) / 100m); Add(baseN * (p - 5) / 100m);
        Add(baseN * (p + 10) / 100m); Add(baseN * (p - 10) / 100m);
        if (p != 0) Add(baseN / (decimal)p);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = _rng.Next(5, 26) / 10m; // 0,5..2,5
            Add(correct + (_rng.Next(2) == 0 ? -d : d));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string qText = $"{p}% van {baseN} = ?";
        string explanation = $"{baseN} × {p}/100 = {correctS}.";

        return new Question(qText, correctS, list, explanation, ProblemType.PercentageOfNumber);
    }

    /* ============= 2) ?% van N = M ============= */
    private Question GenFindPercentGivenResult()
    {
        int baseN = _rng.Next(40, 401);
        int[] P = { 5, 10, 12, 15, 20, 25, 30, 33, 40, 50, 60, 66, 75, 80, 90 };
        int pTrue = P[_rng.Next(P.Length)];
        decimal M = baseN * pTrue / 100m;

        decimal correct = 100m * M / baseN;
        string correctS = FmtPct(correct) + " %";

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddPct(decimal v) { var s = FmtPct(v) + " %"; if (!opts.Contains(s)) opts.Add(s); }

        AddPct(correct + 1m); AddPct(correct - 1m);
        AddPct(correct + 5m); AddPct(correct - 5m);
        AddPct(correct + 10m); AddPct(correct - 10m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = _rng.Next(5, 26) / 10m;
            AddPct(correct + (_rng.Next(2) == 0 ? -d : d));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string qText = $"?% van {baseN} = {FmtDec(M)}";
        string explanation = $"p = 100 × {FmtDec(M)}/{FmtDec(baseN)} = {FmtPct(correct)} %.";

        return new Question(qText, correctS, list, explanation, ProblemType.Percentage_FindPercent);
    }

    /* ============= 3) p% van ? = M ============= */
    private Question GenFindBaseGivenPercent()
    {
        int[] P = { 5, 10, 12, 15, 20, 25, 30, 33, 40, 50, 60, 66, 75, 80, 90 };
        int p = P[_rng.Next(P.Length)];
        int baseTrue = _rng.Next(40, 401);
        decimal M = baseTrue * p / 100m;

        decimal correct = 100m * M / p;
        string correctS = FmtDec(correct);

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void Add(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        Add(correct + 1m); Add(correct - 1m);
        Add(correct + 5m); Add(correct - 5m);
        Add(correct * 110m / 100m); Add(correct * 90m / 100m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = _rng.Next(5, 26) / 10m;
            Add(correct + (_rng.Next(2) == 0 ? -d : d));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string qText = $"{p}% van ? = {FmtDec(M)}";
        string explanation = $"Geheel = {FmtDec(M)} ÷ ({FmtPct(p)}%) = {FmtDec(M)} ÷ ({p}/100) = {correctS}.";

        return new Question(qText, correctS, list, explanation, ProblemType.Percentage_FindBase);
    }

    /* ============= 4) A is ?% van B ============= */
    private Question GenWhatPercentOfNumber()
    {
        int B = _rng.Next(40, 401);                   // geheel
        int[] numerators = { 1, 2, 3, 4, 5, 6, 8, 9 }; // A ≈ nette breuken van B
        int k = numerators[_rng.Next(numerators.Length)];
        int A = Math.Max(1, B / k);

        decimal correct = 100m * A / B;
        string correctS = FmtPct(correct) + " %";

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };

        string ToPct(decimal v) => FmtPct(v) + " %";
        void AddPct(decimal v)
        {
            var s = ToPct(v);
            if (!opts.Contains(s)) opts.Add(s);
        }

        // --- 1) Offsets met kleine jitter (breekt identieke decimalen) ---
        decimal Jitter() => (_rng.Next(2) == 0 ? -1 : 1) * (_rng.Next(2, 6) / 100m); // ±0,02 .. ±0,05
        AddPct(correct + 1m + Jitter());
        AddPct(correct - 1m + Jitter());
        AddPct(correct + 5m + Jitter());
        AddPct(correct - 5m + Jitter());

        // --- 2) Ratio slips: verander teller/noemer met 1 ---
        if (A + 1 <= B - 1) AddPct(100m * (A + 1) / B);
        if (A - 1 >= 1) AddPct(100m * (A - 1) / B);
        if (B + 1 > 0) AddPct(100m * A / (B + 1));
        if (B - 1 > 0) AddPct(100m * A / (B - 1));

        // --- 3) Rounding varianten (naar integer / 1 decimaal) ---
        AddPct(Math.Round(correct, 0));               // afgerond op hele %
        AddPct(Math.Round(correct, 1));               // 1 decimaal

        // Vul aan tot 4 met kleine willekeurige variaties
        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = (_rng.Next(5, 26) / 10m);     // 0,5 .. 2,5
            decimal sign = _rng.Next(2) == 0 ? -1m : 1m;
            AddPct(correct + sign * d + Jitter());
        }

        // Maak lijst en shuffle
        var list = opts.ToList();
        Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();

        // Safety net: correct erin
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        // --- 4) Enforce: niet alle dezelfde decimalen ---
        // verzamel decimal-suffix (2 cijfers na komma) indien aanwezig
        string DecPart(string s)
        {
            int i = s.IndexOf(',');
            if (i < 0) return "";
            var p = s.IndexOf(' ', i);
            var frac = p < 0 ? s[(i + 1)..] : s[(i + 1)..p];
            return frac; // bv. "22"
        }

        var decSet = new HashSet<string>(list.Select(DecPart).Where(x => x.Length > 0));
        if (decSet.Count <= 1)
        {
            // Forceer minstens één optie met andere decimalen
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == correctS) continue;
                // parse % waarde terug naar decimal
                var raw = list[i].Replace(" %", "");
                if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                {
                    val += 0.13m; // kleine verschuiving om decimalen te breken
                    list[i] = ToPct(val);
                    break;
                }
            }
        }

        string qText = $"{FmtDec(A)} is ?% van {FmtDec(B)}";
        string explanation = $"p = 100 × {FmtDec(A)}/{FmtDec(B)} = {FmtPct(correct)} %.";

        return new Question(qText, correctS, list, explanation, ProblemType.Percentage_WhatPercent);
    }


    /* ============= 5) Breuk × geheel ============= */
    private Question GenFractionTimesInteger()
    {
        var (num, den) = RandProperFraction(3, 12, allowNegative: false);
        int n = _rng.Next(2, 13);
        string qText = $"{num}/{den} × {n} = ?";

        string correctS = FractionToNiceDecimal(num * n, den);
        var correctVal = (decimal)(num * n) / den;

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void Add(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        Add((decimal)n / den * num);   // fout: n/den × num
        Add((decimal)num / den + n);   // fout: optellen i.p.v. vermenigvuldigen
        Add(correctVal + 0.5m);
        Add(correctVal - 0.5m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = _rng.Next(1, 5) / 10m;
            Add(correctVal + (_rng.Next(2) == 0 ? -d : d));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"{num}/{den} × {n} = {num}×{n}/{den} = {FmtDec(correctVal)}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Multiply_ByInt);
    }

    /* ============= 6) Breuk ± geheel ============= */
    private Question GenFractionPlusMinusInteger()
    {
        var (num, den) = RandProperFraction(3, 9, allowNegative: true);
        int whole = _rng.Next(1, 7) * (_rng.Next(2) == 0 ? 1 : -1);
        bool add = _rng.Next(2) == 0;
        string op = add ? "+" : "−";
        string qText = $"{num}/{den} {op} {whole} = ?";

        decimal correctVal = add ? (decimal)num / den + whole
                                 : (decimal)num / den - whole;
        string correctS = FmtDec(correctVal);

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void Add(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        Add((decimal)num / den);
        Add((decimal)whole);
        Add(correctVal + 0.5m);
        Add(correctVal - 0.5m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal d = _rng.Next(1, 5) / 10m;
            Add(correctVal + (_rng.Next(2) == 0 ? -d : d));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = add
            ? $"{num}/{den} + {whole} = {FmtDec((decimal)num / den)} + {FmtDec(whole)} = {correctS}."
            : $"{num}/{den} − {whole} = {FmtDec((decimal)num / den)} − {FmtDec(whole)} = {correctS}.";

        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Add_Int);
    }

    /* ============= 7) Breuk × breuk ============= */
    private Question GenFractionTimesFraction()
    {
        var (a, b) = RandProperFraction(3, 12, allowNegative: false);
        var (c, d) = RandProperFraction(3, 12, allowNegative: false);

        string qText = $"{a}/{b} × {c}/{d} = ?";

        // correct: (a*c)/(b*d)
        string correctS = FractionToNiceDecimal(a * c, b * d);
        decimal correctVal = (decimal)a * c / (b * d);

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddDec(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        // typische fouten:
        AddDec((decimal)a / b + (decimal)c / d);         // optellen i.p.v. vermenigvuldigen
        AddDec((decimal)(a * d) / (b * c));              // omkering tweede breuk vergeten (verwisseling)
        AddDec((decimal)a / d);                          // kruis-simplificatie fout
        AddDec((decimal)c / b);

        // rond correcte waarde
        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal dlt = _rng.Next(1, 4) / 10m; // 0,1..0,3
            AddDec(correctVal + (_rng.Next(2) == 0 ? -dlt : dlt));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"{a}/{b} × {c}/{d} = {a}×{c}/{b}×{d} = {a * c}/{b * d} = {correctS}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Multiply);
    }

    /* ============= 8) Breuk ÷ breuk ============= */
    private Question GenFractionDivideFraction()
    {
        var (a, b) = RandProperFraction(3, 12, allowNegative: false);
        var (c, d) = RandProperFraction(3, 12, allowNegative: false);

        string qText = $"{a}/{b} ÷ {c}/{d} = ?";

        // correct: (a/b) ÷ (c/d) = (a/b) × (d/c) = (a*d)/(b*c)
        string correctS = FractionToNiceDecimal(a * d, b * c);
        decimal correctVal = (decimal)a * d / (b * c);

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddDec(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        // typische fouten:
        AddDec((decimal)a * c / (b * d)); // vergeten te inverteren: (a*c)/(b*d)
        AddDec((decimal)a / b + (decimal)c / d);
        AddDec((decimal)a / c);           // verkeerde kruisdeling

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal dlt = _rng.Next(1, 4) / 10m;
            AddDec(correctVal + (_rng.Next(2) == 0 ? -dlt : dlt));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"{a}/{b} ÷ {c}/{d} = {a}/{b} × {d}/{c} = {a * d}/{b * c} = {correctS}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Divide);
    }

    /* ============= 9) Breuk + breuk ============= */
    private Question GenFractionPlusFraction()
    {
        var (a, b) = RandProperFraction(3, 12, allowNegative: true);
        var (c, d) = RandProperFraction(3, 12, allowNegative: true);

        string qText = $"{a}/{b} + {c}/{d} = ?";

        int l = Lcm(b, d);
        int na = a * (l / b);
        int nc = c * (l / d);
        int sumNum = na + nc;
        string correctS = FractionToNiceDecimal(sumNum, l);
        decimal correctVal = (decimal)sumNum / l;

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddDec(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        // typische fouten:
        AddDec((decimal)(a + c) / (b + d)); // tellers + tellers, noemers + noemers
        AddDec((decimal)a / b + (decimal)c / d + 0.5m); // inschatting
        AddDec((decimal)a / b + (decimal)c / d - 0.5m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal dlt = _rng.Next(1, 4) / 10m;
            AddDec(correctVal + (_rng.Next(2) == 0 ? -dlt : dlt));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"Gelijke noemer: LCM({b},{d})={l}. ⇒ {a}/{b}={na}/{l}, {c}/{d}={nc}/{l}. " +
                             $"Som: ({na}+{nc})/{l} = {sumNum}/{l} = {correctS}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Add);
    }

    /* ============= 10) Breuk − breuk ============= */
    private Question GenFractionMinusFraction()
    {
        var (a, b) = RandProperFraction(3, 12, allowNegative: true);
        var (c, d) = RandProperFraction(3, 12, allowNegative: true);

        string qText = $"{a}/{b} − {c}/{d} = ?";

        int l = Lcm(b, d);
        int na = a * (l / b);
        int nc = c * (l / d);
        int diffNum = na - nc;
        string correctS = FractionToNiceDecimal(diffNum, l);
        decimal correctVal = (decimal)diffNum / l;

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddDec(decimal v) { var s = FmtDec(v); if (!opts.Contains(s)) opts.Add(s); }

        // typische fouten:
        AddDec((decimal)(a - c) / (b - d));
        AddDec((decimal)a / b - (decimal)c / d + 0.5m);
        AddDec((decimal)a / b - (decimal)c / d - 0.5m);

        int guard = 0;
        while (opts.Count < 4 && guard++ < 50)
        {
            decimal dlt = _rng.Next(1, 4) / 10m;
            AddDec(correctVal + (_rng.Next(2) == 0 ? -dlt : dlt));
        }

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"Gelijke noemer: LCM({b},{d})={l}. ⇒ {a}/{b}={na}/{l}, {c}/{d}={nc}/{l}. " +
                             $"Verschil: ({na}−{nc})/{l} = {diffNum}/{l} = {correctS}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Fraction_Subtract);
    }

    /* ============= 11) Verhoudingen ============= */
    private Question GenRatioCrossSolve()
    {
        int s = _rng.Next(3, 16);
        int k = _rng.Next(2, 11);
        int a = s * s;
        int b = k * k;
        int x = s * k;

        string qText = $"Los op: {a}/? = ?/{b}";
        string correctS = x.ToString();

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddInt(int v) { opts.Add(v.ToString()); }

        AddInt(x + 1); AddInt(Math.Max(1, x - 1));
        AddInt(x + 2); AddInt(Math.Max(1, x - 2));

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation = $"a/x = x/b ⇒ x² = a·b ⇒ x = √(a·b) = √({a}×{b}) = {x}.";
        return new Question(qText, correctS, list, explanation, ProblemType.Ratio_CrossSolve);
    }

    /* ============= 12) Algemene expressie ============= */
    private Question GenArithmeticExpression()
    {
        int A = _rng.Next(-5, 6);
        int B = _rng.Next(1, 6);
        int C = _rng.Next(1, 10);
        int D = _rng.Next(1, 10);

        bool plus = _rng.Next(2) == 0;
        string op = plus ? "+" : "−";

        int inner = C - D;
        int mult = B * inner;
        int result = plus ? (A + mult) : (A - mult);

        string qText = $"{A} {op} {B} × ({C} − {D}) = ?";
        string correctS = result.ToString();

        var opts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correctS };
        void AddInt(int v) { opts.Add(v.ToString()); }

        int wrong1 = plus ? (A + B + inner) : (A - B + inner);
        int wrong2 = plus ? (A + B * (C + D)) : (A - B * (C + D));
        int wrong3 = plus ? (A + (B * C) - D) : (A - (B * C) - D);

        AddInt(wrong1); AddInt(wrong2); AddInt(wrong3);
        AddInt(result + _rng.Next(-4, 5));

        var list = opts.ToList(); Shuffle(list, _rng);
        if (list.Count > 4) list = list.Take(4).ToList();
        if (!list.Contains(correctS)) { list[0] = correctS; Shuffle(list, _rng); }

        string explanation =
            $"Eerst haakjes: ({C} − {D}) = {inner}. " +
            $"Dan vermenigvuldigen: {B} × {inner} = {mult}. " +
            $"Daarna {A} {op} {mult} = {result}.";

        return new Question(qText, correctS, list, explanation, ProblemType.Arithmetic_Expression);
    }
}
