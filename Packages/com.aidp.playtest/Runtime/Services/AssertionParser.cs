using System.Globalization;
using System.Text.RegularExpressions;

namespace Aidp.Playtest
{
    /// <summary>Analyse "sonde.champ op valeur" ; op parmi == != &lt; &lt;= &gt; &gt;= ~= (avec "± tolerance").</summary>
    public static class AssertionParser
    {
        private static readonly Regex Pattern = new Regex(
            @"^\s*(?<probe>[A-Za-z_][A-Za-z0-9_]*)\.(?<field>[A-Za-z0-9_.\-]+?)\s*(?<op>==|!=|<=|>=|~=|<|>)\s*(?<value>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ToleranceSplit = new Regex(@"\s*(?:±|\+/-|\+-)\s*", RegexOptions.Compiled);

        public static AssertionExpression Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ScenarioParseException("empty assertion");
            Match m = Pattern.Match(text);
            if (!m.Success)
                throw new ScenarioParseException("invalid assertion '" + text + "' (expected: probe.field op value)");

            AssertionOperator op = ToOperator(m.Groups["op"].Value);
            string valueText = m.Groups["value"].Value;
            double tolerance = PlaytestConstants.DefaultTolerance;

            if (op == AssertionOperator.Approximately)
            {
                string[] parts = ToleranceSplit.Split(valueText);
                if (parts.Length == 2)
                {
                    if (!TryNumber(parts[1], out tolerance) || tolerance < 0)
                        throw new ScenarioParseException("invalid tolerance in '" + text + "'");
                    valueText = parts[0];
                }
                else if (parts.Length > 2)
                {
                    throw new ScenarioParseException("invalid tolerance in '" + text + "'");
                }

                if (!TryNumber(valueText, out double target))
                    throw new ScenarioParseException("'~=' needs a number in '" + text + "'");
                return new AssertionExpression(text, m.Groups["probe"].Value, m.Groups["field"].Value, op, target, tolerance);
            }

            object expected = ParseLiteral(valueText);
            if (expected is string && op != AssertionOperator.Equal && op != AssertionOperator.NotEqual)
                throw new ScenarioParseException("operator '" + m.Groups["op"].Value + "' needs a number in '" + text + "'");
            if (expected is bool && op != AssertionOperator.Equal && op != AssertionOperator.NotEqual)
                throw new ScenarioParseException("operator '" + m.Groups["op"].Value + "' needs a number in '" + text + "'");
            return new AssertionExpression(text, m.Groups["probe"].Value, m.Groups["field"].Value, op, expected, tolerance);
        }

        private static AssertionOperator ToOperator(string op)
        {
            switch (op)
            {
                case "==": return AssertionOperator.Equal;
                case "!=": return AssertionOperator.NotEqual;
                case "<": return AssertionOperator.Less;
                case "<=": return AssertionOperator.LessOrEqual;
                case ">": return AssertionOperator.Greater;
                case ">=": return AssertionOperator.GreaterOrEqual;
                default: return AssertionOperator.Approximately;
            }
        }

        private static object ParseLiteral(string text)
        {
            if (text.Length >= 2 && (text[0] == '"' || text[0] == '\'') && text[text.Length - 1] == text[0])
                return text.Substring(1, text.Length - 2);
            if (string.Equals(text, "true", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(text, "false", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (TryNumber(text, out double number))
                return number;
            return text;
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
