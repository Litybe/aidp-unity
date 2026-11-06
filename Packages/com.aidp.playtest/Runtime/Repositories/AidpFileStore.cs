using System.IO;
using System.Text;

namespace Aidp.Playtest
{
    /// <summary>Ecriture des fichiers du pont : tout I/O du package passe ici. Ecriture atomique (tmp puis move) pour qu'un lecteur ne voie jamais un JSON partiel.</summary>
    public static class AidpFileStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void WriteAtomic(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            string tmp = path + PlaytestConstants.TempExtension;
            File.WriteAllText(tmp, text, Utf8NoBom);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }

        public static void WriteBytes(string path, byte[] bytes)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
        }

        public static string ReadText(string path) => File.ReadAllText(path, Utf8NoBom);

        public static void WriteResult(string projectRoot, PlaytestResult result)
        {
            WriteAtomic(PlaytestPaths.PlaytestResultFile(projectRoot, result.Id), result.ToJsonString());
        }
    }
}
