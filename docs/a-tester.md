# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-15**.

---

## Avant de commencer

- **Lancer le Play Mode depuis `Assets/Data/Scenes/RootScene.unity`**, jamais depuis la GameScene
  (cf. `backlog.md`, Pièges).
- **Garder Unity au premier plan** : sans focus, le player loop de l'éditeur se fige, et rien ne
  bouge.
- **En éditeur, le bouton d'exfiltration reste cliquable même sans point.** C'est voulu, pour
  enchaîner des runs de test. Le libellé et la jauge affichés, eux, sont les vrais. Le verrou
  lui-même ne se vérifie que dans un build de production (§ 3).
- **`BalancingConfig.asset` se règle en Play Mode**, et l'effet est immédiat. Pour atteindre la
  victoire sans grinder : abaisser `_victoryTFlops` sous les TFlops du moment. **Remettre ensuite
  la valeur d'origine** (5e11, provisoire).
- **La sauvegarde est du JSON lisible**, dans
  `%userprofile%\AppData\LocalLow\DeathtreakCorp\HackTheKey\savegame.json`. En faire une copie
  avant de la retoucher. ⚠️ **Plus aucune migration** (lot 5) : la branche
  `chantier/refonte-time-attack` remplace toute sauvegarde d'un autre format par une partie neuve.
  Copier le fichier AVANT le premier lancement, pour pouvoir revenir sur `main`.
- **La refonte est entièrement jouée** (2026-10-15, `historique.md`) : plus rien en attente de ce
  côté. Prochaine étape, la session « chiffres » (§ 5).

---

## 0. Refonte « Time Attack »

Entièrement jouée le 2026-10-15, retours et correctif de la victoire compris (`historique.md`).
Rien en attente.

## 1. Fin de partie et économie

La victoire a été jouée le 2026-10-15 (`historique.md`). Rien en attente.

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`). La
taille d'interface au-dessus de 100 % attend que l'interface soit définitive (§ 5).

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Maintenant (refonte jouée le 2026-10-15) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire, valeurs de l'Interception. Victoire remise à 1e12. |
| L'interface définitive | **Taille d'interface au-dessus de 100 %** (ex-2.8) — *15/10 : « semble ok partout », vérification sommaire ; Bertrand la garde ouverte jusqu'à ce que toute l'interface soit définitive*. Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir **tous** les écrans : menu, chaque onglet des options (Audio surtout, le plus haut), pause, écran de jeu, arbre et inspecteur de nœud (« Extraction Haut Risque », la plus longue description), écran de fin, victoire. Rien ne doit se chevaucher ni sortir de l'écran (`backlog.md` § 4). |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
