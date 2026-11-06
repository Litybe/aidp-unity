using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aidp.Playtest
{
    /// <summary>Arguments d'une commande : les proprietes de l'objet JSON hors "cmd" (nombres en double, textes, booleens).</summary>
    public sealed class PlaytestArgs
    {
        private readonly IReadOnlyDictionary<string, object> _values;

        public PlaytestArgs(IReadOnlyDictionary<string, object> values)
        {
            _values = values ?? new Dictionary<string, object>();
        }

        public IEnumerable<string> Names => _values.Keys;

        public bool Has(string name) => _values.ContainsKey(name);

        public string GetString(string name, string fallback = null)
        {
            return _values.TryGetValue(name, out object v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : fallback;
        }

        public string RequireString(string name)
        {
            string v = GetString(name);
            if (string.IsNullOrEmpty(v))
                throw new PlaytestException("missing argument '" + name + "'");
            return v;
        }

        public float GetFloat(string name, float fallback = 0f)
        {
            return _values.TryGetValue(name, out object v) && TryToDouble(v, out double d) ? (float)d : fallback;
        }

        public float RequireFloat(string name)
        {
            if (_values.TryGetValue(name, out object v) && TryToDouble(v, out double d))
                return (float)d;
            throw new PlaytestException("missing or non numeric argument '" + name + "'");
        }

        public int GetInt(string name, int fallback = 0)
        {
            return _values.TryGetValue(name, out object v) && TryToDouble(v, out double d) ? (int)Math.Round(d) : fallback;
        }

        public bool GetBool(string name, bool fallback = false)
        {
            if (!_values.TryGetValue(name, out object v))
                return fallback;
            if (v is bool b)
                return b;
            return bool.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out bool parsed) ? parsed : fallback;
        }

        private static bool TryToDouble(object v, out double d)
        {
            switch (v)
            {
                case double dd: d = dd; return true;
                case float f: d = f; return true;
                case int i: d = i; return true;
                case long l: d = l; return true;
                case string s: return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                default: d = 0; return false;
            }
        }
    }
}
