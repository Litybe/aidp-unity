using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Pont playtest (contrat C2). <see cref="Run"/> met un scenario en file ; le pont entre en Play Mode, le joue avec
    /// <see cref="PlaytestRunnerHost"/>, sort du Play Mode et ecrit .aidp/playtest/results/&lt;id&gt;.json. L'etat vit dans SessionState :
    /// il survit au domain reload de l'entree en Play Mode. Les demandes deposees dans .aidp/playtest/requests/ sont prises par
    /// <see cref="RequestWatcher"/> (aucun polling).
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestBridge
    {
        private const string StateKey = "Aidp.PlaytestBridge.State";
        private const int MaxSeen = 200;
        private const double PartialFileGraceSeconds = 2;
        private const string AlreadyPlayingMessage = "editor is already in play mode: refusing to interrupt it";
        private const string CompileErrorMessage = "script compilation failed: fix compile errors before playtesting";
        private const string PlayEndedMessage = "play mode ended before the scenario completed";
        private const string NotFoundMessage = "no scenario file for this id (looked in .aidp/playtest/requests and Assets/Tests/Playtest/Scenarios)";

        static PlaytestBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += RequestWatcher.Stop;
            CompilationPipeline.compilationFinished += _ => EditorApplication.delayCall += Pump;
            RequestWatcher.Start(PlaytestPaths.ProjectRoot, OnPlaytestRequestFile, OnCompileRequestFile, OnTestRequestFile);
            EditorApplication.delayCall += OnEditorLoaded;
        }

        /// <summary>Met le scenario <c>requests/&lt;id&gt;.json</c> (ou, a defaut, la bibliotheque Assets/Tests/Playtest/Scenarios) en file. Rend la main tout de suite.</summary>
        public static void Run(string id)
        {
            if (!PlaytestPaths.IsValidId(id))
                throw new ArgumentException("invalid scenario id '" + id + "'", nameof(id));
            BridgeState state = LoadState();
            if (state.Queue.Contains(id) || state.CurrentId == id)
                return;
            DeleteStaleResult(id);
            state.Queue.Add(id);
            SaveState(state);
            Pump();
        }

        private static void OnEditorLoaded()
        {
            string root = PlaytestPaths.ProjectRoot;
            BridgeState state = LoadState();
            if (state.CurrentId != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Le domaine a ete recharge hors Play Mode avec un scenario ouvert : on le clot.
                FinishCurrent(state);
            }

            ScanExisting(PlaytestPaths.PlaytestRequestsDir(root), OnPlaytestRequestFile);
            ScanExisting(PlaytestPaths.CompileRequestsDir(root), OnCompileRequestFile);
            ScanExisting(PlaytestPaths.TestRequestsDir(root), OnTestRequestFile);
            Pump();
        }

        private static void ScanExisting(string dir, Action<string> handler)
        {
            if (!Directory.Exists(dir))
                return;
            foreach (string file in Directory.GetFiles(dir, "*" + PlaytestConstants.JsonExtension))
                handler(file);
        }

        private static void OnTestRequestFile(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int cut = name.LastIndexOf('-');
            if (cut <= 0 || !TestGate.TryParseMode(name.Substring(cut + 1), out string mode))
                return;
            string id = name.Substring(0, cut);
            string result = PlaytestPaths.TestResultFile(PlaytestPaths.ProjectRoot, id, mode);
            if (!TryNewRequest(path, result, out _))
                return;
            MarkSeen(path);
            TestGate.Request(id, mode);
        }

        private static void OnCompileRequestFile(string path)
        {
            string root = PlaytestPaths.ProjectRoot;
            if (!TryNewRequest(path, PlaytestPaths.CompileResultFile(root, Path.GetFileNameWithoutExtension(path)), out string id))
                return;
            CompileGate.Request(id);
        }

        private static void OnPlaytestRequestFile(string path)
        {
            string root = PlaytestPaths.ProjectRoot;
            if (!TryNewRequest(path, PlaytestPaths.PlaytestResultFile(root, Path.GetFileNameWithoutExtension(path)), out string id))
                return;

            try
            {
                JObject.Parse(AidpFileStore.ReadText(path));
            }
            catch (JsonException e)
            {
                // Fichier encore en cours d'ecriture : l'evenement Changed suivant relancera. Un fichier ancien est une vraie erreur.
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds > PartialFileGraceSeconds)
                {
                    MarkSeen(path);
                    WriteFailure(id, "invalid JSON in request: " + e.Message);
                }

                return;
            }
            catch (IOException)
            {
                return;
            }

            MarkSeen(path);
            Run(id);
        }

        /// <summary>Vrai si le fichier est une demande nouvelle : id valide, jamais vue sous cette date, sans resultat plus recent.</summary>
        private static bool TryNewRequest(string path, string resultPath, out string id)
        {
            id = Path.GetFileNameWithoutExtension(path);
            if (!PlaytestPaths.IsValidId(id) || !File.Exists(path))
                return false;
            BridgeState state = LoadState();
            if (state.Seen.Contains(SeenKey(path)))
                return false;
            if (File.Exists(resultPath) && File.GetLastWriteTimeUtc(resultPath) >= File.GetLastWriteTimeUtc(path))
                return false;
            return true;
        }

        private static string SeenKey(string path) => Path.GetFileNameWithoutExtension(path) + "@" + File.GetLastWriteTimeUtc(path).Ticks;

        private static void MarkSeen(string path)
        {
            BridgeState state = LoadState();
            state.Seen.Add(SeenKey(path));
            if (state.Seen.Count > MaxSeen)
                state.Seen.RemoveRange(0, state.Seen.Count - MaxSeen);
            SaveState(state);
        }

        private static void DeleteStaleResult(string id)
        {
            string root = PlaytestPaths.ProjectRoot;
            string result = PlaytestPaths.PlaytestResultFile(root, id);
            if (File.Exists(result))
                File.Delete(result);
        }

        /// <summary>L'id du scenario en cours de lecture, ou null (lu par le heartbeat).</summary>
        public static string CurrentScenarioId() => LoadState().CurrentId;

        /// <summary>Lance le prochain scenario de la file s'il n'y en a pas en cours.</summary>
        private static void Pump()
        {
            bool guarded = false;
            while (true)
            {
                BridgeState state = LoadState();
                if (state.CurrentId != null || state.Queue.Count == 0)
                    return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    // Reessai unique a la fin de la compilation (voir l'abonnement du constructeur) ou de l'import.
                    EditorApplication.delayCall += Pump;
                    return;
                }

                if (!guarded && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // Etat neutre avant d'entrer en Play Mode : scenes sales reglees, scene d'edition ouverte, assets a jour.
                    // Si le refresh declenche une compilation, on reprend a sa fin (le scenario est toujours en file).
                    guarded = true;
                    EditorGuard.Prepare();
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    {
                        EditorApplication.delayCall += Pump;
                        return;
                    }
                }

                string id = state.Queue[0];
                state.Queue.RemoveAt(0);
                SaveState(state);

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    WriteFailure(id, AlreadyPlayingMessage);
                    continue;
                }

                if (EditorUtility.scriptCompilationFailed)
                {
                    WriteFailure(id, CompileErrorMessage);
                    continue;
                }

                string json = ReadScenarioJson(id);
                if (json == null)
                {
                    WriteFailure(id, NotFoundMessage);
                    continue;
                }

                try
                {
                    ScenarioParser.Parse(json, id);
                }
                catch (ScenarioParseException e)
                {
                    WriteFailure(id, "invalid scenario: " + e.Message);
                    continue;
                }

                state.CurrentId = id;
                state.Phase = BridgeState.PhaseEntering;
                state.ScenarioJson = json;
                state.ResultJson = null;
                SaveState(state);
                EditorApplication.isPlaying = true;
                EditorApplication.delayCall += CheckPlayModeEntered;
                return;
            }
        }

        private static string ReadScenarioJson(string id)
        {
            string root = PlaytestPaths.ProjectRoot;
            string request = PlaytestPaths.PlaytestRequestFile(root, id);
            string library = Path.Combine(root, PlaytestConstants.DefaultScenarioDirectory, id + PlaytestConstants.JsonExtension);
            foreach (string path in new[] { request, library })
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    return AidpFileStore.ReadText(path);
                }
                catch (IOException)
                {
                }
            }

            return null;
        }

        private static void CheckPlayModeEntered()
        {
            BridgeState state = LoadState();
            if (state.CurrentId != null && state.Phase == BridgeState.PhaseEntering && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                state.ResultJson = PlaytestResult.Failure(state.CurrentId, "could not enter play mode").ToJsonString();
                SaveState(state);
                FinishCurrent(state);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                OnEnteredPlayMode();
            else if (change == PlayModeStateChange.EnteredEditMode)
                OnEnteredEditMode();
        }

        private static void OnEnteredPlayMode()
        {
            BridgeState state = LoadState();
            if (state.CurrentId == null || state.Phase != BridgeState.PhaseEntering)
                return;
            try
            {
                Scenario scenario = ScenarioParser.Parse(state.ScenarioJson, state.CurrentId);
                state.Phase = BridgeState.PhaseRunning;
                SaveState(state);
                var options = new PlaytestOptions
                {
                    ProjectRoot = PlaytestPaths.ProjectRoot,
                    GifFrameIntervalMs = GifEncoder.FindFfmpeg() != null ? PlaytestConstants.GifFrameIntervalMs : 0
                };
                PlaytestRunnerHost.Launch(scenario, options, OnRunnerFinished);
            }
            catch (Exception e)
            {
                state.ResultJson = PlaytestResult.Failure(state.CurrentId, "cannot start the scenario: " + e.Message).ToJsonString();
                state.Phase = BridgeState.PhaseFinishing;
                SaveState(state);
                EditorApplication.isPlaying = false;
            }
        }

        private static void OnRunnerFinished(PlaytestResult result)
        {
            BridgeState state = LoadState();
            state.ResultJson = result.ToJsonString();
            state.Phase = BridgeState.PhaseFinishing;
            SaveState(state);
            EditorApplication.isPlaying = false;
        }

        private static void OnEnteredEditMode()
        {
            BridgeState state = LoadState();
            if (state.CurrentId != null)
                FinishCurrent(state);
        }

        /// <summary>Ecrit le resultat du scenario en cours (avec GIF si possible), libere le pont et lance le suivant.</summary>
        private static void FinishCurrent(BridgeState state)
        {
            string root = PlaytestPaths.ProjectRoot;
            string id = state.CurrentId;
            JObject result = string.IsNullOrEmpty(state.ResultJson)
                ? PlaytestResult.Failure(id, PlayEndedMessage).ToJson()
                : JObject.Parse(state.ResultJson);

            string gif = GifEncoder.TryEncode(root, id);
            if (gif != null)
                result["gif"] = gif;

            try
            {
                AidpFileStore.WriteAtomic(PlaytestPaths.PlaytestResultFile(root, id), result.ToString(Formatting.Indented));
            }
            catch (IOException e)
            {
                UnityEngine.Debug.LogError("[Aidp] cannot write playtest result '" + id + "': " + e.Message);
            }

            state.CurrentId = null;
            state.Phase = null;
            state.ScenarioJson = null;
            state.ResultJson = null;
            SaveState(state);
            EditorApplication.delayCall += Pump;
        }

        private static void WriteFailure(string id, string error)
        {
            AidpFileStore.WriteResult(PlaytestPaths.ProjectRoot, PlaytestResult.Failure(id, error));
        }

        private static BridgeState LoadState() => EditorSessionJson.Load<BridgeState>(StateKey) ?? new BridgeState();

        private static void SaveState(BridgeState state) => EditorSessionJson.Save(StateKey, state);
    }
}
