using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>
    /// Pose un prefab : <c>{ "cmd":"spawn", "prefab":"Assets/.../X.prefab", "x":12, "y":3, "name":"fixture1", "active":true }</c>.
    /// "name" (defaut : nom du prefab) sert a le retrouver par la sonde "object". Detruit a la fin du scenario.
    /// </summary>
    [PlaytestCommand("spawn")]
    public sealed class SpawnCommand : IPlaytestCommand
    {
        public void Execute(PlaytestArgs args)
        {
            string path = args.RequireString("prefab");
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            GameObject prefab = null;
#endif
            if (prefab == null)
                throw new PlaytestException("spawn: no prefab at '" + path + "'");

            string name = args.GetString("name", System.IO.Path.GetFileNameWithoutExtension(path));
            var position = new Vector3(args.GetFloat("x"), args.GetFloat("y"), args.GetFloat("z"));
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = name;
            instance.SetActive(args.GetBool("active", true));
            SpawnedObjects.Add(name, instance);
        }
    }
}
