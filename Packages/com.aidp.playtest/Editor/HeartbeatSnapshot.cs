using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Aidp.Playtest.Editor
{
    /// <summary>
    /// Une ligne de vie de l'editeur, telle qu'ecrite dans .aidp/editor/heartbeat.json. Pure : <see cref="EditorHeartbeat"/> la remplit.
    /// Un lecteur qui voit <c>at</c> vieillir au-dela de <see cref="StaleAfterSeconds"/> alors que le process Unity vit et que
    /// l'etat n'est pas <see cref="StateReloading"/> sait que le thread principal est bloque (boite de dialogue, gel).
    /// </summary>
    public sealed class HeartbeatSnapshot
    {
        public const string StateIdle = "idle";
        public const string StateCompiling = "compiling";
        public const string StateImporting = "importing";
        public const string StateCompileFailed = "compile-failed";
        public const string StateEnteringPlay = "entering-play";
        public const string StatePlaying = "playing";
        public const string StatePaused = "paused";
        public const string StateReloading = "reloading";
        public const string StateQuitting = "quitting";

        /// <summary>Au-dela de ce silence, un heartbeat est considere perime par les lecteurs.</summary>
        public const int StaleAfterSeconds = 15;

        public string At { get; set; }
        public int Pid { get; set; }
        public string UnityVersion { get; set; }
        public string ProjectRoot { get; set; }
        public string State { get; set; }
        public string ActiveScene { get; set; }
        public bool Dirty { get; set; }
        public string Playtest { get; set; }
        public bool CompileFailed { get; set; }
        public LastError LastError { get; set; }
        public EditorGuardReport LastGuard { get; set; }

        /// <summary>L'etat a partir des drapeaux de l'editeur, dans l'ordre de priorite d'un lecteur.</summary>
        public static string StateOf(bool playing, bool willChangePlayMode, bool paused, bool compiling, bool updating, bool compileFailed)
        {
            if (playing)
                return paused ? StatePaused : StatePlaying;
            if (willChangePlayMode)
                return StateEnteringPlay;
            if (compiling)
                return StateCompiling;
            if (updating)
                return StateImporting;
            return compileFailed ? StateCompileFailed : StateIdle;
        }

        public JObject ToJson()
        {
            var json = new JObject
            {
                ["at"] = At,
                ["pid"] = Pid,
                ["unityVersion"] = UnityVersion,
                ["projectRoot"] = ProjectRoot,
                ["state"] = State,
                ["activeScene"] = ActiveScene,
                ["dirty"] = Dirty,
                ["playtest"] = NullableText(Playtest),
                ["compileFailed"] = CompileFailed,
                ["staleAfterSeconds"] = StaleAfterSeconds,
                ["lastError"] = LastError == null ? null : new JObject { ["message"] = LastError.Message, ["at"] = LastError.At },
                ["lastGuard"] = LastGuard == null ? null : GuardJson(LastGuard)
            };
            return json;
        }

        /// <summary>
        /// Un texte absent est un vrai JSON null (JTokenType.Null) : la conversion implicite string vers JToken
        /// fabrique sinon un JValue de type String portant null.
        /// </summary>
        private static JToken NullableText(string value)
        {
            return value == null ? JValue.CreateNull() : new JValue(value);
        }

        private static JObject GuardJson(EditorGuardReport report)
        {
            var steps = new JArray();
            foreach (string step in report.Steps ?? new List<string>())
                steps.Add(step);
            return new JObject { ["at"] = report.At, ["steps"] = steps, ["skipped"] = NullableText(report.Skipped), ["activeScene"] = report.ActiveScene };
        }
    }

    /// <summary>La derniere erreur ou exception vue dans la console (SessionState).</summary>
    public sealed class LastError
    {
        public string Message { get; set; }
        public string At { get; set; }
    }
}
