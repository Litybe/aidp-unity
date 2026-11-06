using Newtonsoft.Json;
using UnityEditor;

namespace Aidp.Playtest.Editor
{
    /// <summary>Etat JSON conserve dans SessionState : survit aux domain reloads, disparait a la fermeture de l'editeur.</summary>
    internal static class EditorSessionJson
    {
        public static T Load<T>(string key) where T : class
        {
            string json = SessionState.GetString(key, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<T>(json);
        }

        public static void Save<T>(string key, T value) where T : class
        {
            SessionState.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static void Clear(string key) => SessionState.EraseString(key);
    }
}
