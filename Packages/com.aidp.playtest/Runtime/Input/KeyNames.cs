using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Aidp.Playtest
{
    /// <summary>Noms de touches des scenarios : noms de <see cref="Key"/> en camelCase, ou "gamepad.&lt;controle&gt;".</summary>
    public static class KeyNames
    {
        private const string LeftStick = "leftStick.";
        private const string RightStick = "rightStick.";

        public static bool IsGamepad(string name) => name.StartsWith(PlaytestConstants.GamepadPrefix, StringComparison.OrdinalIgnoreCase);

        public static bool IsValid(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (IsGamepad(name))
                return TryParseGamepad(name, out _, out _);
            return TryParseKey(name, out _);
        }

        public static bool TryParseKey(string name, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(name) || !Enum.TryParse(name, true, out Key parsed))
                return false;
            if (parsed == Key.None || !Enum.IsDefined(typeof(Key), parsed))
                return false;
            key = parsed;
            return true;
        }

        public static string ToName(Key key)
        {
            string text = key.ToString();
            return char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// "gamepad.buttonSouth" (ou north, west, east, dpadUp...), "gamepad.leftStick.left" (up, down, left, right),
        /// "gamepad.leftTrigger" / "gamepad.rightTrigger". Un bouton = <see cref="GamepadButton"/> ; une direction de stick = vecteur.
        /// </summary>
        public static bool TryParseGamepad(string name, out GamepadButton button, out UnityEngine.Vector2 stick)
        {
            button = default;
            stick = UnityEngine.Vector2.zero;
            if (!IsGamepad(name))
                return false;
            string control = name.Substring(PlaytestConstants.GamepadPrefix.Length);
            if (control.StartsWith(LeftStick, StringComparison.OrdinalIgnoreCase) || control.StartsWith(RightStick, StringComparison.OrdinalIgnoreCase))
            {
                string dir = control.Substring(control.IndexOf('.') + 1).ToLowerInvariant();
                switch (dir)
                {
                    case "left": stick = UnityEngine.Vector2.left; return true;
                    case "right": stick = UnityEngine.Vector2.right; return true;
                    case "up": stick = UnityEngine.Vector2.up; return true;
                    case "down": stick = UnityEngine.Vector2.down; return true;
                    default: return false;
                }
            }

            switch (control.ToLowerInvariant())
            {
                case "buttonsouth": button = GamepadButton.South; return true;
                case "buttonnorth": button = GamepadButton.North; return true;
                case "buttonwest": button = GamepadButton.West; return true;
                case "buttoneast": button = GamepadButton.East; return true;
            }

            return Enum.TryParse(control, true, out button) && Enum.IsDefined(typeof(GamepadButton), button);
        }
    }
}
