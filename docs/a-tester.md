# À tester — livré, mais jamais vu fonctionner

> Ce qui a été construit et n'a **pas encore été observé** dans les conditions où le joueur le
> rencontrera. Pour chaque ligne : quoi faire, et ce qu'on doit voir.
>
> **Une ligne quitte ce document** quand on l'a vue fonctionner (elle part dans `historique.md`,
> datée, avec la façon dont elle a été vérifiée) ou échouer (elle devient une entrée de
> `backlog.md`).
>
> Mis à jour : **2026-10-14**.

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
- **Le test de la refonte se fait d'un bloc, sur une sauvegarde neuve** (Bertrand, 2026-10-14) :
  § 0 à § 0 sexies. Supprimer `savegame.json` avant de commencer.

---

## 0. Refonte « Time Attack », lot 1 — jauge de l'A.M.I., Overclock, Proxies

Livré le 2026-10-13. **Joué par Bertrand le 2026-10-14** : les lignes vues fonctionner sont
parties dans `historique.md` ; restent celles-ci. Chiffres
attendus à 0 TFlop et sans Blindage : vitesse de l'A.M.I. 33 UT/s, jauge pleine en ~5 min. Tout ce
qui s'affiche sur la jauge passe par le brouillard (un relevé toutes les ~24 s au départ, § 0 ter) :
pour les montées, **la console fait foi** ; les baisses, elles, se voient tout de suite.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 0.15 | **La rangée d'actions** — *trou constaté le 14/10, corrigé le jour même* | Regarder la rangée d'actions de l'écran de jeu, sous la console. | Deux lignes pleine largeur : « Overclock », puis le Data Wiper. Plus de bouton Ghost Cache, aucun vide à droite. |


## 0 bis. Refonte « Time Attack », lot 2 — Data Wiper et écran de fin sauvegardé

Livré le 2026-10-14, compilé (Unity, zéro erreur), **jamais joué**. Pour débloquer le Data Wiper, il
faut le nœud **« Protocole d'Urgence »** (1 point), relié à la racine de l'arbre. Sans point en
réserve : jeu fermé, monter `CpuCycles` dans `savegame.json` (par exemple à 5), puis relancer.
Les achats de l'arbre ne se font qu'entre deux runs, sur l'écran de fin. Pour finir une run vite,
en éditeur, le bouton « [PRÊT] WIPE SYSTÈME » de la console est toujours cliquable.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.1 | **Verrouillé** | Sans le nœud « Protocole d'Urgence », regarder le bouton sous « Overclock ». | « [VERROUILLÉ] Data Wiper — requiert le nœud Protocole d'Urgence », grisé. |
| 2.2 | **Prêt** | Acheter « Protocole d'Urgence » sur l'écran de fin, « [ NOUVELLE INTRUSION ] ». | « [PRÊT] DATA WIPER — Jauge −2500 UT / TFlops sacrifiées : 30 % · usages restants : 3 ». Cliquable, sans aucune condition de TFlops. |
| 2.3 | **Premier usage** | Posséder du Hardware (noter les TFlops de l'en-tête), laisser la jauge dépasser 25 %, cliquer le Data Wiper. | Console : « > wiper.sh --inject : … Jauge −2500 UT, 30 % des TFlops englouties jusqu'à la fin de la run. » L'aiguille de la jauge recule aussitôt d'environ 25 points. Les TFlops de l'en-tête tombent à 70 %. Le bouton annonce maintenant 60 % et 2 usages restants. |
| 2.4 | **La menace ne ralentit pas** | Juste avant et juste après le premier usage, un Overclock pendant un cycle. | La console annonce la **même** pénalité en UT avant et après (« Jauge +N UT ») : le sacrifice n'a pas touché la vitesse de l'A.M.I. |
| 2.5 | **Épuisé** | Deux usages de plus. | Après le troisième : « [ÉPUISÉ] DATA WIPER — 90 % des TFlops sacrifiées jusqu'à la fin de la run », grisé, jauge pleine de la couleur du sacrifice. TFlops de l'en-tête à 10 % de leur valeur de départ ; les barres des Scripts sont nettement plus lentes. |
| 2.6 | **Usage à vide** | Run neuve, avant toute Data volée : cliquer le Data Wiper. | Console : « Jauge −0 UT » ; l'usage est consommé quand même (2 restants). |
| 2.7 | **Le sacrifice survit à la fermeture** | Deux usages, puis fermer le jeu (croix) et relancer, « Continuer ». | Le bouton annonce 1 usage restant ; les TFlops de l'en-tête sont toujours à 40 %. |
| 2.8 | **Le wipe le rend** | Finir la run, « [ NOUVELLE INTRUSION ] ». | Trois usages de nouveau ; plus aucune TFlop sacrifiée. Au bilan de la run précédente : « Data Wiper déployé … N fois ». |
| 2.9 | **Écran de fin rouvert après fermeture** | Finir une run (saisie ou sortie), rester sur l'écran de fin, **fermer le jeu** (croix ou Alt+F4), relancer, « Continuer ». | L'écran de fin revient, avec le même bilan et l'arbre. Un nœud s'achète. « [ NOUVELLE INTRUSION ] » lance bien une run neuve. |
| 2.10 | **Écran de fin rouvert depuis le menu** | Sur l'écran de fin : Échap, « Menu principal », puis « Continuer ». | Même chose qu'en 2.9 : écran de fin et arbre, pas de run neuve. |

## 0 ter. Refonte « Time Attack », lot 3 — le brouillard

Livré le 2026-10-14, compilé (Unity, zéro erreur), **jamais joué**. La jauge de l'en-tête porte deux
choses : **l'aiguille** (le remplissage, avec son libellé « N % · relevé il y a N s ») et **la barre
rouge** devant elle (la borne pessimiste).

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 3.1 | **Vitesse de l'A.M.I.** (ex-0.3) | Sans Hardware ni Proxy, laisser tourner une run après la première Data. | L'aiguille avance par à-coups d'environ **8 %** toutes les ~24 s ; le libellé compte « relevé il y a 1 s, 2 s… » entre deux. ~20 % par minute, saisie vers 5 min. |
| 3.2 | **La barre rouge avance en continu** | Même run, regarder la barre rouge devant l'aiguille. | Elle avance à chaque instant, plus vite que l'aiguille (×3), et retombe sur l'aiguille à chaque relevé. Elle n'est jamais **derrière** l'aiguille. |
| 3.3 | **Overclock à vide** (ex-0.4) | Sans cycle en cours, Espace. | Console « Overclock sans effet » ; ni l'aiguille ni la barre ne bougent. |
| 3.4 | **Overclock en rafale** | Cycles en cours, quatre ou cinq appuis rapides. | La barre rouge **saute** à chaque appui retenu ; l'aiguille suit au relevé suivant, qui arrive plus tôt (la pénalité remplit la jauge). |
| 3.5 | **Après un Proxy** | Jauge à 20 % ou plus, acheter un « VPN Public Décentralisé » (`PRX_01`). | L'aiguille recule aussitôt ; la barre rouge repart **immédiatement** de là — elle ne disparaît plus. |
| 3.6 | **Le rechargement ne lève plus le brouillard** | En pleine run, noter l'aiguille et le « relevé il y a N s ». Fermer le jeu, relancer, « Continuer ». | Même aiguille, même âge de relevé qu'à la fermeture : la vraie jauge n'est pas révélée. |
| 3.7 | **Le capteur s'achète** | Monter les TFlops (Hardware) au fil d'une run longue, ou éditer la sauvegarde pour posséder un gros parc. | Les relevés se rapprochent (l'aiguille saute de moins en moins loin) et la barre rouge colle davantage à l'aiguille. |
| 3.8 | **Paliers et gain sur l'aiguille** | Approcher 50 % de jauge en regardant le bouton « [PRÊT] WIPE SYSTÈME » et le trait des 50 %. | Le trait passe à l'ambre pulsant quand la barre rouge l'enjambe, au vert quand l'**aiguille** le franchit ; le gain du bouton saute au même moment, pas avant. |
| 3.9 | **Pause** | Échap 30 s en pleine run. | Ni l'aiguille, ni la barre, ni le « relevé il y a N s » ne bougent. |

## 0 quater. Refonte « Time Attack », lot 4 — l'arbre à 21 nœuds et le Noyau Compilé

Livré le 2026-10-14, compilé (Unity, zéro erreur), arbre régénéré, **jamais joué**. Pour avoir des
points : jeu fermé, monter `CpuCycles` dans `savegame.json` (200 suffit), relancer. Les achats ne se
font qu'entre deux runs, sur l'écran de fin ; en éditeur, « [PRÊT] WIPE SYSTÈME » finit une run tout
de suite. Pour simuler beaucoup de points dépensés sans cliquer : jeu fermé, ajouter
`{ "Id": "P_CORE_OVERRIDE", "Level": 50 }` à la liste `PrestigeUpgrades` de la sauvegarde.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 4.1 | **L'arbre** | Ouvrir l'arbre (P, ou « [ NOYAU IA — ARBRE ] »), dézoomer à la molette. | 21 nœuds. « Noyau Compromis » en haut ; sept colonnes en dessous, de gauche à droite : « Filtrage DNS », « Cage de Faraday », « Protocole d'Urgence », « Recompilation Forcée », « Code Polymorphe », « Sortie répétée », « Surcadencement ». Plus aucun nœud « Opti Coût » ou « Opti Prod ». Aucun lien ne traverse un nœud. Dire si ça fait trop « arbre de talents de RPG ». |
| 4.2 | **Ligne du Noyau masquée** | Sauvegarde neuve, aucun point dépensé. | Ni sous les boutons ×1 / ×10 / ×100 / MAX, ni en haut de l'arbre : pas de ligne « Noyau : ». Aucun vide gênant sous les boutons de quantité. |
| 4.3 | **Ligne du Noyau affichée** | Sur l'écran de fin, acheter un rang de « Code Polymorphe », puis « [ NOUVELLE INTRUSION ] ». | « Noyau : coûts −1 %, rendement ×1,01 » en haut de l'arbre dès l'achat, puis sous les boutons de quantité en jeu. |
| 4.4 | **Le Noyau baisse les prix d'entrée** | Avec 50 rangs de « Recompilation Forcée » (sauvegarde éditée), run neuve. | Ligne « Noyau : coûts −39 %, rendement ×1,50 ». « Campagne de Phishing Basique » affiche un coût de 7 au lieu de 10 (6,05 arrondi au-dessus), « Vieux PC Recyclé » 31 au lieu de 50 (30,25). |
| 4.5 | **… et pas la menace** | Même run. Overclock pendant un cycle, sans Hardware ; puis un « VPN Public Décentralisé ». | Console : pénalité « Jauge +165 UT », exactement comme sans Noyau. Le Proxy retire ~82 UT (2,5 s × 33 UT/s), comme sans Noyau. |
| 4.6 | **TFlops nettes** | Même run, acheter un « Vieux PC Recyclé ». | Les TFlops de l'en-tête valent 1,5 fois ce que le Hardware annonce produire. |
| 4.7 | **Recompilation Forcée sans plafond** | En acheter trois rangs. | Le nœud affiche « Niv. 3 », jamais « / max » ni « MAX ». Inspecteur : « > Niveau actuel : 3 » et « > Chaque niveau compte comme un point de plus dans le Noyau Compilé ». Le pourcentage de la ligne du Noyau monte d'un cran à chaque rang. |
| 4.8 | **Économie** | Monter « Code Polymorphe » à 9 / 10, puis à 10 / 10. | À 9, « Architecture Distribuée » reste verrouillé, lien en pointillé. À 10, il s'ouvre ; l'inspecteur annonce un coût de **2** CPU et « > TFlops nettes : +10 % par niveau ». « Code Polymorphe » : « > Hausse du prix à chaque niveau acheté : −1 point par niveau ». |
| 4.9 | **Chaînes au maximum** | Monter « Sortie répétée » à 4 / 5 puis 5 / 5 ; même chose plus bas jusqu'à « Extraction Haut Risque ». | Chaque nœud suivant ne s'ouvre qu'au 5 / 5 du précédent, jamais avant. |
| 4.10 | **Extraction** | Deux rangs de « Sortie répétée », run neuve. | Sur la jauge, les paliers annoncent **+30 %** (50 %) et **+60 %** (75 %) au lieu de +15 % et +30 %. Le 90 % reste « [VERROUILLÉ] » sans « Extraction Haut Risque ». |
| 4.11 | **Interception rabotée** | Inspecteur de « Relais fantômes », puis de « Nuage de témoins ». | « +100 % par niveau », puis « +150 % par niveau » (au lieu de +400 % et +1 500 %). |
| 4.12 | **La racine** | Cliquer « Noyau Compromis ». | « > Aucun effet : l'ancre de tout l'arbre ». Aucune erreur en console. |

## 0 quinquies. Refonte « Time Attack », lot 5 — le nettoyage

Livré le 2026-10-14, compilé (Unity, zéro erreur), catalogues régénérés, **jamais joué**. Le lot ne
change aucune règle : il retire du code, des données et des réglages morts. Ce qui se vérifie, c'est
que rien d'utile n'est parti avec.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 5.1 | **Sauvegarde d'un autre format** | Jeu fermé, dans `savegame.json`, passer `"Version"` à 12. Relancer depuis RootScene. | Le menu propose « Commencer » : partie neuve. Console : « [SAVE] Sauvegarde au format v12, binaire en v13 : aucune migration pendant le développement. Nouvelle partie créée. » Aucune erreur. |
| 5.2 | **Sauvegarde au format courant** | Jouer une minute, fermer, relancer, « Continuer ». | La partie reprend telle quelle : argent, niveaux, jauge, arbre. |
| 5.3 | **Les générateurs n'ont pas bougé** | Run neuve, regarder « Campagne de Phishing Basique » puis « Vieux PC Recyclé ». | Coûts 10 et 50 ; « Campagne de Phishing Basique » boucle son cycle en ~1,5 s au niveau 1, comme avant. |
| 5.4 | **L'écran de fin sans le bouton mort** | Finir une run. | Bilan, arbre et « [ NOUVELLE INTRUSION ] » en place ; aucun vide là où dormait l'ancien bouton « [ DÉPENSER — NOYAU IA ] ». Aucune clé brute affichée. |
| 5.5 | **Les palettes** | Options > Accessibilité, passer par les quatre modes de vision. | Les couleurs de l'arbre et de la jauge changent comme avant ; aucune erreur en console. |

## 0 sexies. Correctifs du backlog (2026-10-14)

Compilés (Unity et `dotnet build`, zéro erreur, **zéro warning**), **jamais joués**. Huit petits
défauts, testés en passant pendant la session de la refonte.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 6.1 | **Libellé du Hardware** | Onglet Hardware, « Vieux PC Recyclé » au niveau 1. | « Fournit 2 TFlops », et plus « Génère … Datas par cycle ». Les Scripts gardent « Génère X Datas toutes les Y s ». |
| 6.2 | **Pluriel du bouton de sortie** | Une run qui rapporte un seul point, puis plusieurs. | « (Cycles CPU : +1) », puis « (Cycles CPU : +3) » : juste dans les deux cas. |
| 6.3 | **Virgule partout** | Dépasser 1 000 Datas ; regarder l'en-tête, un coût au-delà de 1 000, la durée d'un Script, le répit d'un Proxy, une ligne d'Overclock en console. | Une virgule partout : « 1,234K », « 1,5 s », « +0,5 s ». Avant, les montants avaient un point (« 1.234K ») et les durées une virgule. |
| 6.4 | **Relance depuis l'écran de fin** | Finir une run, double-cliquer vite « [ NOUVELLE INTRUSION ] ». | Une seule run neuve, aucune erreur en console. |
| 6.5 | **Allers-retours de scène** | Échap, « Menu principal », puis « Continuer » ; recommencer deux fois. | Chaque transition aboutit ; aucune erreur en console. |
| 6.6 | **En-tête au démarrage** | Lancer une partie neuve. | Datas, TFlops, Cycles CPU et « + … /s » s'affichent dès l'arrivée en jeu, comme avant (leurs abonnements ont changé de place). |
| 6.7 | **Console qui tourne** | Laisser défiler plus de 30 lignes (achats, Overclocks). | Les plus anciennes disparaissent par le haut, la plus récente reste en bas, dans l'ordre. |

## 1. Fin de partie et économie — d'avant la refonte, jamais jouées

Tout ce bloc vient de `bf490c6` (2026-09-22), dont le message dit : « Rien de tout cela n'a été
éprouvé en Play Mode », et de `a772e67` (2026-10-02). C'est une session **« ressenti et bugs »** :
on regarde si ça marche et si ça se lit, pas si les chiffres sont bons. ⚠️ La refonte « Time
Attack » a réécrit le brouillard (lot 3, § 0 ter) ; il ne reste ici que la victoire, à rejouer.

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 1.12 | **Victoire : séquence et écran** — *14/10 : séquence OK, écran absent (objet `VictoryPanel` éteint dans la scène), corrigé le jour même* | Abaisser `_victoryTFlops` sous les TFlops courants (cf. « Avant de commencer »). | La console déroule la séquence (« Attaque du chiffrement… C'est un chat. »), puis l'écran « ACCÈS TOTAL » apparaît, textes résolus (aucune clé entre crochets), fond opaque qui capte les clics. La simulation **continue derrière** : la Trace monte. |
| 1.13 | **Victoire : fermeture** | Cliquer « Reprendre ». | L'écran se ferme, la partie continue où elle en était. |
| 1.14 | **Victoire : jamais deux fois** | Remettre `_victoryTFlops`, sauvegarder (retour au menu), relancer le jeu, revenir en jeu. | Aucune séquence ni écran de victoire au rechargement. (Vérifié hors Play Mode sur le code ; jamais sur le chemin complet.) |

## 2. Contrôles et réglages

Les touches, la pause et l'audio ont été éprouvés à la main le 2026-10-13 (`historique.md`).
Restent :

| # | Quoi | Comment | Ce qu'on doit voir |
|---|---|---|---|
| 2.8 | **Taille d'interface au-dessus de 100 %** — *14/10 : options et inspecteur de nœud se chevauchent au-delà de 120 % (`backlog.md` § 4)* | Options > Affichage > Taille d'interface à 110 %, puis 150 %. Parcourir le menu, les options, la pause, l'écran de jeu, l'arbre, l'écran de fin. | Rien ne se chevauche, rien ne sort de l'écran. ⚠️ Le panneau d'options se chevauche par construction dès 110 % (`backlog.md` § 4) ; les autres écrans n'ont jamais été regardés au-delà de 100 %. |

## 3. Uniquement dans un build

Tout ce bloc a été validé en build le 2026-10-13 (`historique.md`). Une ligne y reviendra si un
lot touche au démarrage, à la sauvegarde ou à l'affichage.

## 4. À confirmer à l'œil

Passé en revue le 2026-10-13 : les constats confirmés sont au backlog (§ 4), les autres sont
soldés dans `historique.md`. Rien en attente.

## 5. Plus tard — à tester quand le lot concerné sera livré

| Après | Quoi |
|---|---|
| Le test d'ensemble de la refonte (§ 0 à § 0 quinquies) | Session de jeu « chiffres » sur une campagne : durée des runs, points par run, part de l'arbre à la victoire, valeurs de l'Interception. Victoire remise à 1e12. |
| Steam | Lisibilité sur un écran 1280×800. Navigation complète à la manette, sans souris. Overlay Steam. Synchronisation cloud entre deux machines. |
| Traductions | **Langue à chaud** (ex-2.5) : basculer de langue en pleine partie, parcourir tous les écrans ; aucun texte ne doit rester dans l'ancienne langue, hors lignes déjà écrites dans la console. **Premier lancement** : l'écran de choix propose toutes les langues, celle du système présélectionnée. D'ici là, la technique du `zz.json` (chaque valeur de `fr.json` préfixée « ZZ· ») permet de traquer les textes en dur ; supprimer le fichier et sa `.meta` après. |
