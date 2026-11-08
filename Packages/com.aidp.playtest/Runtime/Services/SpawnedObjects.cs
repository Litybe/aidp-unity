using System.Collections.Generic;
using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>Objets poses par les scenarios (commande "spawn"), indexes par nom, detruits a la fin du scenario.</summary>
    public static class SpawnedObjects
    {
        private static readonly Dictionary<string, GameObject> Objects = new Dictionary<string, GameObject>();

        public static int Count => Objects.Count;

        public static void Add(string name, GameObject instance)
        {
            if (Objects.TryGetValue(name, out GameObject existing) && existing != null)
                throw new PlaytestException("spawn: an object named '" + name + "' already exists");
            Objects[name] = instance;
        }

        /// <summary>L'objet pose sous ce nom, sinon un objet actif de la scene du meme nom, sinon null.</summary>
        public static GameObject Find(string name)
        {
            if (Objects.TryGetValue(name, out GameObject spawned) && spawned != null)
                return spawned;
            return GameObject.Find(name);
        }

        public static void DestroyAll()
        {
            foreach (GameObject go in Objects.Values)
            {
                if (go == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
            }

            Objects.Clear();
        }
    }
}
