namespace Aidp.Playtest.Editor
{
    /// <summary>Ce que la garde fait d'une scene ouverte avec des modifications non sauvegardees avant une porte.</summary>
    public enum DirtyScenePolicy
    {
        /// <summary>Les modifications sont abandonnees : la scene est rechargee depuis le disque. Aucune boite de dialogue possible.</summary>
        Discard = 0,

        /// <summary>Les scenes ouvertes sont sauvegardees avant la porte.</summary>
        Save = 1,

        /// <summary>La garde ne touche pas aux scenes (comportement d'avant : une scene sale peut provoquer une boite de dialogue).</summary>
        Ignore = 2
    }
}
