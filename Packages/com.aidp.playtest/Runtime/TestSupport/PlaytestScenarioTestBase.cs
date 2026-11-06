using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Aidp.Playtest.TestSupport
{
    /// <summary>
    /// Base des tests PlayMode de non-regression : un test par fichier de scenario. Dans le jeu :
    /// <c>public class ScenarioPlayModeTests : PlaytestScenarioTestBase { }</c> dans un asmdef de tests PlayMode.
    /// </summary>
    public abstract class PlaytestScenarioTestBase
    {
        [UnityTest]
        public IEnumerator RunScenario([ValueSource(typeof(PlaytestScenarioSource), nameof(PlaytestScenarioSource.Paths))] string path)
        {
            if (path == PlaytestScenarioSource.NoScenario)
                Assert.Ignore("no scenario file under " + PlaytestScenarioSource.Directory);

            string root = PlaytestPaths.ProjectRoot;
            string json = AidpFileStore.ReadText(System.IO.Path.Combine(root, path));
            Scenario scenario = ScenarioParser.Parse(json, System.IO.Path.GetFileNameWithoutExtension(path));

            var options = new PlaytestOptions { ProjectRoot = root };
            var runner = new PlaytestRunner(PlaytestRegistry.FromLoadedAssemblies(), options);
            var result = new PlaytestResult(scenario.Id);
            yield return runner.Run(scenario, result);

            Assert.That(result.Error, Is.Null, "scenario " + scenario.Id + " failed: " + result.Error);
            Assert.That(result.Passed, Is.True, "scenario " + scenario.Id + ":\n" + result.Summary());
        }
    }
}
