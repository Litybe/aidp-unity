using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Enregistrement d'une repro : jouer en Play Mode, puis sauver la partie en scenario JSON (.aidp/playtest/recordings/).
    /// Appelable par menu ou par MCP (<c>Aidp.Playtest.Editor.ReproRecordingMenu.Start()</c> / <c>Stop()</c>).
    /// </summary>
    public static class ReproRecordingMenu
    {
        private const string StartItem = "Tools/AIDP/Playtest/Start repro recording";
        private const string StopItem = "Tools/AIDP/Playtest/Stop repro recording and save";

        private static ReproRecorder _recorder;

        public static void Start()
        {
            if (!Application.isPlaying)
                throw new PlaytestException("enter play mode first");
            _recorder?.Dispose();
            _recorder = new ReproRecorder();
            _recorder.Start();
            Debug.Log("[Aidp] repro recording started");
        }

        /// <summary>Arrete l'enregistrement et rend le chemin du scenario ecrit.</summary>
        public static string Stop()
        {
            if (_recorder == null || !_recorder.IsRecording)
                throw new PlaytestException("no recording in progress");
            string id = ReproRecorder.NewId();
            string json = _recorder.Stop(id, SceneManager.GetActiveScene().path);
            _recorder = null;
            string path = Path.Combine(PlaytestPaths.RecordingsDir(PlaytestPaths.ProjectRoot), id + PlaytestConstants.JsonExtension);
            AidpFileStore.WriteAtomic(path, json);
            Debug.Log("[Aidp] repro saved: " + path);
            return path;
        }

        [MenuItem(StartItem)]
        private static void StartMenu() => Start();

        [MenuItem(StartItem, true)]
        private static bool StartValidate() => Application.isPlaying && (_recorder == null || !_recorder.IsRecording);

        [MenuItem(StopItem)]
        private static void StopMenu() => Stop();

        [MenuItem(StopItem, true)]
        private static bool StopValidate() => _recorder != null && _recorder.IsRecording;
    }
}
