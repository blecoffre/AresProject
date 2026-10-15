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
- **La refonte a été jouée d'un bloc le 2026-10-15**, ses retours aussi (`historique.md`). Restent
  le correctif de la victoire (§ 0) et la victoire elle-même (§ 1).

---

## 0. La victoire déclenchée à tort (corrigé le 2026-10-15)

Constaté par Bertrand pendant le 1.12 : deux Data Wiper, un énorme parc de Hardware qui ne suffisait
pas, puis « [PRÊT] WIPE SYSTÈME »… et la victoire. En fin de run, le sacrifice était rendu **avant**
le vidage du parc : un instant, les TFlops nettes valaient le parc entier sans sacrifice, et la
victoire le voyait. Même faille au chargement d'une sauvegarde. Compilé (Unity et `dotnet build`,
zéro erreur, zéro warning), **jamais joué**.

**Mise en place, commune aux trois lignes.** Jeu fermé, dans `savegame.json`, passer `"HasWon"` à
`false` : la victoire fausse du 15/10 l'a posé à `true`, et sans ça elle ne se rejouera jamais. Avec
« Protocole d'Urgence », acheter du Hardware et noter les TFlops de l'en-tête : **N**. Utiliser deux
fois le Data Wiper : l'en-tête tombe à 40 % de N. En Play Mode, régler `_victoryTFlops` de
`BalancingConfig.asset` à **0,5 × N** — au-dessus des 40 % qu'il reste, bien en dessous de la pleine
puissance. (Pas plus haut : au chargement, l'ancien code comptait le parc sans le Noyau Compilé,
et un seuil trop haut laisserait passer le test même sans le correctif.)

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 9.1 | **La sortie ne déclenche plus la victoire** | Après la mise en place, « [PRÊT] WIPE SYSTÈME ». | L'écran de fin habituel, **aucune** séquence de victoire. Pas de ligne « [Victory] Seuil atteint » dans la console d'Unity. |
| 9.2 | **Le rechargement non plus** | Refaire la mise en place sur une nouvelle run, mais au lieu de sortir : arrêter le Play Mode en pleine run, le relancer, « Continuer ». | Aucune victoire au chargement. Le Data Wiper annonce toujours « reste 1 », l'en-tête toujours ~40 % de N. |
| 9.3 | **La vraie victoire passe toujours** | Run suivante, seuil inchangé : racheter du Hardware, sans Data Wiper, jusqu'à dépasser 0,5 × N. | La victoire se déclenche à l'achat qui franchit le seuil. Enchaîner 1.12 à 1.14 à partir de là, puis **remettre `_victoryTFlops` à 5e11**. |

## 1. Fin de partie et économie — d'avant la refonte, jamais jouées

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons. ⚠️ La refonte « Time
Attack » a réécrit le brouillard (lot 3) ; il ne reste ici que la victoire, à rejouer.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 1.12 | **Victoire : séquence et écran** — *14/10 : séquence OK, écran absent (objet `VictoryPanel` éteint dans la scène), corrigé le jour même ; 15/10 : déclenchée à tort par la sortie, voir § 0* | Abaisser `_victoryTFlops` sous les TFlops courants (cf. « Avant de commencer »). | La console déroule la séquence (« Attaque du chiffrement… C'est un chat. »), puis l'écran « ACCÈS TOTAL » apparaît, textes résolus (aucune clé entre crochets), fond opaque qui capte les clics. La simulation **continue derrière** : la Trace monte. |
| 1.13 | **Victoire : fermeture** | Cliquer « Reprendre ». | L'écran se ferme, la partie continue où elle en était. |
| 1.14 | **Victoire : jamais deux fois** | Remettre `_victoryTFlops`, sauvegarder (retour au menu), relancer le jeu, revenir en jeu. | Aucune séquence ni écran de victoire au rechargement. (Vérifié hors Play Mode sur le code ; jamais sur le chemin complet.) |

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`).
Restent :

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.8 | **Taille d'interface au-dessus de 100 %** — *14/10 : options et inspecteur de nœud se chevauchent au-delà de 120 % (`backlog.md` § 4) ; l'inspecteur est refait le 15/10, à revoir* | Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir le menu, les options, la pause, l'écran de jeu, l'arbre, l'écran de fin. | Rien ne se chevauche, rien ne sort de l'écran. ⚠️ Le panneau d'options se chevauche par construction dès 110 % (`backlog.md` § 4) ; les autres écrans n'ont jamais été regardés au-delà de 100 %. |

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Le § 0 et le § 1 (la refonte elle-même est jouée depuis le 2026-10-15) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire, valeurs de l'Interception. Victoire remise à 1e12. |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
