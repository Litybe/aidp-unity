using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Aidp.Playtest.Editor
{
    /// <summary>Un message de compilation (erreur) au format du contrat C1.</summary>
    public sealed class CompileIssue
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Construit le JSON de resultat de la compile gate (C1) et normalise les messages du compilateur.</summary>
    public static class CompileResultBuilder
    {
        private static readonly Regex Diagnostic = new Regex(@"(?:error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<text>.*)$",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>"Assets/X.cs(12,5): error CS0103: ..." devient { file, line, column, "CS0103: ..." }.</summary>
        public static CompileIssue ToIssue(string file, int line, int column, string rawMessage, string projectRoot)
        {
            string message = (rawMessage ?? string.Empty).Trim();
            Match m = Diagnostic.Match(message);
            if (m.Success)
                message = m.Groups["code"].Value + ": " + m.Groups["text"].Value.Trim();
            return new CompileIssue { File = NormalizePath(file, projectRoot), Line = line, Column = column, Message = message };
        }

        public static string NormalizePath(string file, string projectRoot)
        {
            if (string.IsNullOrEmpty(file))
                return string.Empty;
            string path = file.Replace('\\', '/');
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(root.Length);
            }

            return path;
        }

        public static JObject Build(string id, IReadOnlyList<CompileIssue> errors, int warnings, long durationMs, string unityVersion, DateTime finishedUtc)
        {
            var list = new JArray();
            foreach (CompileIssue e in errors)
            {
                list.Add(new JObject
                {
                    ["file"] = e.File,
                    ["line"] = e.Line,
                    ["column"] = e.Column,
                    ["message"] = e.Message
                });
            }

            return new JObject
            {
                ["id"] = id,
                ["ok"] = errors.Count == 0,
                ["errors"] = list,
                ["warnings"] = warnings,
                ["durationMs"] = durationMs,
                ["unityVersion"] = unityVersion,
                ["finishedAt"] = finishedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            };
        }
    }
}
