using System.Collections.Generic;
using Aidp.Playtest.Editor;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class EditorGuardPlanTests
    {
        private const string Arena = "Assets/Tests/Playtest/Scenes/PlaytestArena.unity";
        private const string Level = "Assets/Scenes/Level_1.unity";

        private static EditorGuardPlan.Input Input(string active, bool dirty, DirtyScenePolicy policy, string editScene = Arena, bool exists = true, int count = 1)
        {
            return new EditorGuardPlan.Input
            {
                ActiveScenePath = active,
                LoadedSceneCount = count,
                AnyDirty = dirty,
                EditScene = editScene,
                EditSceneExists = exists,
                Policy = policy
            };
        }

        [Test]
        public void CleanEditorOnTheArenaOnlyRefreshes()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Arena, false, DirtyScenePolicy.Discard));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void LevelOpenIsReplacedByTheArena()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Level, false, DirtyScenePolicy.Discard));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.OpenEditScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void DirtyArenaIsReopenedFromDisk()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Arena, true, DirtyScenePolicy.Discard));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.OpenEditScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void SavePolicySavesThenOpensTheArena()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Level, true, DirtyScenePolicy.Save));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.SaveOpenScenes, EditorGuardStep.OpenEditScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void SavePolicyOnTheArenaOnlySaves()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Arena, true, DirtyScenePolicy.Save));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.SaveOpenScenes, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void SeveralScenesLoadedGoBackToTheArenaAlone()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Arena, false, DirtyScenePolicy.Discard, count: 2));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.OpenEditScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void WithoutEditSceneADirtyLevelIsReopened()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Level, true, DirtyScenePolicy.Discard, editScene: string.Empty, exists: false));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.ReopenActiveScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void MissingArenaFileBehavesAsNoEditScene()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Level, false, DirtyScenePolicy.Discard, exists: false));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void DirtyUntitledSceneWithoutEditSceneBecomesEmpty()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(string.Empty, true, DirtyScenePolicy.Discard, editScene: string.Empty, exists: false));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.NewEmptyScene, EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void IgnorePolicyNeverTouchesScenes()
        {
            List<EditorGuardStep> steps = EditorGuardPlan.Decide(Input(Level, true, DirtyScenePolicy.Ignore));
            CollectionAssert.AreEqual(new[] { EditorGuardStep.RefreshAssets }, steps);
        }

        [Test]
        public void RefreshCanBeLeftToTheCaller()
        {
            EditorGuardPlan.Input input = Input(Level, false, DirtyScenePolicy.Discard);
            input.Refresh = false;
            CollectionAssert.AreEqual(new[] { EditorGuardStep.OpenEditScene }, EditorGuardPlan.Decide(input));
        }
    }
}
