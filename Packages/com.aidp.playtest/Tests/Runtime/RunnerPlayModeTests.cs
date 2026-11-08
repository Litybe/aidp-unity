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
            var runner = new PlaytestRunner(PlaytestRegistry.FromLoadedAssemblies(), new PlaytestOptions { ProjectRoot = PlaytestPaths.ProjectRoot, DefaultScene = string.Empty });
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

        [UnityTest]
        public IEnumerator SpawnPlacesAPrefabReadsItAndCleansUp()
        {
#if UNITY_EDITOR
            const string dir = "Assets/_AidpPlaytestTmp";
            const string prefabPath = dir + "/Fixture.prefab";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "_AidpPlaytestTmp");
            var template = new UnityEngine.GameObject("Fixture");
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
            UnityEngine.Object.DestroyImmediate(template);
            try
            {
                var result = new PlaytestResult("pm-spawn");
                const string json = "{\"id\":\"pm-spawn\",\"setup\":[{\"cmd\":\"spawn\",\"prefab\":\"" + prefabPath + "\",\"x\":12,\"y\":3,\"name\":\"fixture1\"},"
                    + "{\"cmd\":\"spawn\",\"prefab\":\"" + prefabPath + "\",\"name\":\"hidden1\",\"active\":false}],"
                    + "\"assert\":[\"object.fixture1.exists == true\",\"object.fixture1.x ~= 12 ± 0.01\",\"object.fixture1.y == 3\","
                    + "\"object.hidden1.active == false\",\"object.nobody.exists == false\"]}";
                yield return Play(json, result);
                Assert.IsNull(result.Error);
                Assert.IsTrue(result.Passed, result.Summary());
                Assert.AreEqual(0, SpawnedObjects.Count);
                yield return null;
                Assert.IsNull(UnityEngine.GameObject.Find("fixture1"));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(dir);
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SpawnOfAMissingPrefabIsAnError()
        {
            var result = new PlaytestResult("pm-spawn-ko");
            yield return Play("{\"id\":\"pm-spawn-ko\",\"setup\":[{\"cmd\":\"spawn\",\"prefab\":\"Assets/Nope.prefab\"}]}", result);
            Assert.IsFalse(result.Passed);
            StringAssert.Contains("no prefab", result.Error);
        }
    }
}
