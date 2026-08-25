# Moteur de cycles — lot 2a, livré le 2026-08-25 (validation en attente)

## Architecture

**`ScriptCycleRunner`** (`Services/Economy/`) — `IStartable + ITickable + IDisposable`, enregistré dans le **RootLifetimeScope** : les cycles appartiennent à la partie, pas à l'affichage, et survivent au rechargement de la GameScene.

- États dans un `CycleSlot[]` de structs parcouru par index — zéro allocation, zéro closure, zéro boxing dans `Tick()`.
- Reconstruit ses emplacements sur `UpgradeManager.OnUpgradesRebuilt` (`Subject<Unit>` émis en fin d'`InitializeFromSave`). Sans ça, il ferait tourner des modèles disposés.
- **`AdvanceSlot` boucle** tant que `elapsed >= duration` : à haute vitesse, ou juste après un gros Overclock, plusieurs cycles s'achèvent dans la même frame. N'en payer qu'un plafonnerait silencieusement la production. Garde-fou `MinSafeDuration = 0.01f` contre la boucle infinie.
- **L'auto-démarrage se fait par test dans `Tick`** (`IsOwned && IsAutomated`), pas par abonnement : quinze tests par frame coûtent moins qu'un abonnement par modèle à maintenir à travers chaque wipe.
- Deux flux réactifs par Script : `GetProgress(id)` pour la barre, `GetRunning(id)` pour réactiver le bouton de relance quand un cycle manuel se termine — sans scruter l'état à chaque frame.

**`UpgradeModel`** — versement, durée et coût sont **mis en cache**, recalculés uniquement au `LevelUp()`. C'était la source du problème de performance : un `Math.Pow` et un parcours de paliers par générateur affiché, à chaque tick d'argent.

**`GeneratorPresenter`** — l'abordabilité passe par `Select(...).DistinctUntilChanged()` : la vue n'est réveillée qu'au passage « pas assez » → « assez ».

**`OverclockSystem`** — `TriggerManualOverclock()` retourne les secondes réellement accordées, pour que l'UI affiche la vraie valeur (elle croît avec `ClickPowerMultiplier`).

## Schéma JSON des upgrades

`Assets/GameData/Editor/UpgradeData/*.json`

```json
{
  "id": "SCR_01",
  "displayName": "Phishing familial",
  "type": "Script",
  "order": 1,
  "baseCost": 10.0,
  "costMultiplier": 1.07,
  "baseProductionYield": 1.0,
  "traceGeneratedPerSecond": 0.5,
  "baseCycleDuration": 1.5,
  "minCycleDuration": 0.2,
  "automationLevel": 10,
  "milestones": [
    { "level": 10, "effect": "YieldMultiplier",    "factor": 2.0 },
    { "level": 25, "effect": "DurationMultiplier", "factor": 0.75 },
    { "level": 50, "effect": "YieldMultiplier",    "factor": 3.0 }
  ]
}
```

- `effect` vaut `YieldMultiplier` ou `DurationMultiplier`.
- `factor` > 1 pour un versement, < 1 pour une durée.
- Les paliers sont **multiplicatifs et cumulatifs** : trois paliers ×2 donnent ×8. C'est ce qui garde un palier lointain spectaculaire malgré la croissance linéaire du niveau.
- Les paliers sont triés par niveau à la génération.
- `automationLevel` et `milestones` sont **optionnels** : sans eux, versement linéaire et seuil d'automatisation à 10.
- `displayNameKey` est accepté comme alias de `displayName`.

**`durationReductionPerLevel` est obsolète** — remplacé par les paliers, mais toujours recopié dans le ScriptableObject pour ne pas perdre la donnée déjà écrite.

## Bug de pipeline corrigé dans ce lot

`UpgradeCatalogGenerator` ne recopiait **jamais** `baseCycleDuration`, `minCycleDuration` ni `durationReductionPerLevel`. Ces valeurs étaient écrites dans les JSON depuis le début, mais tous les ScriptableObjects portaient la durée par défaut de 1 s.

## Protocole de test

1. Acheter `SCR_01`, cliquer son bouton de lancement : la barre se remplit, l'argent tombe à l'échéance.
2. Monter au niveau 10 : le bouton disparaît, le cycle s'enchaîne seul.
3. Avec plusieurs Scripts actifs, un clic sur l'Overclock global fait bondir toutes les barres et la console annonce la vraie valeur en secondes.
