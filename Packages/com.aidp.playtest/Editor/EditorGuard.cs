using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Met l'editeur dans un etat neutre avant une porte (compile, tests, scenario) : scenes sales traitees selon
    /// <see cref="AidpEditorSettings.DirtyScenes"/>, scene d'edition ouverte (<see cref="AidpEditorSettings.EditScene"/>), assets
    /// rafraichis. Tout passe par des appels scriptes qui n'ouvrent jamais de boite de dialogue : c'est ce qui evite les
    /// « recharger la scene ? » et « sauvegarder ? » qui bloquaient le thread principal et donc le pont fichier.
    /// Menu <c>Tools/AIDP/Playtest/Prepare editor now</c> pour l'appliquer a la main.
    /// </summary>
    public static class EditorGuard
    {
        private const string ReportKey = "Aidp.EditorGuard.LastReport";
        private const string SkippedPlayMode = "play mode";
        private const string SkippedBusy = "compiling or importing";
        private const string MenuItem = "Tools/AIDP/Playtest/Prepare editor now";

        /// <summary>Applique la garde. Ne fait rien en Play Mode ou pendant une compilation / un import (le rapport le dit).</summary>
        public static EditorGuardReport Prepare(bool refresh = true)
        {
            var report = new EditorGuardReport { At = Now() };
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Skip(report, SkippedPlayMode);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return Skip(report, SkippedBusy);

            AidpEditorSettings settings = AidpEditorSettings.instance;
            string root = PlaytestPaths.ProjectRoot;
            var input = new EditorGuardPlan.Input
            {
                ActiveScenePath = SceneManager.GetActiveScene().path ?? string.Empty,
                LoadedSceneCount = SceneManager.sceneCount,
                AnyDirty = AnyLoadedSceneDirty(),
                EditScene = settings.EditScene ?? string.Empty,
                EditSceneExists = !string.IsNullOrEmpty(settings.EditScene) && File.Exists(Path.Combine(root, settings.EditScene)),
                Policy = settings.DirtyScenes,
                Refresh = refresh
            };

            foreach (EditorGuardStep step in EditorGuardPlan.Decide(input))
            {
                Apply(step, input);
                report.Steps.Add(step.ToString());
            }

            report.ActiveScene = SceneManager.GetActiveScene().path;
            if (report.Steps.Count > (refresh ? 1 : 0))
                Debug.Log("[Aidp] editor guard: " + string.Join(", ", report.Steps) + " (scene: " + report.ActiveScene + ")");
            EditorSessionJson.Save(ReportKey, report);
            return report;
        }

        /// <summary>Le dernier rapport de garde de la session, ou null.</summary>
        public static EditorGuardReport LastReport() => EditorSessionJson.Load<EditorGuardReport>(ReportKey);

        private static void Apply(EditorGuardStep step, EditorGuardPlan.Input input)
        {
            switch (step)
            {
                case EditorGuardStep.SaveOpenScenes:
                    EditorSceneManager.SaveOpenScenes();
                    break;
                case EditorGuardStep.OpenEditScene:
                    EditorSceneManager.OpenScene(input.EditScene, OpenSceneMode.Single);
                    break;
                case EditorGuardStep.ReopenActiveScene:
                    EditorSceneManager.OpenScene(input.ActiveScenePath, OpenSceneMode.Single);
                    break;
                case EditorGuardStep.NewEmptyScene:
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    break;
                case EditorGuardStep.RefreshAssets:
                    AssetDatabase.Refresh();
                    break;
            }
        }

        private static bool AnyLoadedSceneDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    return true;
            }

            return false;
        }

        private static EditorGuardReport Skip(EditorGuardReport report, string reason)
        {
            report.Skipped = reason;
            EditorSessionJson.Save(ReportKey, report);
            return report;
        }

        private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        [UnityEditor.MenuItem(MenuItem)]
        private static void PrepareFromMenu()
        {
            EditorGuardReport report = Prepare();
            Debug.Log(report.Skipped != null ? "[Aidp] editor guard skipped: " + report.Skipped : "[Aidp] editor guard done: " + string.Join(", ", report.Steps));
        }
    }
}
