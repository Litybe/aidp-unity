using UnityEditor;
using UnityEngine;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Fige, au chargement de l'editeur, les preferences qui evitent qu'un agent doive etre secouru : pas de recompilation
    /// pendant le Play Mode (un fichier C# ecrit pendant un scenario ne casse plus la partie), rafraichissement automatique
    /// des assets et surveillance du dossier (les ecritures du harness sont vues sans focus de la fenetre), et en option
    /// Enter Play Mode sans domain reload. Les cles EditorPrefs sont celles que la fenetre Preferences ecrit ; elles sont
    /// par utilisateur, donc propres a la machine de test. Menu <c>Tools/AIDP/Playtest/Apply editor preferences</c>.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorPreferencesBootstrap
    {
        private const string AppliedKey = "Aidp.EditorPreferences.Applied";
        private const string MenuItem = "Tools/AIDP/Playtest/Apply editor preferences";

        /// <summary>Preferences > General > Script Changes While Playing.</summary>
        private const string ScriptChangesWhilePlayingKey = "ScriptCompilationDuringPlay";
        private const int RecompileAfterFinishedPlaying = 1;

        /// <summary>Preferences > Asset Pipeline > Auto Refresh (mode depuis 2021.2, booleen avant).</summary>
        private const string AutoRefreshModeKey = "kAutoRefreshMode";
        private const int AutoRefreshEnabled = 1;
        private const string AutoRefreshLegacyKey = "kAutoRefresh";

        /// <summary>Preferences > Asset Pipeline > Directory Monitoring (Windows).</summary>
        private const string DirectoryMonitoringKey = "DirectoryMonitoring";

        static EditorPreferencesBootstrap()
        {
            if (!AidpEditorSettings.instance.ApplyEditorPreferences || SessionState.GetBool(AppliedKey, false))
                return;
            EditorApplication.delayCall += () =>
            {
                Apply();
                SessionState.SetBool(AppliedKey, true);
            };
        }

        /// <summary>Applique les preferences et rend un resume d'une ligne.</summary>
        public static string Apply()
        {
            EditorPrefs.SetInt(ScriptChangesWhilePlayingKey, RecompileAfterFinishedPlaying);
            EditorPrefs.SetInt(AutoRefreshModeKey, AutoRefreshEnabled);
            EditorPrefs.SetBool(AutoRefreshLegacyKey, true);
            EditorPrefs.SetBool(DirectoryMonitoringKey, true);

            string playMode = "domain reload kept";
            if (AidpEditorSettings.instance.DisableDomainReload)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                playMode = "domain reload disabled on play";
            }

            string summary = "[Aidp] editor preferences: script changes while playing = recompile after play, auto refresh on, directory monitoring on, " + playMode;
            Debug.Log(summary);
            return summary;
        }

        [UnityEditor.MenuItem(MenuItem)]
        private static void ApplyFromMenu() => Apply();
    }
}
