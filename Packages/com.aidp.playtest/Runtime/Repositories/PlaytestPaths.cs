using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>Chemins du pont fichier (C1, C2), relatifs au dossier du projet.</summary>
    public static class PlaytestPaths
    {
        private static readonly Regex ValidId = new Regex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);

        /// <summary>Un id sert de nom de fichier : lettres, chiffres, point, tiret, soulignement.</summary>
        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 120 && ValidId.IsMatch(id);

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        public static string PlaytestRequestsDir(string root) => Combine(root, PlaytestConstants.PlaytestDirectory, PlaytestConstants.RequestsDirectory);

        public static string PlaytestResultsDir(string root) => Combine(root, PlaytestConstants.PlaytestDirectory, PlaytestConstants.ResultsDirectory);

        public static string CompileRequestsDir(string root) => Combine(root, PlaytestConstants.CompileDirectory, PlaytestConstants.RequestsDirectory);

        public static string CompileResultsDir(string root) => Combine(root, PlaytestConstants.CompileDirectory, PlaytestConstants.ResultsDirectory);

        public static string TestRequestsDir(string root) => Path.Combine(root, PlaytestConstants.AidpDirectory, PlaytestConstants.TestResultsDirectory, PlaytestConstants.RequestsDirectory);

        public static string TestResultFile(string root, string id, string mode) =>
            Path.Combine(root, PlaytestConstants.AidpDirectory, PlaytestConstants.TestResultsDirectory, id + "-" + mode + PlaytestConstants.JsonExtension);

        /// <summary>Battement de coeur de l'editeur (.aidp/editor/heartbeat.json), lu par le harness pour distinguer un editeur lent d'un editeur bloque.</summary>
        public static string HeartbeatFile(string root) => Path.Combine(root, PlaytestConstants.AidpDirectory, PlaytestConstants.EditorDirectory, PlaytestConstants.HeartbeatFile);

        public static string RecordingsDir(string root) => Combine(root, PlaytestConstants.PlaytestDirectory, PlaytestConstants.RecordingsDirectory);

        public static string PlaytestRequestFile(string root, string id) => Path.Combine(PlaytestRequestsDir(root), id + PlaytestConstants.JsonExtension);

        public static string PlaytestResultFile(string root, string id) => Path.Combine(PlaytestResultsDir(root), id + PlaytestConstants.JsonExtension);

        public static string CompileResultFile(string root, string id) => Path.Combine(CompileResultsDir(root), id + PlaytestConstants.JsonExtension);

        /// <summary>Dossier des captures d'un scenario, relatif au projet, avec des "/".</summary>
        public static string RelativeCaptureDir(string id) =>
            PlaytestConstants.AidpDirectory + "/" + PlaytestConstants.PlaytestDirectory + "/" + PlaytestConstants.ResultsDirectory + "/" + id;

        private static string Combine(string root, string area, string kind) =>
            Path.Combine(root, PlaytestConstants.AidpDirectory, area, kind);
    }
}
