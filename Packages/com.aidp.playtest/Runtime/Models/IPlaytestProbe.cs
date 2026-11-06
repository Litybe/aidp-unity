using System;
using System.Collections.Generic;

namespace Aidp.Playtest
{
    /// <summary>
    /// Sonde d'etat : expose ce qu'un test lit (position du joueur, PV, salle...). Une classe par sonde,
    /// avec un constructeur sans parametre et l'attribut <see cref="PlaytestProbeAttribute"/>.
    /// Les valeurs sont des nombres, des booleens ou des chaines ; on les lit par "nom.champ" dans les assertions.
    /// </summary>
    public interface IPlaytestProbe
    {
        /// <summary>Instantane des champs, ou null si le sujet n'existe pas (pas de joueur en scene).</summary>
        IReadOnlyDictionary<string, object> Read();
    }

    /// <summary>Declare une sonde sous un nom (premier segment de "player.x").</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PlaytestProbeAttribute : Attribute
    {
        public PlaytestProbeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
