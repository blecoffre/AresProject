# Décisions de game design — tranchées, à appliquer telles quelles

Validées par Bertrand le 2026-08-25. Ne pas réinventer ces règles ; si l'une pose problème, le signaler avant d'écrire du code qui la contourne.

## Les quatre ressources

| Ressource | Rôle | Source |
|---|---|---|
| **Argent (`$`)** | Monnaie d'achat unique : Scripts, Proxies **et Hardware** | Cycles des Scripts |
| **TFlops** | Capacité de calcul — n'achète rien, **modifie** les deux autres piliers | Hardware possédé |
| **Trace (A.M.I.)** | Jauge de Game Over | Générée par les upgrades, dissipée par les Proxies |
| **CPU Cycles** | Monnaie de méta-progression, survit au wipe | Fin de run |

Principe directeur : **aucune ressource ne doit flotter**. Si un joueur peut ignorer une ressource, l'économie est cassée.

## Les TFlops sont une CAPACITÉ dérivée, pas un stock

`TFlops = Σ rendement des Hardware possédés (+ StartingComputerPower du prestige)`

**Magnitude des bonus de prestige (tranché le 2026-08-26)** : `StartingMoney` et `StartingComputerPower` se calculent avec `BonusPerLevel × niveau`, comme les cinq autres types de bonus — et **non** avec `BaseCost × niveau` comme le fait le code actuel. Indexer l'effet d'un nœud sur son prix couple deux réglages qui doivent bouger séparément pendant l'équilibrage.

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

**Automatisation par seuil, propre à chaque Script.** Sous le seuil, le joueur lance chaque cycle à la main ; au-dessus, il se relance seul. Un nœud de prestige ciblé (`TargetUpgradeId`), achetable par rangs, abaissera ce seuil de quelques niveaux par rang. *(Note technique : s'assurer d'ajouter le champ `TargetUpgradeId` de type `string` dans le modèle de données `PrestigeItemData` du script de désérialisation JSON).*

**Action de l'Overclock (Clic manuel) :** Le clic global (Overclock) agit comme un **réveil**. S'il y a des Scripts inactifs (car sous le seuil d'automatisation), le clic de l'Overclock les démarre en priorité. Si les cycles sont déjà en cours, il avance le temps de tous les cycles de `0,5 s × ClickPowerMultiplier`.

**Précision tranchée le 2026-08-26 — un clic fait les DEUX, jamais l'un ou l'autre.** Un même clic démarre d'un coup **tous** les Scripts possédés à l'arrêt, *et* avance de `0,5 s × ClickPowerMultiplier` ceux qui tournaient déjà. Aucun clic n'est donc perdu, et sa valeur reste constante à tous les stades de la partie. Ne pas implémenter de priorité exclusive (« tant qu'il reste un inactif, on ne fait que réveiller ») : en début de partie, chaque clic ne servirait qu'à relancer et jamais à accélérer.

Les cycles sont **gelés** en fin de run et remis à zéro au redémarrage. La progression partielle n'est pas sauvegardée.

## Trace — les Proxies agissent sur le débit, jamais sur la jauge

```
TraceBrute  = (Σ TraceScriptsActifs + Σ TraceHardwarePossédés) × (1 − ReductionPrestige)
DebitTrace  = max(0, TraceBrute − Σ DissipationProxies)
```

La réduction de prestige s'applique **avant** la soustraction des Proxies.

**Génération de la Trace :** Un Script génère de la Trace **uniquement pendant qu'un cycle tourne**. Cela renforce le concept de risk/reward (l'A.M.I. ne repère le piratage que lorsqu'il est actif), particulièrement en début de partie quand le lancement est strictement manuel.

**Le Hardware, lui, chauffe en permanence** (tranché le 2026-08-26) : sa `traceGeneratedPerSecond` s'applique en continu dès l'achat, puisqu'il n'a pas de cycle. C'est ce qui maintient un risque sur l'onglet le plus rentable — sans quoi le Hardware deviendrait un achat sans contrepartie.

**Attention au nom du champ.** `traceGeneratedPerSecond` porte une **génération** pour les Scripts et les Hardware, mais une **dissipation** pour les Proxies (dont le `baseProductionYield` vaut 0). Le nom ment pour ce troisième type : c'est la valeur qui alimente `ProxyBase` dans la formule de dissipation ci-dessus, et elle doit être **soustraite**, jamais ajoutée.

**Synergie des Proxies (tranché le 2026-08-27)** : chaque niveau de Proxy possédé accélère TOUS les Scripts de 1 %, cumulé sur l'ensemble du parc — `1 + niveaux × 0,01`. Un achat de Proxy n'est donc jamais perdu, même quand la Trace est basse. Multiplicateur **dédié**, appliqué à la durée de cycle : le brancher sur les TFlops créerait une boucle, la dissipation dépendant elle-même des TFlops par son `log10`. ⚠️ Linéaire et sans plafond, contrairement au reste du jeu — c'est `minCycleDuration` qui bornera l'effet, donc un plafond subi plutôt que choisi.

**La règle d'or tient, confirmée le 2026-08-27.** Un excédent de dissipation ne fait PAS redescendre la jauge : il est capté par le Ghost Cache. Si la jauge se vidait, le joueur aurait toujours toute la marge devant lui et déclencher l'Overdrive ne coûterait rien. En la laissant où elle est, le Ghost Cache se remplit à la hauteur où le joueur s'est arrêté : à 30 % il a de la marge, à 80 % c'est un pari. C'est là que naît le choix « j'exfiltre ou je charge encore ».

## Ghost Cache et Zéro-Day Exploit

Valeurs tranchées par le GD le 2026-08-27. L'excédent de dissipation, jusque-là purement jeté, alimente une charge unique échangeable contre un pic de production.

| Paramètre | Valeur | Raison |
|---|---|---|
| **Capacité de la jauge** | **300 secondes d'excédent** | Exprimée en temps et non en magnitude, sinon un joueur de fin de partie la remplirait instantanément |
| **Durée de l'Exploit** | **30 secondes** | Assez long pour regarder la Trace monter et le regretter |
| **Multiplicateur** | **×50 sur le RENDEMENT** | Pas sur la vitesse : `minCycleDuration` bornerait l'effet et le gain deviendrait imprévisible d'un Script à l'autre |
| **Contrepartie 1** | **Dissipation → 0** | Tous les Proxies s'éteignent |
| **Contrepartie 2** | **×10 sur la génération brute de Trace** | Ajouté le 2026-08-27. La jauge se remplit dix fois plus vite en plus de n'être plus dissipée |

**La charge se remplit en TEMPS, pas en magnitude.** Une seconde passée en excédent vaut une seconde de charge, que l'excédent soit de 1 ou de 100 000. Le prix à payer est un maintien de posture défensive, pas un empilement de Proxies. ⚠️ Conséquence à surveiller : rien ne récompense un excédent massif, seulement sa durée.

**Le ×50 ne touche QUE les Scripts.** Chez un Hardware, le « rendement » EST sa contribution en TFlops : le laisser passer multiplierait par 50 la capacité de calcul, donc la compression des cycles *et* la dissipation des Proxies. Un buff économique deviendrait une invulnérabilité.

⚠️ **Conséquence mesurée du ×10 : les 30 secondes ne sont jamais atteintes.** La dissipation valant zéro, le temps de survie depuis une jauge vide vaut `100 / (génération × 10)`. Tenir les 30 s exige donc une génération ≤ **0,33 Trace/s**, alors qu'un seul `SCR_01` de niveau 1 en produit déjà 0,5. Vérifié en Play Mode avec `SCR_01` niveau 12 et `HW_01` niveau 4 (6,32 Trace/s) : jauge remplie à 0,632/s, saisie à **1,58 s**. L'Exploit est donc, en l'état, un bouton « encaisse et exfiltre » de une à deux secondes, pas une fenêtre de 30 s. À arbitrer — voir `backlog.md`.

### Les trois nœuds de prestige de l'Exploit

Ajoutés le 2026-08-27, branche Économie, tous trois derrière `P_EMERG`.

| Nœud | Rangs | Par rang | Au maximum |
|---|---|---|---|
| `P_EXPLOIT_CHARGES` | 5 | +1 charge stockable | 5 charges, soit 1 500 s de réserve |
| `P_EXPLOIT_MULT` | 10 | +10 % de rendement | ×100 au lieu de ×50 |
| `P_EXPLOIT_TRACE_REDUC` | 5 | −0,5 sur le malus de Trace | ×7,5 au lieu de ×10 |

**Le Zéro-Day Exploit est VERROUILLÉ au départ.** `P_EXPLOIT_CHARGES` vaut 0 tant qu'il n'est pas
acheté, donc la capacité du Ghost Cache est nulle et **rien ne s'accumule** — pas de réserve
fantôme qui attendrait un déblocage. Le bouton porte un quatrième état, verrouillé, qui nomme le
nœud à acheter plutôt que de rester gris et muet.

Le malus de Trace est borné à ×1 : le joueur peut l'alléger, jamais le supprimer ni le retourner
en bonus.

**Cas limites, tranchés par le GD :**

- **Mort pendant l'Exploit** → la charge est perdue. C'est la punition pour avoir mal calculé son pari.
- **Interruption manuelle** → impossible. Une fois lancé, l'Exploit va jusqu'au bout.
- **Wipe** → la charge est effacée, même pleine. C'est de l'état de run. La laisser survivre offrirait un Exploit gratuit au démarrage de chaque run, exactement quand la Trace est à zéro et où il ne coûterait donc rien.
- **Fermeture du jeu** → la charge est sauvegardée (`SaveData` v4). Elle représente du temps de jeu investi.
- **Débordement** → plafonne à 1 charge, pour forcer la consommation plutôt que la thésaurisation.

*Précision technique : un Exploit EN COURS n'est pas sauvegardé — la charge ayant déjà été consommée, fermer la fenêtre pendant les 30 s revient à les perdre. Sauvegarder un effet temporaire permettrait de le mettre en pause par fermeture du jeu, dans un jeu qui n'a justement aucune progression hors-ligne.*

La jauge elle-même n'est réduite que par le **Bouton d'Urgence** (voir plus bas) et par le wipe. Règle d'or : *« le FBI n'oublie jamais, sauf si tu formates tout. »* Il faut un mur qui pousse inévitablement au wipe — sinon le joueur trouve une planque parfaite (`ΔTrace = 0`) et le danger disparaît.

## Bouton d'Urgence — le « Data Wiper »

Refondu le 2026-08-27. L'ancienne version achetait l'effacement avec de l'argent, comme un
pot-de-vin. La nouvelle ne coûte **rien** : elle **exige** de la puissance de calcul. Effacer ses
traces, c'est envoyer un ver dans les serveurs fédéraux — il faut de la force brute, pas des
billets.

| Paramètre | Valeur |
|---|---|
| **Prérequis** | Posséder un palier de TFlops. Rien n'est dépensé |
| **Escalade du palier** | ×3 par usage sur la run ⚠️ *valeurs provisoires : 50 TFlops de base, ×3 — non chiffrées par le GD* |
| **Effet** | −**20 points ABSOLUS** de jauge : à 63 % on tombe à 43 % |
| **Contrecoup** | **30 % des TFlops immobilisées pendant 60 s**, +10 points par usage (30, 40, 50…), plafonné à 90 % |
| **Délai** | **5 minutes** entre deux activations |
| **Déblocage** | Nœud de prestige `P_EMERG` |

**Le contrecoup est le cœur de la mécanique.** Les TFlops alimentent aussi la dissipation des
Proxies : purger la Trace **affaiblit la défense juste après**, et la jauge remonte plus vite.
Mesuré : 52 TFlops → 36,4, dissipation de 163,5 → 154,4, cycle de `SCR_01` de 0,347 s → 0,443 s.
La tranche s'alourdissant de dix points par usage, le bouton devient de plus en plus dangereux
à mesure que la run avance — bénéfique en principe, risqué en fin de run.

Le plafond à 90 % n'est pas dans la spécification du GD : sans lui, le septième usage d'une run
immobiliserait 100 % du parc, donc plus aucune dissipation ni compression de cycle. Le bouton
deviendrait un suicide pur, ce qui n'est pas un choix mais un piège.

Le contrecoup ET le délai sont **sauvegardés** (`SaveData` v5), contrairement à l'Overdrive du
Ghost Cache. L'asymétrie est volontaire : là-bas, sauvegarder aurait permis de mettre en PAUSE un
bonus ; ici, ne pas sauvegarder permettrait d'ÉCHAPPER à une pénalité en fermant la fenêtre.

## Exfiltration volontaire — « Protocole Terre Brûlée »

| | Déclencheur | Récompense |
|---|---|---|
| **Saisie Fédérale** | Trace atteint 100 % | CPU Cycles calculés normalement |
| **Effacement Propre** | Le joueur clique avant 100 % | CPU Cycles **+20 % « Clean Exit »** |

Le bonus pousse le joueur à flirter avec 95 % de Trace puis à sortir juste avant l'arrestation.

**Déblocage (précisé le 2026-08-26) :** condition UNIQUE — avoir de quoi gagner au moins 1 CPU Cycle, soit **1 000 Datas générées sur la run** (dépensées ou non), puisque `Cycles = floor(sqrt(RunMoney / 1000))`.

Pas de palier d'upgrade ni de seuil de TFlops : verrouiller derrière un achat précis casserait la liberté systémique, alors qu'un joueur qui joue mal doit pouvoir sortir s'il a farmé assez longtemps. Les TFlops sont un accélérateur, un moyen d'arriver à la fin — pas la condition de fin.

**Trois états du bouton**, la jauge portant l'explication à la place d'un texte d'aide :

1. `[VERROUILLÉ] Compilation du 1er Cycle CPU : 45 %` — barre remplie à `RunMoney / 1000`
2. `[PRÊT] WIPE SYSTÈME (Gain : +1 Cycle CPU)` — bordures rouges, cliquable
3. `[PRÊT] WIPE SYSTÈME (Gain : +2 Cycles CPU) → Prochain à 9 000 Datas` — le seuil suivant vaut `(n+1)² × 1 000`, ce qui donne au joueur une raison de repousser sa sortie

*Note technique : en éditeur et en build de développement, le bouton est toujours cliquable même à zéro cycle, pour enchaîner des runs de test sans farmer.* Placé bien visible, en rouge, dans le Header.

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
- **`runInBackground` est à `true`** (tranché le 2026-08-26). « Hors-ligne » veut dire *jeu fermé*, pas *fenêtre en arrière-plan* : le joueur peut laisser tourner Ares pendant qu'il fait autre chose, et l'A.M.I. continue de le traquer. Le mettre à `false` offrirait une planque parfaite et gratuite — il suffirait d'alt-tab pour geler la Trace.
  *Note pour les tests : ce réglage ne s'applique qu'au build. Dans l'éditeur, le player loop reste figé tant que la fenêtre Unity n'a pas le focus (`Time.frameCount` ne bouge pas). Pour vérifier un `ITickable` sans focus, appeler `Tick()` à la main plutôt que d'attendre des frames.*
- Fichier en JSON lisible et éditable à la main — choix assumé jusqu'à la sortie Steam.

## Steam

Pas de page Steam ni d'AppId pour l'instant. La sauvegarde **locale fait autorité** ; le cloud viendra plus tard et servira de miroir, départagé par horodatage.