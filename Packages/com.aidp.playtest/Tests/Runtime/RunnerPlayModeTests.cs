using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Aidp.Playtest.Tests
{
    [PlaytestProbe("pt")]
    public sealed class PtProbe : IPlaytestProbe
    {
        public static float Value;
        public static bool SpaceDown;

        public IReadOnlyDictionary<string, object> Read() => new Dictionary<string, object> { ["v"] = Value, ["space"] = SpaceDown };
    }

    [PlaytestCommand("ptset")]
    public sealed class PtSetCommand : IPlaytestCommand
    {
        public void Execute(PlaytestArgs args) => PtProbe.Value = args.RequireFloat("v");
    }

    public sealed class RunnerPlayModeTests
    {
        private InputAction _space;

        [SetUp]
        public void SetUp()
        {
            PtProbe.Value = 0;
            PtProbe.SpaceDown = false;
            _space = new InputAction("space", InputActionType.Button, "<Keyboard>/space");
            _space.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            _space.Disable();
            _space.Dispose();
        }

        private static IEnumerator Play(string json, PlaytestResult result)
        {
            var runner = new PlaytestRunner(PlaytestRegistry.FromLoadedAssemblies(), new PlaytestOptions { ProjectRoot = PlaytestPaths.ProjectRoot });
            yield return runner.Run(ScenarioParser.Parse(json), result);
        }

        [UnityTest]
        public IEnumerator VirtualKeyboardIsSeenByAnInputAction()
        {
            using (var input = new VirtualInput())
            {
                Assert.IsFalse(_space.IsPressed());
                input.Press("space");
                yield return null;
                yield return null;
                Assert.IsTrue(_space.IsPressed());
                input.Release("space");
                yield return null;
                yield return null;
                Assert.IsFalse(_space.IsPressed());
                Assert.AreEqual(PlaytestConstants.VirtualKeyboardName, input.Keyboard.name);
            }
        }

        [UnityTest]
        public IEnumerator SimpleScenarioInAnEmptyScene()
        {
            _space.performed += _ => PtProbe.SpaceDown = true;
            var result = new PlaytestResult("pm-ok");
            const string json = "{\"id\":\"pm-ok\",\"timeoutMs\":8000,\"setup\":[{\"cmd\":\"ptset\",\"v\":5}],"
                + "\"steps\":[{\"hold\":\"space\",\"ms\":150},{\"wait\":50}],"
                + "\"assert\":[\"pt.v == 5\",{\"expr\":\"pt.space == true\",\"withinMs\":300},\"pt.v ~= 5.0 ± 0.1\"]}";
            yield return Play(json, result);
            Assert.IsNull(result.Error);
            Assert.IsTrue(result.Passed, result.Summary());
            Assert.AreEqual(3, result.Asserts.Count);
            Assert.GreaterOrEqual(result.DurationMs, 150);
        }

        [UnityTest]
        public IEnumerator FailingAssertionReportsActual()
        {
            var result = new PlaytestResult("pm-ko");
            yield return Play("{\"id\":\"pm-ko\",\"setup\":[{\"cmd\":\"ptset\",\"v\":2}],\"assert\":[\"pt.v > 10\"]}", result);
            Assert.IsFalse(result.Passed);
            Assert.AreEqual(2.0, (double)result.Asserts[0].Actual);
        }

        [UnityTest]
        public IEnumerator UnknownCommandIsAnError()
        {
            var result = new PlaytestResult("pm-cmd");
            yield return Play("{\"id\":\"pm-cmd\",\"setup\":[{\"cmd\":\"nope\"}]}", result);
            Assert.IsFalse(result.Passed);
            StringAssert.Contains("unknown command", result.Error);
        }

        [UnityTest]
        public IEnumerator TimeoutStopsTheScenario()
        {
            var result = new PlaytestResult("pm-to");
            yield return Play("{\"id\":\"pm-to\",\"timeoutMs\":200,\"steps\":[{\"wait\":5000}]}", result);
            Assert.IsFalse(result.Passed);
            StringAssert.Contains("timeout", result.Error);
        }
    }
}
