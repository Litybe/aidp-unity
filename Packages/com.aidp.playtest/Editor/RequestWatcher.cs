using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Surveille .aidp/playtest/requests/ et .aidp/compile/requests/ par FileSystemWatcher. Les evenements arrivent sur un thread
    /// du pool et sont postes sur le thread principal par le SynchronizationContext d'Unity : aucun abonnement a
    /// EditorApplication.update, donc un cout CPU nul quand rien ne se passe.
    /// </summary>
    internal static class RequestWatcher
    {
        private const int InternalBufferBytes = 16 * 1024;
        private static readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private static SynchronizationContext _main;
        private static readonly ConcurrentQueue<Action> Fallback = new ConcurrentQueue<Action>();
        private static bool _fallbackHooked;

        public static void Start(string projectRoot, Action<string> onPlaytestRequest, Action<string> onCompileRequest, Action<string> onTestRequest)
        {
            Stop();
            _main = SynchronizationContext.Current;
            Watch(PlaytestPaths.PlaytestRequestsDir(projectRoot), onPlaytestRequest);
            Watch(PlaytestPaths.CompileRequestsDir(projectRoot), onCompileRequest);
            Watch(PlaytestPaths.TestRequestsDir(projectRoot), onTestRequest);
        }

        public static void Stop()
        {
            foreach (FileSystemWatcher w in Watchers)
                w.Dispose();
            Watchers.Clear();
        }

        private static void Watch(string dir, Action<string> callback)
        {
            Directory.CreateDirectory(dir);
            var watcher = new FileSystemWatcher(dir, "*" + PlaytestConstants.JsonExtension)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = InternalBufferBytes,
                IncludeSubdirectories = false
            };
            FileSystemEventHandler handler = (sender, e) => Dispatch(() => callback(e.FullPath));
            watcher.Created += handler;
            watcher.Changed += handler;
            watcher.Renamed += (sender, e) => Dispatch(() => callback(e.FullPath));
            watcher.EnableRaisingEvents = true;
            Watchers.Add(watcher);
        }

        private static void Dispatch(Action action)
        {
            if (_main != null)
            {
                _main.Post(_ => action(), null);
                return;
            }

            // Pas de contexte de synchronisation (cas anormal) : file d'attente vidée par un hook update, retire des qu'elle est vide.
            Fallback.Enqueue(action);
            EditorApplication.delayCall += HookFallback;
        }

        private static void HookFallback()
        {
            if (_fallbackHooked)
                return;
            _fallbackHooked = true;
            EditorApplication.update += DrainFallback;
        }

        private static void DrainFallback()
        {
            while (Fallback.TryDequeue(out Action action))
                action();
            EditorApplication.update -= DrainFallback;
            _fallbackHooked = false;
        }
    }
}
