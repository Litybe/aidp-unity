using System;

namespace Aidp.Playtest
{
    /// <summary>
    /// Commande de debug jouee dans "setup" (ou entre deux steps) : teleport, donner une capacite, charger une salle...
    /// Une classe par commande, constructeur sans parametre, attribut <see cref="PlaytestCommandAttribute"/>.
    /// Lever <see cref="PlaytestException"/> pour faire echouer le scenario avec un message clair.
    /// </summary>
    public interface IPlaytestCommand
    {
        void Execute(PlaytestArgs args);
    }

    /// <summary>Declare une commande sous un nom (valeur de "cmd" dans le scenario).</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PlaytestCommandAttribute : Attribute
    {
        public PlaytestCommandAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
