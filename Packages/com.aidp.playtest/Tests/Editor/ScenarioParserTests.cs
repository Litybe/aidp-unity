using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class ScenarioParserTests
    {
        private const string Full = @"{
  ""id"": ""issue-214-ca2"", ""scene"": ""Assets/Scenes/Level_1.unity"", ""timeoutMs"": 10000,
  ""setup"": [ { ""cmd"": ""teleport"", ""x"": 12, ""y"": 3 }, { ""cmd"": ""give"", ""ability"": ""dash"" } ],
  ""steps"": [ { ""hold"": ""rightArrow"", ""ms"": 300 }, { ""press"": ""x"" }, { ""wait"": 400 }, { ""capture"": ""after-dash"" } ],
  ""assert"": [ ""player.x > 17.5"", { ""expr"": ""player.grounded == true"", ""withinMs"": 500 }, ""player.hp == 3"" ] }";

        [Test]
        public void ParsesContractExample()
        {
            Scenario s = ScenarioParser.Parse(Full);
            Assert.AreEqual("issue-214-ca2", s.Id);
            Assert.AreEqual("Assets/Scenes/Level_1.unity", s.Scene);
            Assert.AreEqual(10000, s.TimeoutMs);
            Assert.AreEqual(2, s.Setup.Count);
            Assert.AreEqual("teleport", s.Setup[0].Name);
            Assert.AreEqual(12f, s.Setup[0].Args.RequireFloat("x"));
            Assert.AreEqual("dash", s.Setup[1].Args.GetString("ability"));
            Assert.AreEqual(4, s.Steps.Count);
            Assert.AreEqual(StepKind.Hold, s.Steps[0].Kind);
            Assert.AreEqual(300, s.Steps[0].Ms);
            Assert.AreEqual(StepKind.Press, s.Steps[1].Kind);
            Assert.AreEqual(StepKind.Wait, s.Steps[2].Kind);
            Assert.AreEqual("after-dash", s.Steps[3].Name);
            Assert.AreEqual(3, s.Assertions.Count);
            Assert.AreEqual(500, s.Assertions[1].WithinMs);
            Assert.AreEqual(AssertionOperator.Greater, s.Assertions[0].Expression.Operator);
        }

        [Test]
        public void DefaultsIdAndTimeout()
        {
            Scenario s = ScenarioParser.Parse("{\"steps\":[]}", "req-1");
            Assert.AreEqual("req-1", s.Id);
            Assert.AreEqual(PlaytestConstants.DefaultTimeoutMs, s.TimeoutMs);
        }

        [TestCase("{")]
        [TestCase("{\"steps\":[]}")]
        [TestCase("{\"id\":\"a b\"}")]
        [TestCase("{\"id\":\"a\",\"steps\":[{\"hold\":\"space\"}]}")]
        [TestCase("{\"id\":\"a\",\"steps\":[{\"press\":\"notAKey\"}]}")]
        [TestCase("{\"id\":\"a\",\"steps\":[{\"press\":\"x\",\"tap\":\"y\"}]}")]
        [TestCase("{\"id\":\"a\",\"steps\":[{\"foo\":1}]}")]
        [TestCase("{\"id\":\"a\",\"assert\":[\"nonsense\"]}")]
        [TestCase("{\"id\":\"a\",\"setup\":[{\"x\":1}]}")]
        public void RejectsMalformed(string json)
        {
            Assert.Throws<ScenarioParseException>(() => ScenarioParser.Parse(json));
        }

        [Test]
        public void HoldAcceptsSeveralKeysAndGamepad()
        {
            Scenario s = ScenarioParser.Parse("{\"id\":\"a\",\"steps\":[{\"hold\":[\"rightArrow\",\"space\"],\"ms\":50},{\"tap\":\"gamepad.buttonSouth\"}]}");
            Assert.AreEqual(2, s.Steps[0].Keys.Count);
            Assert.AreEqual("gamepad.buttonSouth", s.Steps[1].Keys[0]);
        }

        [Test]
        public void StepCommandKeepsArguments()
        {
            Scenario s = ScenarioParser.Parse("{\"id\":\"a\",\"steps\":[{\"cmd\":\"timeScale\",\"value\":2}]}");
            Assert.AreEqual(StepKind.Command, s.Steps[0].Kind);
            Assert.AreEqual(2f, s.Steps[0].Command.Args.GetFloat("value"));
        }
    }
}
