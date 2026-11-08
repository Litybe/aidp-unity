namespace Aidp.Playtest
{
    /// <summary>Reglages d'execution d'un scenario.</summary>
    public sealed class PlaytestOptions
    {
        /// <summary>Dossier du projet (parent de Assets). Les captures sont ecrites sous .aidp/playtest/results/id/.</summary>
        public string ProjectRoot { get; set; }

        /// <summary>0 = pas d'enregistrement ; sinon une image tous les N ms pour fabriquer un GIF.</summary>
        public int GifFrameIntervalMs { get; set; }

        /// <summary>Scene jouee quand le scenario n'a pas de champ "scene" (si le fichier existe) ; vide = scene courante.</summary>
        public string DefaultScene { get; set; } = PlaytestConstants.DefaultArenaScene;

        public int GifMaxFrames { get; set; } = PlaytestConstants.GifMaxFrames;
    }
}
