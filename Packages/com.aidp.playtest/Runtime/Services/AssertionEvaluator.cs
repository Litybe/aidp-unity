using System;
using System.Globalization;

namespace Aidp.Playtest
{
    /// <summary>Source de valeurs pour les assertions (les sondes du jeu, ou un faux dans les tests).</summary>
    public interface IProbeSource
    {
        bool TryRead(string probe, string field, out object value, out string error);
    }

    /// <summary>Evalue une assertion sur une valeur de sonde.</summary>
    public static class AssertionEvaluator
    {
        public static AssertionOutcome Evaluate(AssertionExpression expr, IProbeSource source)
        {
            if (!source.TryRead(expr.Probe, expr.Field, out object raw, out string error))
                return new AssertionOutcome(false, null, expr.Text + " : " + error);

            object actual = Normalize(raw);
            if (actual == null)
                return new AssertionOutcome(false, null, expr.Text + " : no value for " + expr.Probe + "." + expr.Field);

            bool passed;
            string problem = null;
            switch (expr.Expected)
            {
                case double expectedNumber when actual is double actualNumber:
                    passed = CompareNumbers(expr, actualNumber, expectedNumber);
                    break;
                case bool expectedBool when actual is bool actualBool:
                    passed = CompareEquality(expr.Operator, actualBool == expectedBool, expr.Text, out problem);
                    break;
                case string expectedText when actual is string actualText:
                    passed = CompareEquality(expr.Operator, string.Equals(actualText, expectedText, StringComparison.Ordinal), expr.Text, out problem);
                    break;
                default:
                    passed = false;
                    problem = "type mismatch (actual " + actual.GetType().Name + ", expected " + expr.Expected.GetType().Name + ")";
                    break;
            }

            string message = passed
                ? expr.Text + " (actual " + Format(actual) + ")"
                : expr.Text + " : actual " + Format(actual) + (problem != null ? " - " + problem : string.Empty);
            return new AssertionOutcome(passed, actual, message);
        }

        private static bool CompareNumbers(AssertionExpression expr, double actual, double expected)
        {
            switch (expr.Operator)
            {
                case AssertionOperator.Equal: return actual == expected;
                case AssertionOperator.NotEqual: return actual != expected;
                case AssertionOperator.Less: return actual < expected;
                case AssertionOperator.LessOrEqual: return actual <= expected;
                case AssertionOperator.Greater: return actual > expected;
                case AssertionOperator.GreaterOrEqual: return actual >= expected;
                default: return Math.Abs(actual - expected) <= expr.Tolerance;
            }
        }

        private static bool CompareEquality(AssertionOperator op, bool equal, string text, out string problem)
        {
            problem = null;
            if (op == AssertionOperator.Equal)
                return equal;
            if (op == AssertionOperator.NotEqual)
                return !equal;
            problem = "operator not valid for this type";
            return false;
        }

        /// <summary>Nombres en double (floats arrondis a la valeur decimale lisible), enums en nom, le reste tel quel.</summary>
        public static object Normalize(object raw)
        {
            switch (raw)
            {
                case null: return null;
                case bool _: return raw;
                case string _: return raw;
                case float f: return double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                case double _: return raw;
                case Enum e: return e.ToString();
                case IConvertible c when IsNumeric(raw): return c.ToDouble(CultureInfo.InvariantCulture);
                default: return Convert.ToString(raw, CultureInfo.InvariantCulture);
            }
        }

        private static bool IsNumeric(object o)
        {
            return o is int || o is long || o is short || o is byte || o is sbyte || o is uint || o is ulong || o is ushort || o is decimal;
        }

        private static string Format(object v)
        {
            return v is double d ? d.ToString("R", CultureInfo.InvariantCulture) : v.ToString();
        }
    }
}
