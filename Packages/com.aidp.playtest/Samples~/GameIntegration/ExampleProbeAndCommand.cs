#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Aidp.Playtest;
using UnityEngine;

// A copier dans Assets/Scripts/Playtest/ du jeu et adapter.
[PlaytestProbe("player")]
public sealed class ExamplePlayerProbe : IPlaytestProbe
{
    public IReadOnlyDictionary<string, object> Read()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return null;
        Vector3 p = player.transform.position;
        return new Dictionary<string, object> { ["x"] = p.x, ["y"] = p.y };
    }
}

[PlaytestCommand("teleport")]
public sealed class ExampleTeleportCommand : IPlaytestCommand
{
    public void Execute(PlaytestArgs args)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            throw new PlaytestException("no object tagged Player");
        player.transform.position = new Vector3(args.RequireFloat("x"), args.RequireFloat("y"), player.transform.position.z);
    }
}
#endif
