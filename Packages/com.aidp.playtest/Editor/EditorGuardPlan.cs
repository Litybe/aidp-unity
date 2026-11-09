using System.Collections.Generic;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Decide, sans toucher a Unity, ce que la garde doit faire de l'editeur avant une porte. Testable en EditMode ;
    /// <see cref="EditorGuard"/> applique les etapes.
    /// </summary>
    public static class EditorGuardPlan
    {
        /// <summary>Etat de l'editeur dont la decision depend.</summary>
        public sealed class Input
        {
            public string ActiveScenePath { get; set; } = string.Empty;
            public int LoadedSceneCount { get; set; } = 1;
            public bool AnyDirty { get; set; }
            public string EditScene { get; set; } = string.Empty;
            public bool EditSceneExists { get; set; }
            public DirtyScenePolicy Policy { get; set; } = DirtyScenePolicy.Discard;
            public bool Refresh { get; set; } = true;
        }

        public static List<EditorGuardStep> Decide(Input input)
        {
            var steps = new List<EditorGuardStep>();
            if (input.Policy == DirtyScenePolicy.Ignore)
            {
                if (input.Refresh)
                    steps.Add(EditorGuardStep.RefreshAssets);
                return steps;
            }

            bool dirtyHandled = !input.AnyDirty;

            if (input.AnyDirty && input.Policy == DirtyScenePolicy.Save)
            {
                steps.Add(EditorGuardStep.SaveOpenScenes);
                dirtyHandled = true;
            }

            bool hasEditScene = !string.IsNullOrEmpty(input.EditScene) && input.EditSceneExists;
            bool onEditScene = hasEditScene && input.ActiveScenePath == input.EditScene && input.LoadedSceneCount == 1;
            if (hasEditScene && (!onEditScene || !dirtyHandled))
            {
                steps.Add(EditorGuardStep.OpenEditScene);
                dirtyHandled = true;
            }

            if (!dirtyHandled)
                steps.Add(string.IsNullOrEmpty(input.ActiveScenePath) ? EditorGuardStep.NewEmptyScene : EditorGuardStep.ReopenActiveScene);

            if (input.Refresh)
                steps.Add(EditorGuardStep.RefreshAssets);
            return steps;
        }
    }
}
