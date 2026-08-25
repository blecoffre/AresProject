# Décisions de game design — tranchées, à appliquer telles quelles

Validées par Bertrand le 2026-08-25. Ne pas réinventer ces règles ; si l'une pose problème, le signaler avant d'écrire du code qui la contourne.

## Les quatre ressources

| Ressource | Rôle | Source |
|---|---|---|
| **Argent (`$`)** | Monnaie d'achat unique : Scripts et Proxies | Cycles des Scripts |
| **TFlops** | Capacité de calcul — n'achète rien, **modifie** les deux autres piliers | Hardware possédé |
| **Trace (A.M.I.)** | Jauge de Game Over | Générée par les upgrades, dissipée par les Proxies |
| **CPU Cycles** | Monnaie de méta-progression, survit au wipe | Fin de run |

Principe directeur : **aucune ressource ne doit flotter**. Si un joueur peut ignorer une ressource, l'économie est cassée.

## Les TFlops sont une CAPACITÉ dérivée, pas un stock

`TFlops = Σ rendement des Hardware possédés (+ StartingComputerPower du prestige)`

La valeur ne bouge qu'à l'achat d'un Hardware. Elle ne s'accumule pas dans le temps et ne se dépense pas.

**Conséquence technique :** `UserCurrencies.ComputerPower` ne doit plus être un `Currency` alimenté par `Add()`, mais une `ReadOnlyReactiveProperty<double>` calculée depuis `UpgradeManager`. Le champ `ComputerPower` de `SaveData` devient redondant (recalculable depuis les niveaux d'upgrades) — conservé en v1, à retirer au lot 2b.

Les TFlops agissent à deux endroits :

**1. Compression du temps sur les Scripts**
```
TempsReel = TempsBase / (1 + TFlops × 0.05)
```
10 s de base avec 20 TFlops → 5 s. Décroissance asymptotique : ne tombe jamais à zéro.

**2. Efficacité des Proxies**
```
DissipationTrace = ProxyBase × (1 + log10(1 + TFlops))
```
Le `log10` donne un gros boost au début puis aplatit la courbe — le joueur ne doit **jamais** devenir indétectable.

## Cycles de production — Scripts uniquement

Chaque Script a sa propre barre et verse son montant **à la fin** de son cycle. Le Hardware fixe la capacité TFlops dès l'achat (effet immédiat, pas de barre). Les Proxies dissipent en continu (pas de barre).

Le niveau augmente **toujours** le versement (`BaseProductionYield × niveau`). Les bonus supplémentaires viennent de **paliers explicites** listés par Script (voir `cycles.md`).

**Automatisation par seuil, propre à chaque Script.** Sous le seuil, le joueur lance chaque cycle à la main ; au-dessus, il se relance seul. Un nœud de prestige ciblé (`TargetUpgradeId`), achetable par rangs, abaissera ce seuil de quelques niveaux par rang — **ce nœud reste à écrire dans les données**.

Le clic global (Overclock) **n'est pas un démarreur** : il avance tous les cycles déjà en cours de `0,5 s × ClickPowerMultiplier`.

Les cycles sont **gelés** en fin de run et remis à zéro au redémarrage. La progression partielle n'est pas sauvegardée.

## Trace — les Proxies agissent sur le débit, jamais sur la jauge

```
TraceBrute  = Σ TraceScripts × (1 − ReductionPrestige)
DebitTrace  = max(0, TraceBrute − Σ DissipationProxies)
```

La réduction de prestige s'applique **avant** la soustraction des Proxies.

La jauge elle-même n'est réduite que par le **Bouton d'Urgence** (coût exponentiel, ×3 par clic) et par le wipe. Règle d'or : *« le FBI n'oublie jamais, sauf si tu formates tout. »* Il faut un mur qui pousse inévitablement au wipe — sinon le joueur trouve une planque parfaite (`ΔTrace = 0`) et le danger disparaît.

## Exfiltration volontaire — « Protocole Terre Brûlée » (à implémenter)

| | Déclencheur | Récompense |
|---|---|---|
| **Saisie Fédérale** | Trace atteint 100 % | CPU Cycles calculés normalement |
| **Effacement Propre** | Le joueur clique avant 100 % | CPU Cycles **+20 % « Clean Exit »** |

Le bonus pousse le joueur à flirter avec 95 % de Trace puis à sortir juste avant l'arrestation.

**Déblocage :** bouton cliquable uniquement si la run génère au moins 1 CPU Cycle. Placé bien visible, en rouge, dans le Header.

- Grisé, au survol : `[ERREUR] Données insuffisantes pour compiler un nouveau noyau.`
- Débloqué : rouge clignotant, au survol `[PRÊT] Formatage recommandé. Gain estimé : +X Cycles CPU`

**Séquence :** geler l'UI ~2 s et dérouler dans la console —
```
> Lancement de PURGE.exe...
> Effacement des disques en cours... 100%
> Brouillage de l'adresse MAC... 100%
> La clé USB est en sécurité.
> Compilation des erreurs... Noyau IA amélioré.
> Redémarrage du système...
```

## Lore

L'objectif ultime du joueur est de récupérer **une photo de chat sur une clé USB verrouillée**. Chaque wipe = déménagement dans un nouveau sous-sol avec un noyau IA amélioré, compilé à partir des tentatives ratées. Ton absurde assumé.

## Échelle de contenu

≈ 15 améliorations par onglet (45 générateurs) et **≈ 170 nœuds de prestige**. Cette dernière valeur rend le sujet performance et le rendu de l'arbre non négociables : pooling ou virtualisation seront nécessaires.

## Sauvegarde

- Autosave toutes les **5 minutes**, plus fin de run et achat de prestige.
- Save d'urgence à la fermeture (croix, Alt+F4). Ne couvre ni un kill process ni un crash.
- **Aucune progression hors-ligne, aucun rattrapage au retour.**
- Fichier en JSON lisible et éditable à la main — choix assumé jusqu'à la sortie Steam.

## Steam

Pas de page Steam ni d'AppId pour l'instant. La sauvegarde **locale fait autorité** ; le cloud viendra plus tard et servira de miroir, départagé par horodatage.

## Question de design encore ouverte

Un Script génère-t-il de la Trace en permanence dès qu'il est possédé (comportement actuel), ou **seulement pendant qu'un cycle tourne** ? Le second est plus cohérent avec le modèle manuel/auto mais change le rythme du début de partie. À trancher avec Bertrand au lot 2b.
