# État des lieux — équilibrage

> Document de **reprise**. Il dit où on en est, ce qui reste à faire et par quoi commencer.
> Les décisions de design, elles, vivent dans `game-design.md` — qui fait foi.
>
> Dernière mise à jour : **2026-09-16**

---

## En une phrase

Le jeu se termine — et depuis le 2026-09-16 il le **dit** — tout son contenu est accessible, les
runs ne s'effondrent plus, et la posture agressive ne s'effondre plus non plus : elle meurt parfois
et boucle quand même. Il reste **le Zéro-Day, à refondre en Baroud d'Honneur**, le **verrou du
palier 90 %**, et l'**écran** de victoire par-dessus sa séquence console.

---

## Ce qui est acquis

| | Valeur |
|---|---|
| Contenu | 25 Scripts · 20 Hardware · 15 Proxies |
| Arbre de prestige | **147** nœuds, 1 point chacun *(+`P_EXTRACT_HIGHRISK` le 2026-09-16)* |
| Condition de victoire | **1e12 TFlops** |
| Pic atteint en campagne | 6,4e11 — soit ~64 % de la cible |
| Première run | 10,8 min, palier 90 % à 8,9 min |
| Durée des runs en campagne | stable, ~3,5 à 6,3 min selon la posture |

Les quatre piliers ont chacun un rôle mesurable et distinct :

- **Scripts** — ce que tu gagnes
- **Hardware** — la vitesse et la force avec lesquelles tu le gagnes *(TFlops → Scripts
  uniquement)*, et depuis le 2026-09-16 **ce que tu vois** : les TFlops divisent le brouillard du
  relevé de Trace. Troisième rôle propre, qui n'empiète toujours pas sur les Proxies
- **Furtivité** — combien de temps tu tiens. Seul levier de durée depuis le plafond dynamique :
  23 % de dissipation donnent des runs de 3,4 min, 61 % en donnent 7,0
- **Blindage** *(prestige)* — multiplie le plafond

---

## Les trois chantiers restants

### 1. La spirale de la posture agressive — DÉSAMORCÉE le 2026-09-16 *(à confirmer)*

Un joueur qui visait le dernier palier mourait, donc ne banquait rien, donc n'achetait aucun
prestige, donc mourait encore. On la croyait insoluble par réglage : en déplaçant une seule
constante, la même posture basculait de 1 saisie à 15, et de « contenu complet » à « SCR_16, arbre
à 2,7 % ».

**Ce qui l'a débloquée : le lot « capteurs ».** Le brouillard devient rachetable en Hardware, donc
le téméraire peut s'outiller au lieu de subir. Mesuré, posture 90 % :

| | Sans capteur | Capteur, brouillard 25 s | **Capteur, brouillard 60 s** |
|---|---|---|---|
| Saisies | 15 sur 18 runs | 0 | **2 sur 11** |
| Arbre | 2,7 % | complet | **complet** |
| Contenu | SCR_16 / 25 | SCR_25 / 25 | **SCR_25 / 25** |

Le téméraire **meurt parfois et boucle quand même sa campagne** — c'est l'arbitrage qui manquait. À
25 s de brouillard il ne mourait plus du tout : le capteur rachetait tout, et le pari disparaissait
avec lui. C'est `_traceReadoutMaxInterval` qui dose l'ensemble.

⚠️ **Un passage par configuration, toutes campagnes TRONQUÉES.** Signal net et cohérent, pas une
validation. À refaire avant de considérer le chantier clos.

**Ce qui reste, et qui n'est plus la même question :** la posture 90 % sort en réalité à **~68 %**
de jauge. Elle meurt en visant un palier qu'elle n'atteint presque jamais, et n'encaisse donc que
le bonus +15 %. Les deux pistes d'origine restent ouvertes pour ça :

- **Verrouiller le dernier palier.** Il est proposé dès la première run, donc il invite. S'il doit
  être réservé aux joueurs équipés, il devrait se débloquer plus tard. *(désormais la piste la plus
  cohérente : le capteur est précisément ce qui « équipe »)*
- **Adoucir la saisie.** Elle ne verse aujourd'hui *rien* au cumul de campagne. Lui faire verser 20
  à 30 % de la contribution laisserait le joueur malchanceux progresser lentement au lieu de
  stagner. Une ligne dans `GameSessionManager.ResolveRunEnd` et un réglage. *(moins urgent depuis
  que la campagne se boucle malgré les saisies)*

### 2. Le Zéro-Day Exploit ne se déclenche plus

Zéro fois sur 264 runs. Il a marché, puis s'est éteint **trois fois pour trois raisons
différentes** : seuil de charge trop haut, capacité en secondes plus longue qu'une run, et
maintenant des runs raccourcies par l'extension des paliers.

La charge exige `GhostCacheCapacitySeconds` (90 s) de posture défensive tenue au-dessus de
`MaxTraceReduction × GhostCacheReductionThreshold` (59,5 %). Vérifier lequel des deux bloque avant
de toucher à quoi que ce soit.

⚠️ **C'est la CHARGE qui est cassée, pas l'effet — mesuré le 2026-09-16.** Le GDD annonçait aussi
que les 30 secondes n'étaient « jamais atteintes » et que l'Exploit durait 1,58 s. **Faux depuis le
31/08** : ce chiffre supposait un plafond de jauge de 100, qui vaut 5e6. Mesuré sur le code actuel,
l'Overdrive **tient ses 30 secondes pleines** et laisse la partie vivante à 81,8 % de jauge.

Conséquence pour la refonte en Baroud d'Honneur : sa justification était double — conditions
d'activation inatteignables *et* durée jamais tenue. **Seule la première tient.** Raccourcir l'effet
à 3-5 s reste un choix de design défendable, mais ce n'est plus une réparation.

### 3. La victoire — câblée le 2026-09-16, l'écran reste à faire

`VictorySystem` (scope racine) observe les TFlops et déclenche une fois, au franchissement ;
`VictoryPresenter` déroule la séquence dans la console. La partie **continue** après. `HasWon` est
sauvegardé en `SaveData` v10, et restauré **avant** `InitializeFromSave` — sans cet ordre, la
victoire se rejouerait à chaque ouverture.

Vérifié hors Play Mode : franchissement, non-redéclenchement, reprise d'une sauvegarde déjà gagnée,
et migration v9 → v10.

**Seuil à 5e11 à titre provisoire** (décision GD), pour éprouver la fin de campagne sans grinder —
le pic mesuré est de 6,44e11. À remonter ensuite.

**L'écran est posé** (`Canvas/VictoryPanel`, dernier enfant, monté et câblé dans la GameScene) :
titre, séparateur, corps et bouton de reprise, aux mêmes police et couleurs que l'écran de bilan.
Vue autonome avec sa propre racine, enregistrée sous condition comme les trois autres vues neuves —
sans elle, la victoire se raconte quand même dans la console.

**Reste à faire :** l'éprouver en Play Mode. Show/Hide et la résolution des textes sont vérifiés
hors Play Mode, mais le chemin complet — conteneur, abonnement, clic de fermeture — ne l'est pas.

---

## Ce qu'il faut savoir avant de toucher à l'équilibrage

**Sept défauts ont été trouvés en deux jours, et pas un seul ne venait d'un réglage.** Tous venaient
d'une mécanique que le simulateur n'exerçait pas, ou d'une constante qui ne suivait pas le reste.
Avant de tourner un curseur, vérifier que la chose qu'on mesure est bien jouée.

⚠️ **Le simulateur est une borne basse, jamais une prédiction.** Il clique l'Overclock depuis le
2026-09-13, mais son modèle est calé sur **une seule run réelle** et reste 1,8× en dessous. Tout
écart inférieur à un facteur 2 est du bruit.

⚠️ **Rien n'a jamais été vérifié en Play Mode.** Toutes les mesures viennent de la simulation. Les
deux sessions de jeu du GD ont chacune trouvé un problème que les mesures ne voyaient pas — la run
de 92 minutes sans danger, et le rythme trop précipité.

⚠️ **Les fichiers générés sont des SORTIES.** `04_SpecificUpgrades.json` et tous les `.asset` de
`GameData/` sont écrasés par leurs générateurs. Régler une valeur dedans ne survit pas. Les sources
sont `GameData/Editor/**` et `PrestigeSpecificNodesGenerator.cs`.

⚠️ **Les valeurs de l'enum `PrestigeBonusType` s'appendent en FIN.** L'index est sérialisé dans les
`.asset` : une insertion au milieu redéfinit silencieusement des nœuds existants. Déjà arrivé.

---

## Outillage de mesure

Tout est sous `Tools/Core/Équilibrage/` :

| Entrée | Ce qu'elle répond |
|---|---|
| **Comparer les paliers d'extraction** | la mesure principale — 4 postures, durée, contenu, saisies, dissipation, mécaniques utilisées |
| **Première run — temps avant le Game Over** | le plafond dur d'une run, et la marge après chaque palier |
| **Première run — où se trouve le temps mort ?** | densité d'achats minute par minute |
| **Et si le joueur évitait une famille de nœuds ?** | ce que coûte l'évitement d'une famille |
| **Test rapide (une run)** | un relevé complet sur une seule run |

Et les générateurs, sous `Tools/Core/` : catalogue d'upgrades, nœuds ciblés, arbre depuis JSON.
**Les trois doivent être relancés dans cet ordre** après toute modification de `GameData/Editor/`.

Une campagne est coupée à **45 secondes de calcul** et s'annonce alors « MESURE TRONQUÉE ». Ce
n'est pas « jamais fini » : c'est une mesure incomplète, et les confondre mène à une fausse
conclusion.

Scripts Python versionnés sous `tools/balance/` : `recost.py` aligne la croissance des coûts sur
celle des rendements, `extend_ladder.py` étend une échelle en conservant ses deux extrémités.

---

## Réglages en vigueur

```
_dynamicTraceCap            1          le plafond suit la puissance
_dynamicCapReferenceYield   3e+05      baisser = runs plus longues
_victoryTFlops              1e+12      la clé USB
_passiveTraceFillMinutes    6          la traque de l'A.M.I.
_maxTraceReduction          0.85       asymptote de la Furtivité
_traceReadoutMaxInterval    60         brouillard AU DÉPART, avant rachat
_traceReadoutSensorPower    1          ce que les TFlops achètent en visibilité ; 0 = éteint
_traceReadoutSensorReferenceTFlops 1000  en dessous, le Hardware n'achète PAS de visibilité
_prestigeFirstThresholdDatas 5e+06     premier point de prestige — calé sur une run JOUÉE (15,7 M)
_ghostCacheCapacitySeconds  90         charge du Zéro-Day
_prestigeThresholdGrowth    1.12       +12 % de cumul par point suivant
paliers d'extraction        50 % → +15 % · 75 % → +30 % · 90 % → +50 %
```

Le plafond dynamique est **basculable** : décocher `_dynamicTraceCap` restaure l'ancien
comportement sans toucher au code, pour comparer en jouant.

---

## Par quoi je reprendrais

1. **Une session de jeu.** Les deux précédentes ont chacune trouvé ce que la simulation ne voyait
   pas. Le réglage actuel n'a jamais été joué — et le capteur, moins que tout le reste : c'est une
   mécanique de RESSENTI, une aiguille qui se fige, que la simulation ne peut pas juger.
2. **Reconfirmer le capteur** (chantier 1), en relançant les trois configurations : un seul passage
   par configuration, toutes tronquées.
3. **Le Zéro-Day**, qui ne s'identifie plus mais se refond : le GD l'a tranché le 2026-09-16 en
   **Baroud d'Honneur** — un burst de 3 à 5 s, ×50 sur le rendement, malus ×10 conservé, et **N
   usages par run** (N = rangs de `P_EXPLOIT_CHARGES`) à la place de la charge de 90 s. La charge
   disparaissant, `GhostCacheSeconds` sort du format de sauvegarde.
4. **Éprouver l'affichage des paliers en jeu.** Construit le 2026-09-16 : trois traits sur la jauge
   (franchi / incertain / à venir / verrouillé), la zone d'incertitude enfin câblée — elle ne
   l'était pas — et le gain du bouton d'exfiltration qui saute à chaque cran franchi. Vérifié hors
   Play Mode et sur rendu ; reste à juger si la pulsation du palier incertain se lit à l'œil, et
   si le saut du gain se remarque.
