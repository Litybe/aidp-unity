using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aidp.Playtest
{
    public sealed class AssertResult
    {
        public AssertResult(string expr, bool passed, object actual, string message)
        {
            Expr = expr;
            Passed = passed;
            Actual = actual;
            Message = message;
        }

        public string Expr { get; }
        public bool Passed { get; }
        public object Actual { get; }
        public string Message { get; }
    }

    /// <summary>Resultat d'un scenario (schema C2) : serialise dans .aidp/playtest/results/id.json.</summary>
    public sealed class PlaytestResult
    {
        public PlaytestResult(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public bool Passed { get; private set; }
        public long DurationMs { get; set; }
        public List<AssertResult> Asserts { get; } = new List<AssertResult>();
        public List<string> Captures { get; } = new List<string>();
        public string Gif { get; set; }
        public List<string> Log { get; } = new List<string>();
        public string Error { get; set; }

        public static PlaytestResult Failure(string id, string error)
        {
            var result = new PlaytestResult(id) { Error = error };
            result.Complete();
            return result;
        }

        /// <summary>Calcule "passed" : aucune erreur et toutes les assertions vraies.</summary>
        public void Complete()
        {
            bool allPassed = true;
            foreach (AssertResult a in Asserts)
                allPassed &= a.Passed;
            Passed = Error == null && allPassed;
        }

        public string Summary()
        {
            if (Error != null)
                return "error: " + Error;
            var lines = new List<string>();
            foreach (AssertResult a in Asserts)
                lines.Add((a.Passed ? "PASS " : "FAIL ") + a.Message);
            return string.Join("\n", lines);
        }

        public JObject ToJson()
        {
            var asserts = new JArray();
            foreach (AssertResult a in Asserts)
            {
                asserts.Add(new JObject
                {
                    ["expr"] = a.Expr,
                    ["passed"] = a.Passed,
                    ["actual"] = a.Actual == null ? JValue.CreateNull() : JToken.FromObject(a.Actual),
                    ["message"] = a.Message
                });
            }

            return new JObject
            {
                ["id"] = Id,
                ["passed"] = Passed,
                ["durationMs"] = DurationMs,
                ["asserts"] = asserts,
                ["captures"] = new JArray(Captures),
                ["gif"] = Gif == null ? JValue.CreateNull() : (JToken)Gif,
                ["log"] = new JArray(Log),
                ["error"] = Error == null ? JValue.CreateNull() : (JToken)Error
            };
        }

        public string ToJsonString() => ToJson().ToString(Formatting.Indented);
    }
}
