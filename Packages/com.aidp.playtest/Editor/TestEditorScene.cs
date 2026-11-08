using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Editeur dedie aux tests (fichier .aidp/test-editor, absent d'un projet de dev) : il reste sur une scene
    /// sans fichier. Une scene ouverte que git change sous l'editeur ouvre la fenetre modale « Reload Scene »,
    /// qui gele l'editeur et donc le pont. Les scenarios chargent leur scene eux-memes en Play Mode.
    /// </summary>
    public static class TestEditorScene
    {
        private const string MarkerFile = "test-editor";

        /// <summary>Referme les scenes de l'editeur de test ; sans effet dans un projet de dev ou en Play Mode.</summary>
        public static void LeaveSceneFiles()
        {
            string marker = Path.Combine(PlaytestPaths.ProjectRoot, PlaytestConstants.AidpDirectory, MarkerFile);
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (ShouldLeave(File.Exists(marker), playing, SceneManager.sceneCount, SceneManager.GetActiveScene().path))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        public static bool ShouldLeave(bool marked, bool playing, int sceneCount, string activePath) =>
            marked && !playing && (sceneCount > 1 || !string.IsNullOrEmpty(activePath));
    }
}
