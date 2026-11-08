namespace Aidp.Playtest
{
    /// <summary>
    /// Sonde a champs dynamiques : le champ lu n'est connu qu'a la lecture (ex. "object.fixture1.x"). Prioritaire sur
    /// <see cref="IPlaytestProbe.Read"/> quand la classe implemente les deux.
    /// </summary>
    public interface IPlaytestFieldProbe : IPlaytestProbe
    {
        /// <summary>Vrai et <paramref name="value"/> rempli, ou faux avec <paramref name="error"/>.</summary>
        bool TryReadField(string field, out object value, out string error);
    }
}
