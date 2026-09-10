# Simulateur d'équilibrage

Réplique les formules d'Ares hors Unity, pour mesurer un rythme sans lancer le jeu.
Il lit `Assets/GameData/Balancing/BalancingConfig.asset` et les JSON de `GameData`
**directement** : aucune valeur n'est recopiée ici, il ne peut donc pas dériver du projet.

```bash
cd tools/balance
python measure_blindage.py     # contrôle de fidélité + effet des paliers de prérequis
python diag_campagne.py        # qui bloque la campagne, et pourquoi
```

## ⚠️ Ce à quoi on peut se fier, et ce à quoi on ne peut pas

| Périmètre | État | Vérification |
|---|---|---|
| **Modèle de run** | ✅ fidèle | Reproduit au chiffre près les mesures de `docs/game-design.md` : `SCR_01` niveau 10 à 90 s, 33 761 Datas à 5 min, run 1 de 27,0 min s'arrêtant à `SCR_05` |
| **Boucle de campagne** | ❌ **non fidèle** | Donne 11 runs / 16 h / 18 % de l'arbre là où août documente 19 runs / 9,93 h / arbre complet |

L'écart de campagne persiste **quelle que soit la règle de sortie de run** (testé : ratio de
plateau de 1,6 à 5,0, fenêtre de 0,15 à 0,35, plafond de 7 200 ou 9 000 s — l'arbre reste entre
10 et 18 %). L'agent est environ deux fois plus faible en fin de partie que celui d'août
(dernière run à 2,15e10 Datas contre 9,3e10 documentés). Cause non identifiée.

**En clair : mesurer une run, oui. Annoncer une durée de partie, non** — tant que cet écart n'est
pas expliqué.

## Structure

- `ares_sim.py` — `Cfg` (lit l'asset), `Meta` (prestige, prérequis à niveau), `Up` (générateur), `Run`
- `ares_agent.py` — joueur **glouton** : achète au meilleur gain immédiat par euro dépensé.
  Joue mieux qu'un débutant, moins bien qu'un optimiseur.
- `measure_blindage.py`, `diag_campagne.py` — scripts de mesure

Sources répliquées : `UpgradeModel.RecalculateCache`, `UpgradeManager.RecalculateTotals`,
`SimulationTicker.Tick`, `ThreatManager`, `PrestigeManager` (dont `PrestigeRequirement`),
`UserCurrencies.CalculatePendingCpuCycles`.
