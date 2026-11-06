using System.Collections.Generic;

namespace Aidp.Playtest.Editor
{
    /// <summary>Etat du pont playtest (SessionState) : file d'attente, scenario en cours et sa phase.</summary>
    internal sealed class BridgeState
    {
        public const string PhaseEntering = "entering";
        public const string PhaseRunning = "running";
        public const string PhaseFinishing = "finishing";

        public List<string> Queue { get; set; } = new List<string>();
        public List<string> Seen { get; set; } = new List<string>();
        public string CurrentId { get; set; }
        public string Phase { get; set; }
        public string ScenarioJson { get; set; }
        public string ResultJson { get; set; }
    }
}
