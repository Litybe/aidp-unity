using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Compile gate (contrat C1). <see cref="Request"/> rend la main tout de suite ; le resultat est ecrit dans
    /// .aidp/compile/results/&lt;id&gt;.json une fois la compilation terminee (et le domain reload fait quand il y en a un).
    /// Aucun abonnement permanent a EditorApplication.update : la boucle de suivi n'existe que pendant une demande.
    /// </summary>
    [InitializeOnLoad]
    public static class CompileGate
    {
        private const string StateKey = "Aidp.CompileGate.State";
        private const double TimeoutSeconds = 590;
        private const double StartGraceSeconds = 3;
        private const double ReloadGraceSeconds = 5;
        private const string PlayModeMessage = "editor is in play mode: exit play mode before compiling";
        private const string TimeoutMessage = "compilation did not finish in time";
        private const string FailedWithoutMessage = "script compilation failed (see the Unity console)";

        static CompileGate()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            ResumeAfterReload();
        }

        /// <summary>Lance AssetDatabase.Refresh puis une compilation, et ecrit le resultat dans .aidp/compile/results/&lt;requestId&gt;.json.</summary>
        public static void Request(string requestId)
        {
            if (!PlaytestPaths.IsValidId(requestId))
                throw new ArgumentException("invalid request id '" + requestId + "'", nameof(requestId));

            string stale = PlaytestPaths.CompileResultFile(PlaytestPaths.ProjectRoot, requestId);
            if (File.Exists(stale))
                File.Delete(stale);

            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            bool fresh = state == null;
            if (fresh)
                state = new CompileGateState { StartedUtcTicks = DateTime.UtcNow.Ticks, LastEventUtcTicks = DateTime.UtcNow.Ticks };
            if (!state.Ids.Contains(requestId))
                state.Ids.Add(requestId);
            EditorSessionJson.Save(StateKey, state);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Finish(state, PlayModeMessage);
                return;
            }

            Subscribe();
            if (fresh)
            {
                TestEditorScene.LeaveSceneFiles();
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
            }
        }

        private static void ResumeAfterReload()
        {
            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            if (state == null)
                return;
            Subscribe();
            if (!state.CompileFinished)
                return;
            // La compilation s'est terminee juste avant ce rechargement : l'editeur est de nouveau utilisable.
            EditorApplication.delayCall += () =>
            {
                CompileGateState current = EditorSessionJson.Load<CompileGateState>(StateKey);
                if (current != null && current.CompileFinished && !EditorApplication.isCompiling)
                    Finish(current, null);
            };
        }

        private static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void OnCompilationStarted(object context)
        {
            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            if (state == null)
                return;
            state.SawCompile = true;
            state.CompileFinished = false;
            state.Errors.Clear();
            state.Warnings = 0;
            state.LastEventUtcTicks = DateTime.UtcNow.Ticks;
            EditorSessionJson.Save(StateKey, state);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            if (state == null)
                return;
            string root = PlaytestPaths.ProjectRoot;
            foreach (CompilerMessage m in messages)
            {
                if (m.type == CompilerMessageType.Error)
                    state.Errors.Add(CompileResultBuilder.ToIssue(m.file, m.line, m.column, m.message, root));
                else if (m.type == CompilerMessageType.Warning)
                    state.Warnings++;
            }

            state.LastEventUtcTicks = DateTime.UtcNow.Ticks;
            EditorSessionJson.Save(StateKey, state);
        }

        private static void OnCompilationFinished(object context)
        {
            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            if (state == null)
                return;
            state.CompileFinished = true;
            state.LastEventUtcTicks = DateTime.UtcNow.Ticks;
            EditorSessionJson.Save(StateKey, state);
            // Avec des erreurs, Unity ne recharge pas le domaine : rien a attendre.
            if (state.Errors.Count > 0)
                Finish(state, null);
        }

        private static void Tick()
        {
            CompileGateState state = EditorSessionJson.Load<CompileGateState>(StateKey);
            if (state == null)
            {
                EditorApplication.update -= Tick;
                return;
            }

            DateTime now = DateTime.UtcNow;
            if ((now - new DateTime(state.StartedUtcTicks, DateTimeKind.Utc)).TotalSeconds > TimeoutSeconds)
            {
                Finish(state, TimeoutMessage);
                return;
            }

            if (EditorApplication.isCompiling)
                return;

            double quiet = (now - new DateTime(state.LastEventUtcTicks, DateTimeKind.Utc)).TotalSeconds;
            if (state.CompileFinished || state.SawCompile)
            {
                if (quiet > ReloadGraceSeconds)
                    Finish(state, null);
                return;
            }

            if (quiet <= StartGraceSeconds)
                return;

            // Aucune compilation lancee : rien n'a change, ou une erreur precedente n'est pas rejouee. On force une fois.
            if (EditorUtility.scriptCompilationFailed && !state.Retried)
            {
                state.Retried = true;
                state.LastEventUtcTicks = now.Ticks;
                EditorSessionJson.Save(StateKey, state);
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                return;
            }

            Finish(state, null);
        }

        private static void Finish(CompileGateState state, string forcedError)
        {
            EditorApplication.update -= Tick;
            EditorSessionJson.Clear(StateKey);

            string root = PlaytestPaths.ProjectRoot;
            var errors = new List<CompileIssue>(state.Errors);
            if (forcedError != null)
                errors.Add(new CompileIssue { File = string.Empty, Message = forcedError });
            else if (errors.Count == 0 && EditorUtility.scriptCompilationFailed)
                errors.Add(new CompileIssue { File = string.Empty, Message = FailedWithoutMessage });

            DateTime finished = DateTime.UtcNow;
            long durationMs = (long)(finished - new DateTime(state.StartedUtcTicks, DateTimeKind.Utc)).TotalMilliseconds;
            foreach (string id in state.Ids)
            {
                try
                {
                    var json = CompileResultBuilder.Build(id, errors, state.Warnings, durationMs, Application.unityVersion, finished);
                    AidpFileStore.WriteAtomic(PlaytestPaths.CompileResultFile(root, id), json.ToString(Newtonsoft.Json.Formatting.Indented));
                }
                catch (IOException e)
                {
                    Debug.LogError("[Aidp] cannot write compile result '" + id + "': " + e.Message);
                }
            }
        }
    }
}
