using Aidp.Playtest.Editor;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class TestEditorSceneTests
    {
        private const string SceneFile = "Assets/Scenes/SceneGame/Game/Level_test.unity";

        [TestCase(true, false, 1, SceneFile, true, TestName = "test editor on a scene file leaves it")]
        [TestCase(true, false, 2, "", true, TestName = "test editor with several scenes leaves them")]
        [TestCase(true, false, 1, "", false, TestName = "test editor already on an untitled scene stays")]
        [TestCase(false, false, 1, SceneFile, false, TestName = "dev editor keeps its scene")]
        [TestCase(true, true, 1, SceneFile, false, TestName = "play mode is left alone")]
        public void ShouldLeave(bool marked, bool playing, int sceneCount, string activePath, bool expected)
        {
            Assert.AreEqual(expected, TestEditorScene.ShouldLeave(marked, playing, sceneCount, activePath));
        }
    }
}
