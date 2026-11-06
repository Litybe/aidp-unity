using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Aidp.Playtest
{
    /// <summary>
    /// Clavier et manette virtuels : de vrais peripheriques de l'Input System ajoutes par code. Le jeu les voit comme
    /// des touches reelles. Chaque changement est un evenement d'etat, traite a la frame suivante.
    /// </summary>
    public sealed class VirtualInput : IDisposable
    {
        private readonly Keyboard _keyboard;
        private readonly HashSet<Key> _keys = new HashSet<Key>();
        private Gamepad _gamepad;
        private GamepadState _padState;
        private readonly HashSet<string> _padHeld = new HashSet<string>();

        public VirtualInput()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>(PlaytestConstants.VirtualKeyboardName);
        }

        public Keyboard Keyboard => _keyboard;
        public Gamepad Gamepad => _gamepad;

        /// <summary>Touches actuellement tenues (noms camelCase), pour les logs et les tests.</summary>
        public IEnumerable<string> HeldKeys
        {
            get
            {
                foreach (Key key in _keys)
                    yield return KeyNames.ToName(key);
                foreach (string pad in _padHeld)
                    yield return pad;
            }
        }

        public void Press(string name) => Set(name, true);

        public void Release(string name) => Set(name, false);

        public void ReleaseAll()
        {
            if (_keys.Count > 0)
            {
                _keys.Clear();
                PushKeyboard();
            }

            if (_gamepad != null && _padHeld.Count > 0)
            {
                _padHeld.Clear();
                _padState = default;
                InputSystem.QueueStateEvent(_gamepad, _padState);
            }
        }

        private void Set(string name, bool down)
        {
            if (KeyNames.IsGamepad(name))
            {
                SetGamepad(name, down);
                return;
            }

            if (!KeyNames.TryParseKey(name, out Key key))
                throw new PlaytestException("unknown key '" + name + "'");
            bool changed = down ? _keys.Add(key) : _keys.Remove(key);
            if (changed)
                PushKeyboard();
        }

        private void PushKeyboard()
        {
            var keys = new Key[_keys.Count];
            _keys.CopyTo(keys);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        }

        private void SetGamepad(string name, bool down)
        {
            if (!KeyNames.TryParseGamepad(name, out GamepadButton button, out Vector2 stick))
                throw new PlaytestException("unknown gamepad control '" + name + "'");
            if (_gamepad == null)
                _gamepad = InputSystem.AddDevice<Gamepad>(PlaytestConstants.VirtualGamepadName);

            if (down)
                _padHeld.Add(name);
            else
                _padHeld.Remove(name);

            if (stick != Vector2.zero)
            {
                bool left = name.IndexOf("leftStick", StringComparison.OrdinalIgnoreCase) >= 0;
                Vector2 value = down ? stick : Vector2.zero;
                if (left)
                    _padState.leftStick = value;
                else
                    _padState.rightStick = value;
            }
            else
            {
                _padState = _padState.WithButton(button, down);
                if (button == GamepadButton.LeftTrigger)
                    _padState.leftTrigger = down ? 1f : 0f;
                else if (button == GamepadButton.RightTrigger)
                    _padState.rightTrigger = down ? 1f : 0f;
            }

            InputSystem.QueueStateEvent(_gamepad, _padState);
        }

        public void Dispose()
        {
            ReleaseAll();
            if (_keyboard != null && _keyboard.added)
                InputSystem.RemoveDevice(_keyboard);
            if (_gamepad != null && _gamepad.added)
                InputSystem.RemoveDevice(_gamepad);
        }
    }
}
