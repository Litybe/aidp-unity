using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;

namespace Aidp.Playtest
{
    /// <summary>
    /// Enregistre une partie jouee (clavier et manette reels, via <see cref="InputEventTrace"/>) et la convertit en scenario JSON :
    /// suite de press / release / wait. A completer ensuite avec un setup et des assertions.
    /// </summary>
    public sealed class ReproRecorder : IDisposable
    {
        private const float PressThreshold = 0.5f;
        private InputEventTrace _keyboardTrace;
        private InputEventTrace _gamepadTrace;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public bool IsRecording => _keyboardTrace != null;

        public void Start()
        {
            if (IsRecording)
                return;
            _keyboard = Keyboard.current;
            _gamepad = Gamepad.current;
            if (_keyboard != null)
            {
                _keyboardTrace = new InputEventTrace(_keyboard, 1024 * 1024, true);
                _keyboardTrace.Enable();
            }
            else
            {
                throw new PlaytestException("no keyboard device to record");
            }

            if (_gamepad != null)
            {
                _gamepadTrace = new InputEventTrace(_gamepad, 256 * 1024, true);
                _gamepadTrace.Enable();
            }
        }

        /// <summary>Arrete l'enregistrement et rend le scenario JSON.</summary>
        public string Stop(string id, string scene)
        {
            if (!IsRecording)
                throw new PlaytestException("recorder is not running");
            _keyboardTrace.Disable();
            _gamepadTrace?.Disable();

            var events = new List<(double time, string control, bool down)>();
            CollectKeyboard(events);
            CollectGamepad(events);
            events.Sort((a, b) => a.time.CompareTo(b.time));
            string json = ToScenarioJson(id, scene, events);
            Dispose();
            return json;
        }

        public void Dispose()
        {
            _keyboardTrace?.Dispose();
            _gamepadTrace?.Dispose();
            _keyboardTrace = null;
            _gamepadTrace = null;
        }

        private void CollectKeyboard(List<(double, string, bool)> events)
        {
            var previous = new Dictionary<Key, bool>();
            foreach (InputEventPtr e in _keyboardTrace)
            {
                if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>())
                    continue;
                foreach (KeyControl control in _keyboard.allKeys)
                {
                    if (control.keyCode == Key.None || !control.ReadValueFromEvent(e, out float value))
                        continue;
                    bool down = value >= PressThreshold;
                    previous.TryGetValue(control.keyCode, out bool was);
                    if (down == was)
                        continue;
                    previous[control.keyCode] = down;
                    events.Add((e.time, KeyNames.ToName(control.keyCode), down));
                }
            }
        }

        private void CollectGamepad(List<(double, string, bool)> events)
        {
            if (_gamepadTrace == null)
                return;
            var previous = new Dictionary<GamepadButton, bool>();
            foreach (InputEventPtr e in _gamepadTrace)
            {
                if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>())
                    continue;
                foreach (GamepadButton button in Enum.GetValues(typeof(GamepadButton)))
                {
                    ButtonControl control = _gamepad[button];
                    if (control == null || !control.ReadValueFromEvent(e, out float value))
                        continue;
                    bool down = value >= PressThreshold;
                    previous.TryGetValue(button, out bool was);
                    if (down == was)
                        continue;
                    previous[button] = down;
                    string name = button.ToString();
                    events.Add((e.time, PlaytestConstants.GamepadPrefix + char.ToLowerInvariant(name[0]) + name.Substring(1), down));
                }
            }
        }

        /// <summary>Evenements (temps en secondes, controle, appuye) vers un scenario ; les trous deviennent des "wait".</summary>
        public static string ToScenarioJson(string id, string scene, IReadOnlyList<(double time, string control, bool down)> events)
        {
            var steps = new JArray();
            double last = events.Count > 0 ? events[0].time : 0;
            var held = new HashSet<string>();
            foreach ((double time, string control, bool down) in events)
            {
                int gap = (int)Math.Round((time - last) * 1000.0, MidpointRounding.AwayFromZero);
                if (gap > 0)
                    steps.Add(new JObject { [PlaytestConstants.StepWait] = gap });
                last = time;
                steps.Add(new JObject { [down ? PlaytestConstants.StepPress : PlaytestConstants.StepRelease] = control });
                if (down)
                    held.Add(control);
                else
                    held.Remove(control);
            }

            foreach (string control in held)
                steps.Add(new JObject { [PlaytestConstants.StepRelease] = control });

            var scenario = new JObject
            {
                ["id"] = id,
                ["scene"] = scene ?? string.Empty,
                ["timeoutMs"] = Math.Max(PlaytestConstants.DefaultTimeoutMs, (int)((last - (events.Count > 0 ? events[0].time : 0)) * 1000.0) + 5000),
                ["setup"] = new JArray(),
                ["steps"] = steps,
                ["assert"] = new JArray()
            };
            return scenario.ToString(Formatting.Indented);
        }

        public static string NewId() => "repro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    }
}
