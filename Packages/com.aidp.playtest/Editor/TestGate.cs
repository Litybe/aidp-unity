using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Aidp.Playtest.Editor
{
    /// <summary>Un test en echec (contrat C8).</summary>
    public sealed class TestFailure
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public string Stack { get; set; }
    }

    /// <summary>
    /// Test gate (contrat C8) : <see cref="Request"/> lance les tests EditMode ou PlayMode via TestRunnerApi (retour immediat)
    /// et ecrit .aidp/test-results/id-mode.json a la fin. Les callbacks sont reenregistres a chaque domain reload
    /// (PlayMode en provoque un) ; la demande en cours vit dans SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class TestGate
    {
        private const string StateKey = "Aidp.TestGate.State";
        public const string EditMode = "EditMode";
        public const string PlayMode = "PlayMode";
        private const string NoTestsNote = "no-tests";

        private sealed class Pending
        {
            public string Id { get; set; }
            public string Mode { get; set; }
            public long StartedUtcTicks { get; set; }
        }

        static TestGate()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
        }

        public static bool TryParseMode(string text, out string mode)
        {
            mode = null;
            if (string.Equals(text, EditMode, StringComparison.OrdinalIgnoreCase))
                mode = EditMode;
            else if (string.Equals(text, PlayMode, StringComparison.OrdinalIgnoreCase))
                mode = PlayMode;
            return mode != null;
        }

        /// <param name="mode">"EditMode" ou "PlayMode".</param>
        public static void Request(string requestId, string mode)
        {
            if (!PlaytestPaths.IsValidId(requestId))
                throw new ArgumentException("invalid request id '" + requestId + "'", nameof(requestId));
            if (!TryParseMode(mode, out string canonical))
                throw new ArgumentException("mode must be EditMode or PlayMode", nameof(mode));

            string stale = PlaytestPaths.TestResultFile(PlaytestPaths.ProjectRoot, requestId, canonical);
            if (File.Exists(stale))
                File.Delete(stale);

            EditorSessionJson.Save(StateKey, new Pending { Id = requestId, Mode = canonical, StartedUtcTicks = DateTime.UtcNow.Ticks });
            TestMode testMode = canonical == EditMode ? TestMode.EditMode : TestMode.PlayMode;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RetrieveTestList(testMode, root =>
            {
                if (root == null || root.TestCaseCount == 0)
                {
                    WriteResult(TestResultBuilder.Build(requestId, canonical, 0, 0, 0, 0, 0, new List<TestFailure>(), DateTime.UtcNow, NoTestsNote));
                    EditorSessionJson.Clear(StateKey);
                    return;
                }

                api.Execute(new ExecutionSettings(new Filter { testMode = testMode }));
            });
        }

        private static void WriteResult(JObject json)
        {
            string id = (string)json["id"];
            string mode = (string)json["mode"];
            try
            {
                AidpFileStore.WriteAtomic(PlaytestPaths.TestResultFile(PlaytestPaths.ProjectRoot, id, mode), json.ToString(Formatting.Indented));
            }
            catch (IOException e)
            {
                Debug.LogError("[Aidp] cannot write test result '" + id + "': " + e.Message);
            }
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Pending pending = EditorSessionJson.Load<Pending>(StateKey);
                if (pending == null)
                    return;
                EditorSessionJson.Clear(StateKey);

                int total = 0, passed = 0, failed = 0, skipped = 0;
                var failures = new List<TestFailure>();
                Collect(result, ref total, ref passed, ref failed, ref skipped, failures);
                long durationMs = (long)(DateTime.UtcNow - new DateTime(pending.StartedUtcTicks, DateTimeKind.Utc)).TotalMilliseconds;
                WriteResult(TestResultBuilder.Build(pending.Id, pending.Mode, total, passed, failed, skipped, durationMs, failures, DateTime.UtcNow,
                    total == 0 ? NoTestsNote : null));
            }

            private static void Collect(ITestResultAdaptor node, ref int total, ref int passed, ref int failed, ref int skipped, List<TestFailure> failures)
            {
                if (node.HasChildren)
                {
                    foreach (ITestResultAdaptor child in node.Children)
                        Collect(child, ref total, ref passed, ref failed, ref skipped, failures);
                    return;
                }

                total++;
                switch (node.TestStatus)
                {
                    case TestStatus.Passed:
                        passed++;
                        break;
                    case TestStatus.Failed:
                        failed++;
                        failures.Add(new TestFailure { Name = node.FullName, Message = node.Message, Stack = node.StackTrace });
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
        }
    }

    /// <summary>JSON de resultat de la test gate (C8).</summary>
    public static class TestResultBuilder
    {
        public static JObject Build(string id, string mode, int total, int passed, int failed, int skipped, long durationMs,
            IReadOnlyList<TestFailure> failures, DateTime finishedUtc, string note)
        {
            var list = new JArray();
            foreach (TestFailure f in failures)
                list.Add(new JObject { ["name"] = f.Name, ["message"] = f.Message, ["stack"] = f.Stack });
            var json = new JObject
            {
                ["id"] = id,
                ["mode"] = mode,
                ["total"] = total,
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["durationMs"] = durationMs,
                ["failures"] = list,
                ["finishedAt"] = finishedUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)
            };
            if (note != null)
                json["note"] = note;
            return json;
        }
    }
}
