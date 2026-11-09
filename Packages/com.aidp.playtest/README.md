# AIDP Playtest Driver (`com.aidp.playtest`)

Un agent joue au jeu par l'Input System, sans captures d'ecran : clavier et manette virtuels, sondes d'etat, commandes de debug, scenarios JSON, assertions, et trois portes pour le harness aidp (compile, playtest, tests).

> **Usage : manuel, dans l'editeur ouvert.** Depuis la decision du 2026-11-09, les pipelines aidp de la
> route `code` ne jouent plus le jeu : ils verifient en headless (Unity en batchmode dans le worktree du
> ticket, compile gate puis tests EditMode, voir [Batchmode](#batchmode-ce-que-les-pipelines-aidp-lancent)) et le
> test PlayMode est fait a la main, a partir de la section « Test manuel en PlayMode » de la PR. Ce package
> reste ton outil dans l'editeur : rejouer un scenario, enregistrer une repro, lancer les portes par le
> pont fichier. Aucun stage ne depose plus de requete dans `.aidp/`.

## Installation

Dans `Packages/manifest.json` du jeu, un chemin relatif au dossier `Packages/` (ou une URL git), jamais un
chemin absolu propre a une machine :

```json
"com.aidp.playtest": "file:../../aidp-unity/Packages/com.aidp.playtest"
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
{ "id": "issue-214-ca2", "timeoutMs": 10000,
  "setup": [ { "cmd": "spawn", "prefab": "Assets/Prefab/Lever.prefab", "x": 12, "y": 3, "name": "fixture1" }, { "cmd": "teleport", "x": 12, "y": 1 } ],
  "steps": [ { "hold": "rightArrow", "ms": 300 }, { "tap": "x" }, { "wait": 400 }, { "capture": "after-dash" } ],
  "assert": [ "player.x > 17.5", { "expr": "player.grounded == true", "withinMs": 500 }, "player.x ~= 17.5 ± 0.2" ] }
```

- Steps : `hold` (touche ou liste, `ms`), `press` (enfoncee jusqu'a `release` ou la fin), `release`, `tap` (une frame), `wait` (ms), `capture` (nom), `cmd`. Touches : noms de `UnityEngine.InputSystem.Key` en camelCase ; manette `gamepad.buttonSouth`, `gamepad.dpadUp`, `gamepad.leftStick.left`, `gamepad.leftTrigger`.
- Les ms sont du temps reel, independant de `Time.timeScale`. `timeoutMs` couvre le scenario a partir de la fin du chargement de la scene (chargement : 120 s max, a part).
- Scene : champ `scene` (chemin `Assets/...unity`) ; **sans lui, le scenario joue dans `Assets/Tests/Playtest/Scenes/PlaytestArena.unity`** (`PlaytestOptions.DefaultScene`) si ce fichier existe, sinon dans la scene courante. Le jeu fournit l'arene (GameManager, joueur, camera, sol plat) : un scenario ne depend pas du contenu d'un niveau.
- `spawn` (commande du package) : `{ "cmd":"spawn", "prefab":"Assets/.../X.prefab", "x":12, "y":3, "name":"fixture1", "active":true }` pose une instance (`name` defaut = nom du prefab, `z` et `active` optionnels ; prefab charge par `AssetDatabase`, donc Editor seulement). Les objets poses sont detruits a la fin du scenario.
- Sonde `object` (package) : `object.<nom>.x`, `.y`, `.active`, `.exists` sur un objet pose par `spawn` ou present dans la scene (`GameObject.Find`) ; `exists` est faux sans erreur quand l'objet manque.
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

## Batchmode : ce que les pipelines aidp lancent

Sans editeur ouvert, dans le worktree du ticket (`<projet>` ci-dessous), deux appels au plus par verification,
jamais `-testPlatform PlayMode` :

```
Unity -batchmode -nographics -quit -projectPath "<projet>" -logFile "<stage>/unity-compile-<id>.log"
Unity -batchmode -nographics -projectPath "<projet>" -runTests -testPlatform EditMode -testResults "<stage>/editmode-<id>.xml" -logFile "<stage>/unity-editmode-<id>.log"
```

Le premier compile (verdict : code de sortie 0 et aucun `error CS` dans le log), cree les `.meta` des nouveaux
assets et quitte. Le second joue la suite EditMode (codes 0 tout passe, 2 echecs, 3 erreur d'execution) et ecrit
un XML NUnit. Ce package n'intervient pas dans ces appels ; sa seule exigence est de compiler en batchmode (il le
fait : le runtime est sous `UNITY_EDITOR || DEVELOPMENT_BUILD`). Un editeur deja ouvert sur le meme chemin fait
echouer le batchmode (« Multiple Unity instances cannot open the same project ») : les pipelines ne le lancent
que dans un worktree. Contrat complet : `templates/recipes/unity-verify.md` du skill.

## Tests du package

EditMode (parseur, evaluateur, registres, formats de resultats) et PlayMode (peripherique virtuel vu par une `InputAction`, scenarios) : Test Runner, ou batchmode `-runTests` sur un projet hote qui reference le package en `file:` et declare `"testables": ["com.aidp.playtest"]`.
