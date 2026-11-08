using System.Collections.Generic;
using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>Sonde generique <c>object.&lt;nom&gt;.x|y|active|exists</c> sur un objet pose par "spawn" ou present dans la scene.</summary>
    [PlaytestProbe("object")]
    public sealed class ObjectProbe : IPlaytestFieldProbe
    {
        public IReadOnlyDictionary<string, object> Read() => new Dictionary<string, object>();

        public bool TryReadField(string field, out object value, out string error)
        {
            value = null;
            error = null;
            int cut = field.LastIndexOf('.');
            if (cut <= 0 || cut == field.Length - 1)
            {
                error = "object probe expects 'object.<name>.x|y|active|exists', got 'object." + field + "'";
                return false;
            }

            string name = field.Substring(0, cut);
            string prop = field.Substring(cut + 1);
            GameObject go = SpawnedObjects.Find(name);
            if (prop == "exists")
            {
                value = go != null;
                return true;
            }

            if (go == null)
            {
                error = "object '" + name + "' not found (spawn it first)";
                return false;
            }

            switch (prop)
            {
                case "x": value = (double)go.transform.position.x; return true;
                case "y": value = (double)go.transform.position.y; return true;
                case "active": value = go.activeSelf; return true;
                default:
                    error = "object probe has no field '" + prop + "' (fields: x, y, active, exists)";
                    return false;
            }
        }
    }
}
