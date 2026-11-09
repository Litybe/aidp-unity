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

Les fichiers de demande sont pris par un `FileSystemWatcher` ; le traitement est poste sur le thread principal. Hors heartbeat (ci-dessous), aucun abonnement permanent a `EditorApplication.update`. L'etat survit au domain reload (`SessionState`). Un scenario est refuse si l'editeur est deja en Play Mode ; une demande de tests arrivee pendant un Play Mode part au retour en mode edition.

## Garde d'editeur (zero intervention humaine)

Avant chaque porte, `Aidp.Playtest.Editor.EditorGuard.Prepare()` met l'editeur dans un etat neutre, par des appels scriptes qui n'ouvrent jamais de boite de dialogue :

1. scenes sales : rechargees depuis le disque (`DirtyScenes = Discard`, defaut), sauvegardees (`Save`) ou laissees (`Ignore`) ;
2. scene d'edition : `EditScene` (defaut `Assets/Tests/Playtest/Scenes/PlaytestArena.unity`, si le fichier existe) est ouverte seule en mode edition. Un `git checkout` ne modifie donc jamais le fichier de la scene ouverte et aucun `Level_*` ne reste charge dans l'editeur de test. Vide : la scene ouverte est laissee ;
3. `AssetDatabase.Refresh()` : ce que le harness a ecrit pendant que la fenetre n'avait pas le focus est importe avant de jouer (pas seulement a la compile gate). Si le refresh declenche une compilation, la demande reprend a la fin du domain reload.

Reglages dans `ProjectSettings/AidpPlaytest.asset` (`Tools/AIDP/Playtest/Settings`, versionne). Menu `Tools/AIDP/Playtest/Prepare editor now` pour l'appliquer a la main. Les niveaux se testent toujours via `spawn` dans l'arene, jamais en ouvrant un `Level_*`.

## Preferences editeur figees

Au chargement (`ApplyEditorPreferences`, defaut vrai), `EditorPreferencesBootstrap` ecrit les preferences utilisateur qui evitent les interruptions : *Script Changes While Playing* = *Recompile After Finished Playing* (un `.cs` ecrit pendant un scenario ne casse plus la partie), *Auto Refresh* actif, *Directory Monitoring* actif. Option `DisableDomainReload` : active *Enter Play Mode Options* sans domain reload (entree en Play Mode quasi instantanee) ; a reserver a un jeu qui remet ses statiques a zero (`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`). Menu `Tools/AIDP/Playtest/Apply editor preferences`.

## Heartbeat

`EditorHeartbeat` ecrit `.aidp/editor/heartbeat.json` toutes les 2 s (`Heartbeat`, defaut vrai), une derniere fois juste avant un domain reload (`state: reloading`) et a la fermeture (`quitting`) :

```json
{ "at": "2026-11-09T10:00:02.120Z", "pid": 4242, "unityVersion": "6000.0.1f1", "projectRoot": "D:/Game",
  "state": "idle | compiling | importing | compile-failed | entering-play | playing | paused | reloading | quitting",
  "activeScene": "Assets/Tests/Playtest/Scenes/PlaytestArena.unity", "dirty": false, "playtest": null, "compileFailed": false,
  "staleAfterSeconds": 15, "lastError": { "message": "...", "at": "..." }, "lastGuard": { "at": "...", "steps": ["OpenEditScene", "RefreshAssets"], "skipped": null, "activeScene": "..." } }
```

Lecture par le harness : `at` plus vieux que `staleAfterSeconds` avec un process Unity vivant et un etat autre que `reloading` = thread principal bloque (boite de dialogue, gel) : a rapporter tel quel plutot que d'attendre un timeout de 600 s. Le gateway le renvoie dans `GET /api/unity/status` (`editor.heartbeat`).

Regression : `public class ScenarioPlayModeTests : PlaytestScenarioTestBase { }` dans un asmdef de tests PlayMode (references `Aidp.Playtest.Runtime`, `Aidp.Playtest.TestSupport`) : un test par fichier de `Assets/Tests/Playtest/Scenarios/`.

Repro : menu `Tools/AIDP/Playtest/Start repro recording` en Play Mode, puis `Stop ... and save` : scenario JSON dans `.aidp/playtest/recordings/`.

## Tests du package

EditMode (parseur, evaluateur, registres, formats de resultats) et PlayMode (peripherique virtuel vu par une `InputAction`, scenarios) : Test Runner, ou batchmode `-runTests` sur un projet hote qui reference le package en `file:` et declare `"testables": ["com.aidp.playtest"]`.
