namespace Aidp.Playtest.Editor
{
    /// <summary>Une action de la garde d'editeur, dans l'ordre ou <see cref="EditorGuardPlan"/> les range.</summary>
    public enum EditorGuardStep
    {
        /// <summary>Sauvegarder les scenes ouvertes (politique <see cref="DirtyScenePolicy.Save"/>).</summary>
        SaveOpenScenes,

        /// <summary>Ouvrir la scene d'edition en mode Single : remplace les scenes ouvertes, modifications abandonnees.</summary>
        OpenEditScene,

        /// <summary>Recharger la scene active depuis le disque (pas de scene d'edition, scene sale, politique Discard).</summary>
        ReopenActiveScene,

        /// <summary>Remplacer une scene sans fichier (Untitled) et sale par une scene vide.</summary>
        NewEmptyScene,

        /// <summary>AssetDatabase.Refresh : importer ce que le harness a ecrit sur disque pendant que l'editeur n'avait pas le focus.</summary>
        RefreshAssets
    }
}
