using System.Collections.Generic;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class AssertionTests
    {
        private sealed class FakeSource : IProbeSource
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();

            public bool TryRead(string probe, string field, out object value, out string error)
            {
                error = null;
                if (Values.TryGetValue(probe + "." + field, out value))
                    return true;
                error = "unknown";
                return false;
            }
        }

        private static AssertionOutcome Eval(string expr, object actual)
        {
            var src = new FakeSource();
            src.Values["player.x"] = actual;
            return AssertionEvaluator.Evaluate(AssertionParser.Parse(expr), src);
        }

        [TestCase("player.x > 17.5", 18f, true)]
        [TestCase("player.x > 17.5", 16.9f, false)]
        [TestCase("player.x >= 3", 3, true)]
        [TestCase("player.x < 3", 3, false)]
        [TestCase("player.x <= 3", 3, true)]
        [TestCase("player.x == 3", 3L, true)]
        [TestCase("player.x != 3", 3.0, false)]
        [TestCase("player.x~=17.5 ± 0.2", 17.6f, true)]
        [TestCase("player.x ~= 17.5 +- 0.2", 17.8f, false)]
        [TestCase("player.x ~= 17.5", 17.5004, true)]
        public void Numbers(string expr, object actual, bool expected)
        {
            Assert.AreEqual(expected, Eval(expr, actual).Passed);
        }

        [Test]
        public void BoolsAndStrings()
        {
            Assert.IsTrue(Eval("player.x == true", true).Passed);
            Assert.IsFalse(Eval("player.x == false", true).Passed);
            Assert.IsTrue(Eval("player.x == \"Dash\"", "Dash").Passed);
            Assert.IsTrue(Eval("player.x != \"Dash\"", "Wall").Passed);
        }

        [Test]
        public void TypeMismatchFailsWithMessage()
        {
            AssertionOutcome o = Eval("player.x > 3", "text");
            Assert.IsFalse(o.Passed);
            StringAssert.Contains("type mismatch", o.Message);
        }

        [Test]
        public void MissingProbeFails()
        {
            AssertionOutcome o = AssertionEvaluator.Evaluate(AssertionParser.Parse("nope.x == 1"), new FakeSource());
            Assert.IsFalse(o.Passed);
            Assert.IsNull(o.Actual);
        }

        [Test]
        public void FloatActualIsReadable()
        {
            Assert.AreEqual(16.9, (double)Eval("player.x > 17.5", 16.9f).Actual);
        }

        [TestCase("")]
        [TestCase("player > 1")]
        [TestCase("player.x ~= abc")]
        [TestCase("player.x > true")]
        [TestCase("player.x < \"a\"")]
        public void ParserRejects(string expr)
        {
            Assert.Throws<ScenarioParseException>(() => AssertionParser.Parse(expr));
        }

        [Test]
        public void ParserExtractsParts()
        {
            AssertionExpression e = AssertionParser.Parse("player.ability.dash == true");
            Assert.AreEqual("player", e.Probe);
            Assert.AreEqual("ability.dash", e.Field);
            Assert.AreEqual(true, e.Expected);
        }
    }
}
