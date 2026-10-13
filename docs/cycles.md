# Moteur de cycles — lot 2a, livré le 2026-08-25

> En jeu depuis, et joué. Schéma JSON revu le 2026-10-13 : il décrivait encore le format d'août
> (libellé en dur, `displayNameKey`, valeurs d'origine).

## Architecture

**`ScriptCycleRunner`** (`Services/Economy/`) — `IStartable + ITickable + IDisposable`, enregistré dans le **RootLifetimeScope** : les cycles appartiennent à la partie, pas à l'affichage, et survivent au rechargement de la GameScene.

- États dans un `CycleSlot[]` de structs parcouru par index — zéro allocation, zéro closure, zéro boxing dans `Tick()`.
- Reconstruit ses emplacements sur `UpgradeManager.OnUpgradesRebuilt` (`Subject<Unit>` émis en fin d'`InitializeFromSave`). Sans ça, il ferait tourner des modèles disposés.
- **`AdvanceSlot` boucle** tant que `elapsed >= duration` : à haute vitesse, ou juste après un gros Overclock, plusieurs cycles s'achèvent dans la même frame. N'en payer qu'un plafonnerait silencieusement la production. Garde-fou `MinSafeDuration = 0.01f` contre la boucle infinie ; la durée elle-même est déjà bornée en amont par `AbsoluteMinCycleDuration` (`BalancingConfig`, 0,05 s), appliqué dans `UpgradeModel`.
- **L'auto-démarrage se fait par test dans `Tick`** (`IsOwned && IsAutomated`), pas par abonnement : vingt-cinq tests par frame coûtent moins qu'un abonnement par modèle à maintenir à travers chaque wipe.
- Deux flux réactifs par Script : `GetProgress(id)` pour la barre, `GetRunning(id)` pour réactiver le bouton de relance quand un cycle manuel se termine — sans scruter l'état à chaque frame.

**`UpgradeModel`** — versement, durée et coût sont **mis en cache**, recalculés uniquement au `LevelUp()`. C'était la source du problème de performance : un `Math.Pow` et un parcours de paliers par générateur affiché, à chaque tick d'argent.

**`GeneratorPresenter`** — l'abordabilité passe par `Select(...).DistinctUntilChanged()` : la vue n'est réveillée qu'au passage « pas assez » → « assez ».

**`OverclockSystem`** — `TriggerManualOverclock()` retourne les secondes réellement accordées, pour que l'UI affiche la vraie valeur (elle croît avec `ClickPowerMultiplier`). Il avance d'abord les cycles en cours, puis réveille les Scripts à l'arrêt **seulement** si `P_OVERCLOCK_AWAKE` est possédé (cf. GDD).

La **durée réelle** d'un cycle ne vient plus des paliers seuls : les TFlops la compriment
(`DuréeBase / (1 + TFlops × k) ^ exposant`) et majorent le versement. Formules et raisons dans
`game-design.md`, § « Les TFlops : deux effets ».

## Schéma JSON des upgrades

`Assets/GameData/Editor/UpgradeData/scripts.json`, `hardware.json`, `proxys.json` — un objet
`{ "items": [ … ] }`. Exemple réel (`SCR_01`, au 2026-10-13, paliers tronqués) :

```json
{
  "id": "SCR_01",
  "type": "Script",
  "order": 1,
  "baseCost": 10.0,
  "costMultiplier": 1.15,
  "baseProductionYield": 1.0,
  "baseCycleDuration": 1.5,
  "durationReductionPerLevel": 0.01,
  "minCycleDuration": 0.2,
  "traceGeneratedPerSecond": 2.7,
  "automationLevel": 10,
  "milestones": [
    { "level": 10, "effect": "DurationMultiplier", "factor": 0.8 },
    { "level": 25, "effect": "YieldMultiplier",    "factor": 1.5 }
  ]
}
```

- **Aucun libellé ici.** Les clés de nom et de description se **dérivent de l'id**
  (`UPG_SCR_01_NAME`, `UPG_SCR_01_DESC`, via `LocalizationKeys`) et vivent dans
  `StreamingAssets/Localization/fr.json`. Un champ `displayName` ou `displayNameKey` resté dans un
  JSON est ignoré, et le générateur l'**avertit**.
- `type` vaut `Script`, `Hardware` ou `Proxy`.
- `traceCapIncrease` (Hardware) : contribution au plafond de jauge. **Inactif tant que le plafond
  dynamique est actif** (`DynamicTraceCap`), qui dérive le plafond de la puissance (GDD).
- **`traceGeneratedPerSecond` change de sens selon le type** : une génération de Trace pour un
  Script ou un Hardware, une **dissipation** pour un Proxy. Le nom ment pour ce troisième type.
- `effect` vaut `YieldMultiplier`, `DurationMultiplier` ou **`TraceMultiplier`**.
- **`TraceMultiplier` change de sens selon le type**, comme le champ qu'il amplifie : c'est un BONUS pour un Proxy, dont la magnitude est une dissipation, mais un MALUS pour un Script ou un Hardware, dont elle est une génération. Le générateur avertit quand un facteur > 1 est déclaré hors Proxy. Sans ce troisième effet, les paliers ne pouvaient rien faire aux Proxies : leur rendement de production vaut zéro et ils n'ont pas de cycle.
- Le générateur **avertit aussi** pour les paliers inertes : un `DurationMultiplier` hors Script, un `YieldMultiplier` sur un rendement de base nul.
- `factor` > 1 pour un versement, < 1 pour une durée.
- Les paliers sont **multiplicatifs et cumulatifs** : trois paliers ×2 donnent ×8. C'est ce qui garde un palier lointain spectaculaire malgré la croissance linéaire du niveau. Les Scripts partagent une ladder uniforme (GDD, « Accélération des Scripts »).
- Les paliers sont triés par niveau à la génération.
- `automationLevel` et `milestones` sont **optionnels** : sans eux, versement linéaire et seuil d'automatisation à 10. ⚠️ L'automatisation au niveau 10 est **appelée à disparaître** (intention du GD du 2026-09-25, cf. GDD « Refonte de l'arbre »).

**Champs obsolètes, toujours recopiés dans le ScriptableObject pour ne pas perdre la donnée, mais
plus lus par le jeu :** `durationReductionPerLevel` (remplacé par les paliers) et
`minCycleDuration` (remplacé par la compression des TFlops et le plancher absolu).

Modifier ces JSON n'a **aucun effet** tant que `Tools/Core/Générer Catalogue Upgrades depuis JSON`
n'a pas été relancé — suivi des deux autres générateurs (voir `etat-des-lieux.md`, Outillage).

## Bug de pipeline corrigé dans ce lot

`UpgradeCatalogGenerator` ne recopiait **jamais** `baseCycleDuration`, `minCycleDuration` ni `durationReductionPerLevel`. Ces valeurs étaient écrites dans les JSON depuis le début, mais tous les ScriptableObjects portaient la durée par défaut de 1 s.

## Protocole de test

1. Acheter `SCR_01`, cliquer son bouton de lancement : la barre se remplit, l'argent tombe à l'échéance.
2. Monter au niveau 10 : le bouton disparaît, le cycle s'enchaîne seul.
3. Avec plusieurs Scripts actifs, un clic sur l'Overclock global (ou Espace) fait bondir toutes les barres et la console annonce la vraie valeur en secondes.
