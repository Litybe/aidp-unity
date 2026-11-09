using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Ecrit .aidp/editor/heartbeat.json toutes les deux secondes (<see cref="HeartbeatSnapshot"/>), et une derniere fois
    /// juste avant un domain reload (etat <c>reloading</c>) et a la fermeture (<c>quitting</c>). Le harness lit le fichier a la
    /// place d'un timeout aveugle : un heartbeat perime avec un process Unity vivant, c'est un editeur bloque. Cout : une
    /// comparaison de date par frame, une ecriture de quelques centaines d'octets toutes les deux secondes.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorHeartbeat
    {
        private const double IntervalSeconds = 2;
        private const string LastErrorKey = "Aidp.EditorHeartbeat.LastError";
        private const int MaxErrorLength = 500;

        private static double _nextAt;

        static EditorHeartbeat()
        {
            if (!AidpEditorSettings.instance.Heartbeat)
                return;
            Application.logMessageReceived += OnLog;
            AssemblyReloadEvents.beforeAssemblyReload += () => Write(HeartbeatSnapshot.StateReloading);
            EditorApplication.quitting += () => Write(HeartbeatSnapshot.StateQuitting);
            EditorApplication.update += Tick;
        }

        /// <summary>Ecrit un heartbeat tout de suite, avec l'etat courant de l'editeur.</summary>
        public static void WriteNow() => Write(null);

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextAt)
                return;
            _nextAt = now + IntervalSeconds;
            Write(null);
        }

        private static void Write(string forcedState)
        {
            try
            {
                HeartbeatSnapshot snapshot = Capture(forcedState);
                AidpFileStore.WriteAtomic(PlaytestPaths.HeartbeatFile(PlaytestPaths.ProjectRoot), snapshot.ToJson().ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (IOException)
            {
                // Un lecteur tient le fichier : la prochaine ecriture suffira.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static HeartbeatSnapshot Capture(string forcedState)
        {
            Scene active = SceneManager.GetActiveScene();
            return new HeartbeatSnapshot
            {
                At = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                Pid = Process.GetCurrentProcess().Id,
                UnityVersion = Application.unityVersion,
                ProjectRoot = PlaytestPaths.ProjectRoot,
                State = forcedState ?? HeartbeatSnapshot.StateOf(
                    EditorApplication.isPlaying,
                    EditorApplication.isPlayingOrWillChangePlaymode,
                    EditorApplication.isPaused,
                    EditorApplication.isCompiling,
                    EditorApplication.isUpdating,
                    EditorUtility.scriptCompilationFailed),
                ActiveScene = active.path,
                Dirty = active.isDirty,
                Playtest = PlaytestBridge.CurrentScenarioId(),
                CompileFailed = EditorUtility.scriptCompilationFailed,
                LastError = EditorSessionJson.Load<LastError>(LastErrorKey),
                LastGuard = EditorGuard.LastReport()
            };
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;
            string message = condition ?? string.Empty;
            if (message.Length > MaxErrorLength)
                message = message.Substring(0, MaxErrorLength);
            EditorSessionJson.Save(LastErrorKey, new LastError
            {
                Message = message,
                At = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            });
        }
    }
}
