using System;

namespace Aidp.Playtest
{
    /// <summary>Erreur attendue d'un scenario (sonde ou commande inconnue, valeur invalide). Le message va dans le resultat.</summary>
    public sealed class PlaytestException : Exception
    {
        public PlaytestException(string message) : base(message)
        {
        }
    }

    /// <summary>Scenario ou assertion mal forme.</summary>
    public sealed class ScenarioParseException : Exception
    {
        public ScenarioParseException(string message) : base(message)
        {
        }
    }
}
