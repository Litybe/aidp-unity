namespace Aidp.Playtest
{
    public enum AssertionOperator
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Approximately
    }

    /// <summary>Assertion analysee : "sonde.champ op valeur". <see cref="Expected"/> vaut double, bool ou string.</summary>
    public sealed class AssertionExpression
    {
        public AssertionExpression(string text, string probe, string field, AssertionOperator op, object expected, double tolerance)
        {
            Text = text;
            Probe = probe;
            Field = field;
            Operator = op;
            Expected = expected;
            Tolerance = tolerance;
        }

        public string Text { get; }
        public string Probe { get; }
        public string Field { get; }
        public AssertionOperator Operator { get; }
        public object Expected { get; }
        public double Tolerance { get; }
    }

    public sealed class AssertionOutcome
    {
        public AssertionOutcome(bool passed, object actual, string message)
        {
            Passed = passed;
            Actual = actual;
            Message = message;
        }

        public bool Passed { get; }
        public object Actual { get; }
        public string Message { get; }
    }
}
