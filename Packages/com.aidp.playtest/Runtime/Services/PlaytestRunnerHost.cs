using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>
    /// MonoBehaviour qui porte l'execution d'un scenario en Play Mode (le pont Editor le cree). Survit aux changements de scene,
    /// enregistre les images du GIF en parallele, puis rend le resultat par callback et se detruit.
    /// </summary>
    public sealed class PlaytestRunnerHost : MonoBehaviour
    {
        private const string HostObjectName = "AidpPlaytestRunner";

        private Scenario _scenario;
        private PlaytestOptions _options;
        private Action<PlaytestResult> _onFinished;
        private bool _recording;

        /// <summary>Cree l'hote et lance le scenario. <paramref name="onFinished"/> est appele une fois, avec le resultat complet.</summary>
        public static PlaytestRunnerHost Launch(Scenario scenario, PlaytestOptions options, Action<PlaytestResult> onFinished)
        {
            var go = new GameObject(HostObjectName);
            DontDestroyOnLoad(go);
            var host = go.AddComponent<PlaytestRunnerHost>();
            host._scenario = scenario;
            host._options = options;
            host._onFinished = onFinished;
            host.StartCoroutine(host.Execute());
            return host;
        }

        private IEnumerator Execute()
        {
            var result = new PlaytestResult(_scenario.Id);
            string gifFramesDir = null;
            if (_options.GifFrameIntervalMs > 0)
            {
                gifFramesDir = Path.Combine(_options.ProjectRoot, PlaytestPaths.RelativeCaptureDir(_scenario.Id), PlaytestConstants.FramesDirectory);
                if (Directory.Exists(gifFramesDir))
                    Directory.Delete(gifFramesDir, true);
                _recording = true;
                StartCoroutine(RecordFrames(gifFramesDir));
            }

            PlaytestRegistry registry = PlaytestRegistry.FromLoadedAssemblies();
            var runner = new PlaytestRunner(registry, _options);
            yield return runner.Run(_scenario, result);

            _recording = false;
            yield return null;
            try
            {
                _onFinished?.Invoke(result);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator RecordFrames(string framesDir)
        {
            float interval = _options.GifFrameIntervalMs / 1000f;
            int index = 0;
            while (_recording && index < _options.GifMaxFrames)
            {
                string path = Path.Combine(framesDir, index.ToString("D4") + PlaytestConstants.PngExtension);
                bool ok = false;
                yield return ScreenshotService.CaptureTo(path, done => ok = done);
                if (!ok)
                    yield break;
                index++;
                yield return new WaitForSecondsRealtime(interval);
            }
        }
    }
}
