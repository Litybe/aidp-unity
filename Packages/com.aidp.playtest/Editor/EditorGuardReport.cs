using System.Collections.Generic;

namespace Aidp.Playtest.Editor
{
    /// <summary>Ce que la derniere garde a fait, pour le journal et le heartbeat.</summary>
    public sealed class EditorGuardReport
    {
        public string At { get; set; }
        public List<string> Steps { get; set; } = new List<string>();
        public string Skipped { get; set; }
        public string ActiveScene { get; set; }
    }
}
