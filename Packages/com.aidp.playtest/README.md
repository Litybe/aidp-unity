# AIDP Playtest Driver (`com.aidp.playtest`)

Un agent joue au jeu par l'Input System, sans captures d'ecran : clavier et manette virtuels, sondes d'etat, commandes de debug, scenarios JSON, assertions, et trois portes pour le harness aidp (compile, playtest, tests).

## Installation

Dans `Packages/manifest.json` du jeu :

```json
"com.aidp.playtest": "file:D:/AIDP/src/aidp-unity/Packages/com.aidp.playtest"
```

Dependances : `com.unity.inputsystem`, `com.unity.test-framework`, `com.unity.nuget.newtonsoft-json` (choisi pour lire les formes mixtes du schema : une assertion est une chaine ou un objet, un step a une cle d'action variable ; `JsonUtility` ne sait pas). Le runtime n'est compile que dans l'editeur et les builds de developpement (`UNITY_EDITOR || DEVELOPMENT_BUILD`) : entoure le code du jeu qui l'utilise du meme `#if`.

Ajouter `.aidp/` au `.gitignore` du jeu.

## Sonde

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
[PlaytestProbe("player")]
public sealed class PlayerProbe : IPlaytestProbe
{
    public IReadOnlyDictionary<string, object> Read() => new Dictionary<string, object>
    {
        ["x"] = transform.position.x, ["grounded"] = true
    };   // null si le sujet n'existe pas
}
#endif
```
Lue par `player.x > 17.5`. Classe avec constructeur sans parametre, valeurs nombre / bool / texte.

## Commande

```csharp
[PlaytestCommand("teleport")]
public sealed class TeleportCommand : IPlaytestCommand
{
    public void Execute(PlaytestArgs args) { /* args.RequireFloat("x") ; throw new PlaytestException("...") */ }
}
```
Appelee par `{ "cmd": "teleport", "x": 12, "y": 3 }` dans `setup` ou entre deux steps.

## Scenario

```json
{ "id": "issue-214-ca2", "scene": "Assets/Scenes/Level_1.unity", "timeoutMs": 10000,
  "setup": [ { "cmd": "teleport", "x": 12, "y": 3 } ],
  "steps": [ { "hold": "rightArrow", "ms": 300 }, { "tap": "x" }, { "wait": 400 }, { "capture": "after-dash" } ],
  "assert": [ "player.x > 17.5", { "expr": "player.grounded == true", "withinMs": 500 }, "player.x ~= 17.5 ± 0.2" ] }
```

- Steps : `hold` (touche ou liste, `ms`), `press` (enfoncee jusqu'a `release` ou la fin), `release`, `tap` (une frame), `wait` (ms), `capture` (nom), `cmd`. Touches : noms de `UnityEngine.InputSystem.Key` en camelCase ; manette `gamepad.buttonSouth`, `gamepad.dpadUp`, `gamepad.leftStick.left`, `gamepad.leftTrigger`.
- Les ms sont du temps reel, independant de `Time.timeScale`. `timeoutMs` couvre tout le scenario.
- Assertions : `sonde.champ op valeur`, op `== != < <= > >= ~=` ; `withinMs` = vraie au moins une fois dans ce delai, compte a partir de la fin des steps.

## Portes pour le harness (fichiers sous `<projet>/.aidp/`)

| Porte | Appel MCP (`Unity_RunCommand`) | Depot de fichier | Resultat |
|---|---|---|---|
| Compile gate (C1) | `Aidp.Playtest.Editor.CompileGate.Request("<id>")` | `compile/requests/<id>.json` | `compile/results/<id>.json` |
| Playtest (C2) | `Aidp.Playtest.Editor.PlaytestBridge.Run("<id>")` | `playtest/requests/<id>.json` (schema scenario) | `playtest/results/<id>.json`, captures, `gif` si `D:\DevTools\ffmpeg\bin\ffmpeg.exe` (ou `AIDP_FFMPEG`) existe |
| Test gate (C8) | `Aidp.Playtest.Editor.TestGate.Request("<id>", "EditMode"\|"PlayMode")` | `test-results/requests/<id>-<mode>.json` | `test-results/<id>-<mode>.json` |

Les fichiers de demande sont pris par un `FileSystemWatcher` ; le traitement est poste sur le thread principal. Aucun abonnement permanent a `EditorApplication.update` : cout CPU nul au repos. L'etat survit au domain reload (`SessionState`). Un scenario est refuse si l'editeur est deja en Play Mode.

Regression : `public class ScenarioPlayModeTests : PlaytestScenarioTestBase { }` dans un asmdef de tests PlayMode (references `Aidp.Playtest.Runtime`, `Aidp.Playtest.TestSupport`) : un test par fichier de `Assets/Tests/Playtest/Scenarios/`.

Repro : menu `Tools/AIDP/Playtest/Start repro recording` en Play Mode, puis `Stop ... and save` : scenario JSON dans `.aidp/playtest/recordings/`.

## Tests du package

EditMode (parseur, evaluateur, registres, formats de resultats) et PlayMode (peripherique virtuel vu par une `InputAction`, scenarios) : Test Runner, ou batchmode `-runTests` sur un projet hote qui reference le package en `file:` et declare `"testables": ["com.aidp.playtest"]`.
