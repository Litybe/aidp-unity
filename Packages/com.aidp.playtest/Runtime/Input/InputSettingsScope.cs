using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aidp.Playtest
{
    /// <summary>
    /// Le temps d'un scenario : les peripheriques virtuels continuent de fonctionner quand l'editeur n'a pas le focus,
    /// et le jeu tourne en arriere-plan. Les reglages sont poses sur une copie ; l'asset InputSettings du projet n'est jamais modifie.
    /// </summary>
    public sealed class InputSettingsScope : IDisposable
    {
        private InputSettings _original;
        private InputSettings _temporary;
        private bool _runInBackground;
        private bool _applied;

        public void Apply()
        {
            if (_applied)
                return;
            _applied = true;
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;

            _original = InputSystem.settings;
            // Les reglages courants peuvent avoir ete detruits par un reset du fixture de tests : on repart alors d'un jeu neuf.
            _temporary = _original != null ? UnityEngine.Object.Instantiate(_original) : ScriptableObject.CreateInstance<InputSettings>();
            _temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = _temporary;
        }

        public void Dispose()
        {
            if (!_applied)
                return;
            _applied = false;
            // Le fixture de tests ou un reset externe peut avoir remplace ou detruit les reglages : on ne restaure que si ils sont encore les notres.
            if (_original != null && InputSystem.settings == _temporary)
                InputSystem.settings = _original;
            if (_temporary != null)
                UnityEngine.Object.Destroy(_temporary);
            Application.runInBackground = _runInBackground;
        }
    }
}
