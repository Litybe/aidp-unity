using UnityEditor;
using UnityEngine;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Reglages du harness pour l'editeur de test, versionnes dans ProjectSettings/AidpPlaytest.asset. Menu
    /// <c>Tools/AIDP/Playtest/Settings</c>, ou <c>AidpEditorSettings.instance</c> par code.
    /// </summary>
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    public sealed class AidpEditorSettings : ScriptableSingleton<AidpEditorSettings>
    {
        public const string AssetPath = "ProjectSettings/AidpPlaytest.asset";

        [SerializeField] private DirtyScenePolicy dirtyScenes = DirtyScenePolicy.Discard;
        [SerializeField] private string editScene = PlaytestConstants.DefaultArenaScene;
        [SerializeField] private bool applyEditorPreferences = true;
        [SerializeField] private bool disableDomainReload;
        [SerializeField] private bool heartbeat = true;

        /// <summary>Sort d'une scene sale avant chaque porte (defaut : abandon, rechargement depuis le disque).</summary>
        public DirtyScenePolicy DirtyScenes { get => dirtyScenes; set => dirtyScenes = value; }

        /// <summary>
        /// Scene ouverte en mode edition avant chaque porte (defaut : l'arene). Un checkout git ne modifie alors jamais le fichier
        /// de la scene ouverte, et aucun niveau ne reste charge dans l'editeur de test. Vide : la scene ouverte est laissee telle quelle.
        /// </summary>
        public string EditScene { get => editScene; set => editScene = value; }

        /// <summary>Applique au chargement les preferences editeur qui evitent les interruptions (voir <see cref="EditorPreferencesBootstrap"/>).</summary>
        public bool ApplyEditorPreferences { get => applyEditorPreferences; set => applyEditorPreferences = value; }

        /// <summary>Active Enter Play Mode Options sans domain reload : entree en Play Mode quasi instantanee, a condition que le jeu remette ses statiques a zero.</summary>
        public bool DisableDomainReload { get => disableDomainReload; set => disableDomainReload = value; }

        /// <summary>Ecrit .aidp/editor/heartbeat.json toutes les deux secondes (voir <see cref="EditorHeartbeat"/>).</summary>
        public bool Heartbeat { get => heartbeat; set => heartbeat = value; }

        public void SaveToDisk() => Save(true);

        [MenuItem("Tools/AIDP/Playtest/Settings")]
        private static void Select() => Selection.activeObject = instance;
    }
}
