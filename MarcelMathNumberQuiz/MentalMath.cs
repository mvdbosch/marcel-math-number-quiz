using System;
using System.Collections.Generic;
using System.Linq;

namespace MarcelMathNumberQuiz
{
    public enum MentalType
    {
        Add, Subtract, Multiply, Divide, Power, Root
    }

    // UI-friendly payload voor één vraag
    public record MMQuestion(
        string QuestionText,
        string CorrectAnswer,
        IReadOnlyList<string> Options,
        string Explanation,
        MentalType Type
    );

    // Plug-in punt: de UI vraagt alleen via deze interface om vragen
    public interface IMentalQuestionProvider
    {
        MMQuestion Generate(string difficulty, MentalType? forced = null);
    }

    // Referentie-implementatie (mag je vrij vervangen door eigen logica)
    public sealed class MentalMathGenerator : IMentalQuestionProvider
    {
        private readonly Random _rng;
        public MentalMathGenerator(int? seed = null) => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        public MMQuestion Generate(string difficulty, MentalType? forced = null)
        {
            var type = forced ?? RandomEnum<MentalType>();
            return type switch
            {
                MentalType.Add => GenAdd(difficulty),
                MentalType.Subtract => GenSub(difficulty),
                MentalType.Multiply => GenMul(difficulty),
                MentalType.Divide => GenDiv(difficulty),
                MentalType.Power => GenPow(difficulty),
                MentalType.Root => GenRoot(difficulty),
                _ => GenAdd(difficulty)
            };
        }

        private (int min, int max) Range(string diff)
            => diff switch
            {
                "easy" => (5, 50),
                "hard" => (200, 999),
                _ => (30, 199) // medium
            };

        private MMQuestion GenAdd(string diff)
        {
            var (min, max) = Range(diff);
            int a = Rand(min, max);
            int b = Rand(min, max);
            int ans = a + b;

            var opts = UniqueOptions(ans.ToString(),
                (ans + Rand(-15, -1)).ToString(),
                (ans + Rand(1, 15)).ToString(),
                (a + b + Rand(-9, 9)).ToString());

            return new MMQuestion($"{a} + {b} = ?", ans.ToString(), opts, $"Optellen: {a} + {b} = {ans}.", MentalType.Add);
        }

        private MMQuestion GenSub(string diff)
        {
            var (min, max) = Range(diff);
            int a = Rand(min, max);
            int b = Rand(min, max);
            if (diff != "hard" && a < b) (a, b) = (b, a);
            int ans = a - b;

            var opts = UniqueOptions(ans.ToString(),
                (ans + Rand(-15, -1)).ToString(),
                (ans + Rand(1, 15)).ToString(),
                (a - b + Rand(-9, 9)).ToString());

            return new MMQuestion($"{a} − {b} = ?", ans.ToString(), opts, $"Aftrekken: {a} − {b} = {ans}.", MentalType.Subtract);
        }

        private MMQuestion GenMul(string diff)
        {
            int a, b;
            if (diff == "easy") { a = Rand(2, 12); b = Rand(2, 12); }
            else if (diff == "hard") { a = Rand(12, 25); b = Rand(12, 25); }
            else { a = Rand(6, 19); b = Rand(6, 19); }

            int ans = a * b;

            var opts = UniqueOptions(ans.ToString(),
                (a * (b + 1)).ToString(),
                (a * (b - 1)).ToString(),
                (ans + Rand(-20, 20)).ToString());

            return new MMQuestion($"{a} × {b} = ?", ans.ToString(), opts, $"Vermenigvuldigen: {a}×{b} = {ans}.", MentalType.Multiply);
        }

        private MMQuestion GenDiv(string diff)
        {
            int b = diff == "easy" ? Rand(2, 12) : (diff == "hard" ? Rand(12, 25) : Rand(6, 19));
            int q = Rand(2, diff == "hard" ? 40 : 20);
            int a = b * q;
            int ans = q;

            var opts = UniqueOptions(ans.ToString(),
                (q + 1).ToString(),
                Math.Max(1, q - 1).ToString(),
                (q + Rand(-5, 5)).ToString());

            return new MMQuestion($"{a} ÷ {b} = ?", ans.ToString(), opts, $"Delen: {a}÷{b} = {q}.", MentalType.Divide);
        }

        private MMQuestion GenPow(string diff)
        {
            if (diff == "hard")
            {
                int b = _rng.NextDouble() < 0.3 ? 3 : 2; // kwadraat of kubus
                int a = b == 2 ? Rand(6, 20) : Rand(2, 8);
                int ans = (int)Math.Pow(a, b);
                var opts = UniqueOptions(ans.ToString(),
                    ((int)Math.Pow(a + 1, b)).ToString(),
                    ((int)Math.Pow(Math.Max(1, a - 1), b)).ToString(),
                    (ans + Rand(-50, 50)).ToString());
                return new MMQuestion($"{a}^{b} = ?", ans.ToString(), opts, $"{a}^{b} = {ans}.", MentalType.Power);
            }
            else
            {
                int a = Rand(2, diff == "easy" ? 12 : 16);
                int ans = a * a;
                var opts = UniqueOptions(ans.ToString(),
                    ((a + 1) * (a + 1)).ToString(),
                    ((a - 1) * (a - 1)).ToString(),
                    (ans + Rand(-30, 30)).ToString());
                return new MMQuestion($"{a}² = ?", ans.ToString(), opts, $"{a}² = {a}×{a} = {ans}.", MentalType.Power);
            }
        }

        private MMQuestion GenRoot(string diff)
        {
            if (diff == "hard" && _rng.NextDouble() < 0.25)
            {
                int a = Rand(2, 9);
                int n = a * a * a;
                var opts = UniqueOptions(a.ToString(),
                    (a + 1).ToString(),
                    Math.Max(1, a - 1).ToString(),
                    (a + Rand(-3, 3)).ToString());
                return new MMQuestion($"∛{n} = ?", a.ToString(), opts, $"∛{n} = {a}.", MentalType.Root);
            }
            else
            {
                int a = diff == "easy" ? Rand(2, 12) : Rand(6, 20);
                int n = a * a;
                var opts = UniqueOptions(a.ToString(),
                    (a + 1).ToString(),
                    Math.Max(1, a - 1).ToString(),
                    (a + Rand(-3, 3)).ToString());
                return new MMQuestion($"√{n} = ?", a.ToString(), opts, $"√{n} = {a}.", MentalType.Root);
            }
        }

        /* ---- helpers ---- */

        private int Rand(int a, int b) => _rng.Next(a, b + 1);

        private T RandomEnum<T>() where T : Enum
        {
            var values = Enum.GetValues(typeof(T));
            return (T)values.GetValue(_rng.Next(values.Length));
        }

        // altijd 4 unieke opties + shuffle
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
                if (!int.TryParse(baseStr, out int v)) v = int.TryParse(seeds.FirstOrDefault(), out var vv) ? vv : 0;
                int cand;
                do { cand = v + Rand(-15, 15); } while (cand == v || seen.Contains(Norm(cand.ToString())));
                TryAdd(cand.ToString());
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
