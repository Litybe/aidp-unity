using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aidp.Playtest
{
    /// <summary>Lit un scenario JSON (schema C2) et le valide : une erreur de forme se voit avant de lancer le jeu.</summary>
    public static class ScenarioParser
    {
        private static readonly string[] ActionKeys =
        {
            PlaytestConstants.StepHold, PlaytestConstants.StepPress, PlaytestConstants.StepRelease, PlaytestConstants.StepTap,
            PlaytestConstants.StepWait, PlaytestConstants.StepCapture, PlaytestConstants.StepCommand
        };

        /// <param name="json">Contenu du fichier.</param>
        /// <param name="defaultId">Id utilise si le JSON n'en porte pas (nom du fichier de demande).</param>
        public static Scenario Parse(string json, string defaultId = null)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new ScenarioParseException("invalid JSON: " + e.Message);
            }

            string id = (string)root["id"] ?? defaultId;
            if (!PlaytestPaths.IsValidId(id))
                throw new ScenarioParseException("missing or invalid 'id' (letters, digits, '.', '-', '_')");

            string scene = (string)root["scene"] ?? string.Empty;
            int timeoutMs = ReadInt(root["timeoutMs"], PlaytestConstants.DefaultTimeoutMs, "timeoutMs");
            if (timeoutMs <= 0)
                throw new ScenarioParseException("'timeoutMs' must be > 0");

            var setup = new List<ScenarioCommand>();
            foreach ((JToken token, int index) in Items(root["setup"], "setup"))
                setup.Add(ParseCommand(token, "setup[" + index + "]"));

            var steps = new List<ScenarioStep>();
            foreach ((JToken token, int index) in Items(root["steps"], "steps"))
                steps.Add(ParseStep(token, "steps[" + index + "]"));

            var assertions = new List<ScenarioAssertion>();
            foreach ((JToken token, int index) in Items(root["assert"], "assert"))
                assertions.Add(ParseAssertion(token, "assert[" + index + "]"));

            return new Scenario(id, scene, timeoutMs, setup, steps, assertions);
        }

        private static IEnumerable<(JToken, int)> Items(JToken token, string name)
        {
            if (token == null || token.Type == JTokenType.Null)
                yield break;
            if (token.Type != JTokenType.Array)
                throw new ScenarioParseException("'" + name + "' must be an array");
            int i = 0;
            foreach (JToken item in token)
                yield return (item, i++);
        }

        private static ScenarioCommand ParseCommand(JToken token, string where)
        {
            if (!(token is JObject obj) || obj["cmd"] == null || obj["cmd"].Type != JTokenType.String)
                throw new ScenarioParseException(where + ": expected an object with a 'cmd' name");
            var values = new Dictionary<string, object>();
            foreach (JProperty p in obj.Properties())
            {
                if (p.Name == PlaytestConstants.StepCommand)
                    continue;
                values[p.Name] = ToValue(p.Value);
            }

            return new ScenarioCommand((string)obj["cmd"], new PlaytestArgs(values));
        }

        private static object ToValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float: return (double)token;
                case JTokenType.Boolean: return (bool)token;
                case JTokenType.String: return (string)token;
                default: return token.ToString(Formatting.None);
            }
        }

        private static ScenarioStep ParseStep(JToken token, string where)
        {
            if (!(token is JObject obj))
                throw new ScenarioParseException(where + ": expected an object");

            string action = null;
            foreach (string key in ActionKeys)
            {
                if (obj[key] == null)
                    continue;
                if (action != null)
                    throw new ScenarioParseException(where + ": several actions ('" + action + "', '" + key + "')");
                action = key;
            }

            if (action == null)
                throw new ScenarioParseException(where + ": no action (hold, press, release, tap, wait, capture, cmd)");

            switch (action)
            {
                case PlaytestConstants.StepHold:
                {
                    IReadOnlyList<string> keys = ReadKeys(obj[action], where);
                    if (obj[PlaytestConstants.KeyMs] == null)
                        throw new ScenarioParseException(where + ": 'hold' needs 'ms'");
                    int ms = ReadInt(obj[PlaytestConstants.KeyMs], 0, where + ".ms");
                    if (ms < 0)
                        throw new ScenarioParseException(where + ": 'ms' must be >= 0");
                    return new ScenarioStep(StepKind.Hold, keys, ms, null, null);
                }
                case PlaytestConstants.StepPress:
                    return new ScenarioStep(StepKind.Press, ReadKeys(obj[action], where), 0, null, null);
                case PlaytestConstants.StepRelease:
                    return new ScenarioStep(StepKind.Release, ReadKeys(obj[action], where), 0, null, null);
                case PlaytestConstants.StepTap:
                    return new ScenarioStep(StepKind.Tap, ReadKeys(obj[action], where), 0, null, null);
                case PlaytestConstants.StepWait:
                {
                    int ms = ReadInt(obj[action], -1, where + ".wait");
                    if (ms < 0)
                        throw new ScenarioParseException(where + ": 'wait' needs a number of ms >= 0");
                    return new ScenarioStep(StepKind.Wait, null, ms, null, null);
                }
                case PlaytestConstants.StepCapture:
                {
                    string name = (string)obj[action];
                    if (!PlaytestPaths.IsValidId(name))
                        throw new ScenarioParseException(where + ": invalid capture name");
                    return new ScenarioStep(StepKind.Capture, null, 0, name, null);
                }
                default:
                    return new ScenarioStep(StepKind.Command, null, 0, null, ParseCommand(obj, where));
            }
        }

        private static IReadOnlyList<string> ReadKeys(JToken token, string where)
        {
            var keys = new List<string>();
            if (token.Type == JTokenType.String)
                keys.Add((string)token);
            else if (token.Type == JTokenType.Array)
                foreach (JToken t in token)
                    keys.Add(t.Type == JTokenType.String ? (string)t : null);
            else
                throw new ScenarioParseException(where + ": key must be a string or an array of strings");

            if (keys.Count == 0)
                throw new ScenarioParseException(where + ": empty key list");
            foreach (string key in keys)
            {
                if (key == null || !KeyNames.IsValid(key))
                    throw new ScenarioParseException(where + ": unknown key '" + key + "' (Input System Key name in camelCase, or gamepad.<control>)");
            }

            return keys;
        }

        private static ScenarioAssertion ParseAssertion(JToken token, string where)
        {
            string text;
            int withinMs = 0;
            if (token.Type == JTokenType.String)
            {
                text = (string)token;
            }
            else if (token is JObject obj && obj[PlaytestConstants.KeyExpr] != null)
            {
                text = (string)obj[PlaytestConstants.KeyExpr];
                withinMs = ReadInt(obj[PlaytestConstants.KeyWithinMs], 0, where + ".withinMs");
                if (withinMs < 0)
                    throw new ScenarioParseException(where + ": 'withinMs' must be >= 0");
            }
            else
            {
                throw new ScenarioParseException(where + ": expected a string or {\"expr\": ..., \"withinMs\": ...}");
            }

            try
            {
                return new ScenarioAssertion(AssertionParser.Parse(text), withinMs);
            }
            catch (ScenarioParseException e)
            {
                throw new ScenarioParseException(where + ": " + e.Message);
            }
        }

        private static int ReadInt(JToken token, int fallback, string name)
        {
            if (token == null || token.Type == JTokenType.Null)
                return fallback;
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                throw new ScenarioParseException("'" + name + "' must be a number");
            return (int)System.Math.Round((double)token);
        }
    }
}
