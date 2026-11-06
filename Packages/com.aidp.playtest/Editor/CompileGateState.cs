using System.Collections.Generic;

namespace Aidp.Playtest.Editor
{
    /// <summary>Etat d'une compile gate en cours (SessionState, survit au domain reload).</summary>
    internal sealed class CompileGateState
    {
        public List<string> Ids { get; set; } = new List<string>();
        public long StartedUtcTicks { get; set; }
        public long LastEventUtcTicks { get; set; }
        public bool SawCompile { get; set; }
        public bool CompileFinished { get; set; }
        public bool Retried { get; set; }
        public List<CompileIssue> Errors { get; set; } = new List<CompileIssue>();
        public int Warnings { get; set; }
    }
}
