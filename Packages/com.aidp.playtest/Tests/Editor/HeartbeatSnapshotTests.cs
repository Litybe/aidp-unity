using System.Collections.Generic;
using Aidp.Playtest.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Aidp.Playtest.Tests
{
    public sealed class HeartbeatSnapshotTests
    {
        [Test]
        public void StateFollowsTheEditorFlagsInPriorityOrder()
        {
            Assert.AreEqual(HeartbeatSnapshot.StatePlaying, HeartbeatSnapshot.StateOf(true, true, false, true, true, true));
            Assert.AreEqual(HeartbeatSnapshot.StatePaused, HeartbeatSnapshot.StateOf(true, true, true, false, false, false));
            Assert.AreEqual(HeartbeatSnapshot.StateEnteringPlay, HeartbeatSnapshot.StateOf(false, true, false, false, false, false));
            Assert.AreEqual(HeartbeatSnapshot.StateCompiling, HeartbeatSnapshot.StateOf(false, false, false, true, true, false));
            Assert.AreEqual(HeartbeatSnapshot.StateImporting, HeartbeatSnapshot.StateOf(false, false, false, false, true, false));
            Assert.AreEqual(HeartbeatSnapshot.StateCompileFailed, HeartbeatSnapshot.StateOf(false, false, false, false, false, true));
            Assert.AreEqual(HeartbeatSnapshot.StateIdle, HeartbeatSnapshot.StateOf(false, false, false, false, false, false));
        }

        [Test]
        public void JsonMatchesTheContract()
        {
            var snapshot = new HeartbeatSnapshot
            {
                At = "2026-11-09T10:00:00.000Z",
                Pid = 4242,
                UnityVersion = "6000.0.1f1",
                ProjectRoot = "D:/Game",
                State = HeartbeatSnapshot.StateIdle,
                ActiveScene = "Assets/Tests/Playtest/Scenes/PlaytestArena.unity",
                Dirty = false,
                Playtest = null,
                CompileFailed = false,
                LastError = new LastError { Message = "NullReferenceException", At = "2026-11-09T09:59:00Z" },
                LastGuard = new EditorGuardReport { At = "2026-11-09T09:58:00Z", Steps = new List<string> { "OpenEditScene", "RefreshAssets" } }
            };

            JObject json = snapshot.ToJson();

            CollectionAssert.AreEqual(
                new[] { "at", "pid", "unityVersion", "projectRoot", "state", "activeScene", "dirty", "playtest", "compileFailed", "staleAfterSeconds", "lastError", "lastGuard" },
                KeysOf(json));
            Assert.AreEqual("idle", (string)json["state"]);
            Assert.AreEqual(4242, (int)json["pid"]);
            Assert.AreEqual(JTokenType.Null, json["playtest"].Type);
            Assert.AreEqual(HeartbeatSnapshot.StaleAfterSeconds, (int)json["staleAfterSeconds"]);
            Assert.AreEqual("NullReferenceException", (string)json["lastError"]["message"]);
            Assert.AreEqual("RefreshAssets", (string)json["lastGuard"]["steps"][1]);
            Assert.AreEqual(JTokenType.Null, json["lastGuard"]["skipped"].Type);
        }

        [Test]
        public void MissingErrorAndGuardAreNull()
        {
            JObject json = new HeartbeatSnapshot { State = HeartbeatSnapshot.StateReloading }.ToJson();
            Assert.AreEqual(JTokenType.Null, json["lastError"].Type);
            Assert.AreEqual(JTokenType.Null, json["lastGuard"].Type);
            Assert.AreEqual("reloading", (string)json["state"]);
        }

        private static List<string> KeysOf(JObject json)
        {
            var keys = new List<string>();
            foreach (JProperty property in json.Properties())
                keys.Add(property.Name);
            return keys;
        }
    }
}
