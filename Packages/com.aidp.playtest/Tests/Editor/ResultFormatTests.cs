using System;
using System.Collections.Generic;
using Aidp.Playtest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class ResultFormatTests
    {
        [Test]
        public void PlaytestResultMatchesContract()
        {
            var r = new PlaytestResult("t1") { DurationMs = 4200 };
            r.Asserts.Add(new AssertResult("player.x > 17.5", false, 16.9, "msg"));
            r.Asserts.Add(new AssertResult("player.hp == 3", true, 3.0, "ok"));
            r.Captures.Add(".aidp/playtest/results/t1/after-dash.png");
            r.Log.Add("line");
            r.Complete();
            JObject j = r.ToJson();
            Assert.AreEqual("t1", (string)j["id"]);
            Assert.AreEqual(false, (bool)j["passed"]);
            Assert.AreEqual(4200, (long)j["durationMs"]);
            Assert.AreEqual("player.x > 17.5", (string)j["asserts"][0]["expr"]);
            Assert.AreEqual(16.9, (double)j["asserts"][0]["actual"]);
            Assert.AreEqual(JTokenType.Null, j["gif"].Type);
            Assert.AreEqual(JTokenType.Null, j["error"].Type);
            Assert.AreEqual(1, ((JArray)j["captures"]).Count);
            CollectionAssert.AreEqual(new[] { "id", "passed", "durationMs", "asserts", "captures", "gif", "log", "error" }, KeysOf(j));
        }

        [Test]
        public void ErrorMakesResultFail()
        {
            PlaytestResult r = PlaytestResult.Failure("t2", "boom");
            Assert.IsFalse(r.Passed);
            Assert.AreEqual("boom", (string)r.ToJson()["error"]);
        }

        [Test]
        public void CompileResultMatchesContract()
        {
            CompileIssue issue = CompileResultBuilder.ToIssue("C:\\proj\\Assets\\A.cs", 12, 5,
                "Assets/A.cs(12,5): error CS0103: The name 'x' does not exist", "C:/proj");
            JObject j = CompileResultBuilder.Build("c1", new List<CompileIssue> { issue }, 3, 8400, "6000.6.7f1", new DateTime(2026, 11, 6, 16, 0, 0, DateTimeKind.Utc));
            Assert.AreEqual(false, (bool)j["ok"]);
            Assert.AreEqual("Assets/A.cs", (string)j["errors"][0]["file"]);
            Assert.AreEqual("CS0103: The name 'x' does not exist", (string)j["errors"][0]["message"]);
            Assert.AreEqual(12, (int)j["errors"][0]["line"]);
            Assert.AreEqual(3, (int)j["warnings"]);
            Assert.AreEqual("2026-11-06T16:00:00Z", (string)j["finishedAt"]);
            CollectionAssert.AreEqual(new[] { "id", "ok", "errors", "warnings", "durationMs", "unityVersion", "finishedAt" }, KeysOf(j));
            Assert.AreEqual(true, (bool)CompileResultBuilder.Build("c2", new List<CompileIssue>(), 0, 1, "v", DateTime.UtcNow)["ok"]);
        }

        [Test]
        public void TestGateResultMatchesContract()
        {
            var failures = new List<TestFailure> { new TestFailure { Name = "Ns.C.T", Message = "m", Stack = "s" } };
            JObject j = TestResultBuilder.Build("r1", "EditMode", 42, 41, 1, 0, 9000, failures, new DateTime(2026, 11, 6, 16, 0, 0, DateTimeKind.Utc), null);
            Assert.AreEqual(42, (int)j["total"]);
            Assert.AreEqual("Ns.C.T", (string)j["failures"][0]["name"]);
            Assert.IsNull(j["note"]);
            CollectionAssert.AreEqual(new[] { "id", "mode", "total", "passed", "failed", "skipped", "durationMs", "failures", "finishedAt" }, KeysOf(j));
            JObject none = TestResultBuilder.Build("r2", "PlayMode", 0, 0, 0, 0, 0, new List<TestFailure>(), DateTime.UtcNow, "no-tests");
            Assert.AreEqual("no-tests", (string)none["note"]);
        }

        [Test]
        public void TestGateModesAndPaths()
        {
            Assert.IsTrue(TestGate.TryParseMode("editmode", out string m));
            Assert.AreEqual("EditMode", m);
            Assert.IsFalse(TestGate.TryParseMode("x", out _));
            Assert.AreEqual("r-EditMode.json", System.IO.Path.GetFileName(PlaytestPaths.TestResultFile("p", "r", "EditMode")));
        }

        [Test]
        public void ReproRecorderBuildsScenario()
        {
            var events = new List<(double time, string control, bool down)> { (1.0, "space", true), (1.3, "space", false), (1.5, "x", true) };
            Scenario s = ScenarioParser.Parse(ReproRecorder.ToScenarioJson("repro-1", "Assets/S.unity", events));
            Assert.AreEqual("repro-1", s.Id);
            Assert.AreEqual(StepKind.Press, s.Steps[0].Kind);
            Assert.AreEqual(300, s.Steps[1].Ms);
            Assert.AreEqual(StepKind.Release, s.Steps[s.Steps.Count - 1].Kind);
        }

        private static List<string> KeysOf(JObject j)
        {
            var keys = new List<string>();
            foreach (JProperty p in j.Properties())
                keys.Add(p.Name);
            return keys;
        }
    }
}
