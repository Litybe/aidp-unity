using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aidp.Playtest
{
    /// <summary>
    /// Joue un scenario : scene, setup, steps (entrees virtuelles), assertions. S'utilise comme une coroutine
    /// (<c>yield return runner.Run(scenario, result)</c>) depuis un MonoBehaviour ou un [UnityTest].
    /// Les durees (ms) sont en temps reel (horloge murale), indifferentes a Time.timeScale.
    /// </summary>
    public sealed class PlaytestRunner
    {
        private readonly PlaytestRegistry _registry;
        private readonly PlaytestOptions _options;

        public PlaytestRunner(PlaytestRegistry registry, PlaytestOptions options)
        {
            _registry = registry;
            _options = options;
        }

        /// <summary>Etat d'une execution.</summary>
        private sealed class RunState
        {
            public Scenario Scenario;
            public PlaytestResult Result;
            public Stopwatch Clock = Stopwatch.StartNew();
            public VirtualInput Input;
            public bool Aborted;
        }

        /// <summary>
        /// Coroutine complete. N'echoue jamais : toute exception finit dans <see cref="PlaytestResult.Error"/>.
        /// Les coroutines imbriquees sont pilotees ici (pile) pour que leurs exceptions soient attrapees.
        /// </summary>
        public IEnumerator Run(Scenario scenario, PlaytestResult result)
        {
            var state = new RunState { Scenario = scenario, Result = result };
            using (var settings = new InputSettingsScope())
            {
                float timeScale = Time.timeScale;
                var stack = new Stack<IEnumerator>();
                stack.Push(Body(state, settings));
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    bool more;
                    try
                    {
                        more = top.MoveNext();
                    }
                    catch (Exception e)
                    {
                        result.Error = e is PlaytestException ? e.Message : e.GetType().Name + ": " + e.Message;
                        break;
                    }

                    if (!more)
                    {
                        stack.Pop();
                        continue;
                    }

                    if (top.Current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }

                    yield return top.Current;
                }

                if (state.Input != null)
                    state.Input.Dispose();
                Time.timeScale = timeScale;
            }

            result.DurationMs = state.Clock.ElapsedMilliseconds;
            result.Complete();
        }

        private IEnumerator Body(RunState s, InputSettingsScope settings)
        {
            settings.Apply();
            foreach (string problem in _registry.Problems)
                s.Result.Log.Add("registry: " + problem);

            if (!string.IsNullOrEmpty(s.Scenario.Scene))
            {
                yield return LoadScene(s);
                if (s.Aborted)
                    yield break;
            }

            for (int i = 0; i < PlaytestConstants.SceneSettleFrames; i++)
                yield return null;

            s.Input = new VirtualInput();
            foreach (ScenarioCommand command in s.Scenario.Setup)
                RunCommand(s, command);

            foreach (ScenarioStep step in s.Scenario.Steps)
            {
                yield return RunStep(s, step);
                if (s.Aborted)
                    yield break;
            }

            yield return EvaluateAssertions(s);
        }

        private static IEnumerator LoadScene(RunState s)
        {
            string path = s.Scenario.Scene;
            s.Result.Log.Add(Stamp(s) + "load scene " + path);
#if UNITY_EDITOR
            AsyncOperation op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            AsyncOperation op = SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(path), LoadSceneMode.Single);
#endif
            if (op == null)
                throw new PlaytestException("cannot load scene '" + path + "'");
            while (!op.isDone)
            {
                if (TimedOut(s))
                    yield break;
                yield return null;
            }
        }

        private void RunCommand(RunState s, ScenarioCommand command)
        {
            s.Result.Log.Add(Stamp(s) + "cmd " + command.Name);
            _registry.Execute(command);
        }

        private IEnumerator RunStep(RunState s, ScenarioStep step)
        {
            switch (step.Kind)
            {
                case StepKind.Hold:
                    s.Result.Log.Add(Stamp(s) + "hold " + string.Join("+", step.Keys) + " " + step.Ms + " ms");
                    foreach (string key in step.Keys)
                        s.Input.Press(key);
                    yield return WaitMs(s, step.Ms, true);
                    foreach (string key in step.Keys)
                        s.Input.Release(key);
                    break;
                case StepKind.Press:
                    s.Result.Log.Add(Stamp(s) + "press " + string.Join("+", step.Keys));
                    foreach (string key in step.Keys)
                        s.Input.Press(key);
                    yield return null;
                    break;
                case StepKind.Release:
                    s.Result.Log.Add(Stamp(s) + "release " + string.Join("+", step.Keys));
                    foreach (string key in step.Keys)
                        s.Input.Release(key);
                    yield return null;
                    break;
                case StepKind.Tap:
                    s.Result.Log.Add(Stamp(s) + "tap " + string.Join("+", step.Keys));
                    foreach (string key in step.Keys)
                        s.Input.Press(key);
                    yield return null;
                    foreach (string key in step.Keys)
                        s.Input.Release(key);
                    yield return null;
                    break;
                case StepKind.Wait:
                    s.Result.Log.Add(Stamp(s) + "wait " + step.Ms + " ms");
                    yield return WaitMs(s, step.Ms, false);
                    break;
                case StepKind.Capture:
                    yield return Capture(s, step.Name);
                    break;
                default:
                    RunCommand(s, step.Command);
                    break;
            }
        }

        private static IEnumerator WaitMs(RunState s, int ms, bool atLeastOneFrame)
        {
            long end = s.Clock.ElapsedMilliseconds + ms;
            if (atLeastOneFrame)
                yield return null;
            while (s.Clock.ElapsedMilliseconds < end)
            {
                if (TimedOut(s))
                    yield break;
                yield return null;
            }
        }

        private IEnumerator Capture(RunState s, string name)
        {
            string relative = PlaytestPaths.RelativeCaptureDir(s.Scenario.Id) + "/" + name + PlaytestConstants.PngExtension;
            string absolute = Path.Combine(_options.ProjectRoot, relative);
            bool ok = false;
            yield return ScreenshotService.CaptureTo(absolute, done => ok = done);
            if (ok)
            {
                s.Result.Captures.Add(relative);
                s.Result.Log.Add(Stamp(s) + "capture " + name);
            }
            else
            {
                s.Result.Log.Add(Stamp(s) + "capture " + name + " skipped (no rendering available)");
            }
        }

        private sealed class PendingAssertion
        {
            public ScenarioAssertion Assertion;
            public bool Done;
            public AssertionOutcome Last;
        }

        private IEnumerator EvaluateAssertions(RunState s)
        {
            var pending = new List<PendingAssertion>();
            foreach (ScenarioAssertion a in s.Scenario.Assertions)
                pending.Add(new PendingAssertion { Assertion = a });

            long start = s.Clock.ElapsedMilliseconds;
            while (true)
            {
                bool waiting = false;
                long now = s.Clock.ElapsedMilliseconds;
                foreach (PendingAssertion p in pending)
                {
                    if (p.Done)
                        continue;
                    p.Last = AssertionEvaluator.Evaluate(p.Assertion.Expression, _registry);
                    if (p.Last.Passed || now - start >= p.Assertion.WithinMs)
                        p.Done = true;
                    else
                        waiting = true;
                }

                if (!waiting)
                    break;
                if (TimedOut(s))
                    break;
                yield return null;
            }

            foreach (PendingAssertion p in pending)
            {
                AssertionOutcome o = p.Last ?? new AssertionOutcome(false, null, p.Assertion.Expression.Text + " : not evaluated");
                s.Result.Asserts.Add(new AssertResult(p.Assertion.Expression.Text, o.Passed, o.Actual, o.Message));
            }
        }

        private static bool TimedOut(RunState s)
        {
            if (s.Aborted)
                return true;
            if (s.Clock.ElapsedMilliseconds <= s.Scenario.TimeoutMs)
                return false;
            s.Result.Error = "timeout after " + s.Scenario.TimeoutMs + " ms";
            s.Aborted = true;
            return true;
        }

        private static string Stamp(RunState s) => "[" + s.Clock.ElapsedMilliseconds + " ms] ";
    }
}
