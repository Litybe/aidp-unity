using System.Collections.Generic;
using System.IO;

namespace Aidp.Playtest.TestSupport
{
    /// <summary>Liste les fichiers de scenarios d'une bibliotheque de non-regression (une entree de test par fichier).</summary>
    public static class PlaytestScenarioSource
    {
        /// <summary>Marqueur rendu quand le dossier est vide : le test s'ignore au lieu d'echouer.</summary>
        public const string NoScenario = "(no scenario)";

        /// <summary>Dossier des scenarios, relatif au projet. Modifiable avant la decouverte des tests.</summary>
        public static string Directory { get; set; } = PlaytestConstants.DefaultScenarioDirectory;

        public static IEnumerable<string> Paths()
        {
            string dir = System.IO.Path.Combine(PlaytestPaths.ProjectRoot, Directory);
            var found = new List<string>();
            if (System.IO.Directory.Exists(dir))
            {
                string[] files = System.IO.Directory.GetFiles(dir, "*" + PlaytestConstants.JsonExtension);
                System.Array.Sort(files, System.StringComparer.Ordinal);
                foreach (string file in files)
                    found.Add(Directory + "/" + System.IO.Path.GetFileName(file));
            }

            if (found.Count == 0)
                found.Add(NoScenario);
            return found;
        }
    }
}
