# Backlog — défauts et manques connus

> Révisé entrée par entrée le **2026-10-13**, contre le code de `main` (`7b95a5a`), puis mis à jour
> le même jour par le lot 1 de la refonte « Time Attack ». Les entrées réglées sont descendues dans
> `historique.md`.
>
> | Pour savoir | Lire |
> |---|---|
> | dans quel ordre on traite tout ça | `etat-des-lieux.md` — feuille de route |
> | ce qui est livré mais jamais vu fonctionner | `a-tester.md` |
> | ce qui a déjà été réglé, et comment | `historique.md` |

**Chaque entrée porte sa nature — méthode à conserver :**

- 🔬 **vérifié en Play Mode** — le comportement a été observé, pas déduit
- 📖 **lecture de code ou de données** — constat statique, vrai par construction (lecteur absent,
  champ non lu, valeur dans un asset…)
- ⚖️ **estimation** — plausible mais non mesuré ; ne pas planifier un lot dessus sans mesurer

Un « X n'a aucun lecteur » est un constat de lecture. « La fonctionnalité est cassée » est un
constat de comportement, et demande le Play Mode. Ne pas confondre les deux : l'audit du 25/08
avait propagé une entrée fausse pour cette raison, et celui du 27/08 en a porté une autre six
semaines — les 30 s du Zéro-Day, devenues fausses par une refonte et jamais revérifiées.

**Règle de tenue.** Une entrée réglée descend dans `historique.md` le jour même, avec son commit.
Une entrée nouvelle porte sa date et sa nature. **Un point ouvert qui n'existe que dans un message
de commit, un commentaire de code ou une conversation n'existe pas** : le reporter ici. La révision
du 13/10 a trouvé des points ouverts consignés seulement dans un message de commit ou dans les
notes de session : le nœud de la barre d'espace, trois changements d'économie, tout le volet
Steam Deck.

État de la base au 2026-10-15, après la refonte « Time Attack » (lots 1 à 5) et sa passe de test :
114 fichiers runtime, 9 fichiers d'éditeur. `dotnet build` : **0 erreur, 0 warning** depuis les correctifs du
2026-10-14 — tout warning est donc nouveau.

---

## 0. Refonte « Time Attack » — ce qui reste ouvert

La feuille de route des lots est dans `etat-des-lieux.md`. Ici, les points ouverts.

- ⚖️ **Valeurs de l'Interception provisoires** (lot 4, 2026-10-14). `P_INTERCEPT_3` et `_4` passent
  de +4 et +15 à **+1,0 et +1,5 par rang** (×19,5 au max au lieu de ×102), point de départ choisi
  par Bertrand ; le GD fixera les valeurs définitives en jouant. Calcul, fin de partie Blindage
  max : `PRX_01` retire ~15 % de la jauge, `PRX_05` ~59 %, il faut `PRX_07` pour la vider.
- ⚖️ **La disposition de l'arbre en éventail est provisoire** (lot 4). Vue en jeu le 2026-10-15 :
  « ça fait un peu arbre de RPG, MAIS on va ajouter quelques autres nœuds » — la dispersion se
  décidera avec eux. **Ne rien changer d'ici là** (Bertrand). À zoom 1, l'écran ne montre que trois
  des sept colonnes ; il faut dézoomer à la molette.

## 1. Bloquant pour une sortie publique

Rien de tout cela n'empêche de jouer aujourd'hui. Aucun build public ne doit partir sans.

- 📖 **La victoire est réglée à 5e11, à titre PROVISOIRE.** `BalancingConfig.asset` porte
  `_victoryTFlops: 5e11`, le défaut du code et le GDD disent 1e12. Abaissée par le GD le
  2026-09-16 pour éprouver la fin de campagne sans grinder. À remonter après la session de jeu
  « chiffres ». Si on l'oublie, le jeu se termine à mi-chemin.
- 📖 **Le nom du jeu n'est pas fixé dans le projet.** `productName: HackTheKey`,
  `companyName: DeathtreakCorp`. Le dossier de sauvegarde en dérive
  (`%userprofile%\AppData\LocalLow\DeathtreakCorp\HackTheKey\`) : renommer après une démo publique
  rendrait leurs sauvegardes invisibles aux joueurs. **À trancher avant le premier build public.**
- 📖 **Aucune intégration Steam réelle.** `steam_appid.txt` vaut `480` (Spacewar, l'application
  de test de Valve) et `SteamManager` appelle `RestartAppIfNecessary(AppId_t.Invalid)`. Attend
  l'AppID, donc l'ouverture de la page Steam. Pour la sauvegarde cloud, voir § 5.
- 📖 **Aucune navigation clavier ni manette.** `GameSceneBootstrapper.ReleaseUiSelection` vide la
  sélection d'interface à chaque frame : c'est le correctif du conflit entre Espace (Overclock) et
  `UI/Submit` (cf. GDD, « La BARRE ESPACE »). Pour la manette, il faudra lier `UI/Submit`
  explicitement à Entrée et au bouton Sud, puis rendre la sélection. **Prérequis du badge Steam
  Deck Verified.** Inquiétude du GD (2026-10-02) : l'Overclock, martelé en début de partie,
  serait pénible à aller chercher au pad.
- 📖 **Aucune configuration Steam Input.** Elle se déclare côté Steamworks, pas dans le code. Sans
  elle, le Deck est injouable manette en main — motif de refus du badge Verified.
- ⚖️ **Lisibilité en 1280×800 jamais regardée.** `CanvasScaler` en Scale With Screen Size,
  référence 1920×1080, match 0,5 : facteur ≈ 0,72 sur Deck. Un texte prévu pour 20 px tombe à
  ~14 px sur un écran de 7 pouces.
- 📖 **Pas d'écran de crédits ni de mentions légales.** Les deux polices (ShareTechMono,
  LiberationSans) sont sous licence OFL, qui exige que la notice et la licence accompagnent
  chaque copie distribuée. Leurs fichiers `*-OFL.txt` dorment dans `Assets/TextMesh Pro/Fonts/` et
  ne partent pas dans le build. Les frappes Pixabay n'exigent pas d'attribution (créditer reste
  apprécié, cf. `audio-licences.md`).
- 📖 **Déclaration « contenu créé avec une IA »** au questionnaire Steam, tant que les sons
  synthétisés sont en jeu — et les traductions, si elles sont faites par IA comme prévu.
- 📖 **Traductions EN et ES.** Seul `fr.json` existe (316 clés). Les nombres suivent déjà la
  culture de la langue chargée (`ILocalizationService.Culture`, 2026-10-14) : un `en.json` suffira à
  passer au point décimal. Reportées à la fin du contenu,
  décision du 2026-10-01 : inutile de traduire un texte qui va encore bouger.

## 2. Du contenu qui s'achète et ne fait rien

Visible par le joueur : il dépense un point de prestige pour rien.

Rien à ce jour : les 15 nœuds « Opti Prod » des Proxies sont partis avec les 120 nœuds ciblés
(lot 4 de la refonte).

## 3. Équilibrage et données

Le détail chiffré et les chantiers d'équilibrage sont dans `etat-des-lieux.md`. Ici, les défauts.

- 📖 **Trois changements d'économie de `a772e67` (2026-10-02) n'avaient été consignés nulle part**
  — rattrapé dans le GDD le 2026-10-13 : modèle des boîtes pour les points, premier point
  abaissé de 5e6 à 2e6, saisie versant 30 % au lieu de rien. ⚖️ **Aucune campagne n'a été
  remesurée depuis** — voir la session « chiffres » de la feuille de route, dans `etat-des-lieux.md`.

## 4. Défauts d'affichage et de texte

- 🔬 **Les boutons du bas du panneau d'options chevauchent les réglages** — « [ PAR DÉFAUT ] » et
  « [ RETOUR ] ». Constaté par Bertrand le 2026-10-13, en build, en 3840×2160 plein écran ;
  reconfirmé le 2026-10-14 au-delà de 120 % de taille d'interface. ⚠️ **Contredit le 2026-10-15** :
  au 2.8 rejoué à 110 et 150 %, « semble ok partout ». Rien n'a été modifié dans le panneau entre
  les deux : à reconfirmer sur l'onglet **Audio** à 150 %, le plus haut des trois, quand toute
  l'interface sera définitive (`a-tester.md` § 5, décision de Bertrand).
  📖 **Cause probable, lue dans `OptionsPanel.prefab`** : le panneau mélange des ancrages
  proportionnels et des tailles fixes. `Pages` est ancré à 64 % de la hauteur avec une hauteur
  fixe de 460 (l'onglet Audio la remplit exactement : 6 lignes de 60 + 5 espaces de 20) ; les deux
  boutons sont ancrés à 15 %, hauteur 60. L'écart vaut `0,49 × H − 490` unités : **39 à 100 %**,
  et le chevauchement commence dès que la hauteur virtuelle du canvas passe sous 1000.
  Or la résolution ne la change pas — à 16:9 elle vaut toujours 1080, en 1080p comme en 4K —
  mais la **taille d'interface**, si : `UiScalePresenter` divise la résolution de référence, donc
  `H = 1080 / échelle`. Calculé : les boutons mordent sur la dernière ligne de l'onglet **Audio**
  dès **110 %**, sur celle de l'onglet **Affichage** (5 lignes) dès **130 %** ; l'onglet
  Accessibilité (2 lignes) ne chevauche jamais.
  ⚖️ À confirmer : la taille d'interface était-elle au-dessus de 100 % lors du constat ? Si non, la
  cause est ailleurs. **Le remède n'est pas qu'une question d'ancrage** : à 150 %, le canvas ne
  fait plus que 720 unités de haut, et le contenu du panneau (titre, onglets, 460 de réglages,
  boutons) n'y tient pas même parfaitement empilé. Il faudra choisir : panneau compact, défilement,
  ou plafond plus bas pour la taille d'interface. Les autres écrans ont été regardés à 110 et 150 %
  le 2026-10-15, sommairement : rien vu, vérification complète avec l'interface définitive.
- ⚖️ **L'inspecteur de nœud peut atteindre son bouton à grande taille d'interface.** Ses textes ne
  se chevauchent plus entre eux (2026-10-15) : ils s'empilent à taille fixe, mais la colonne ne
  défile pas. Mesuré dans l'éditeur, le nœud le plus bavard (« Extraction Haut Risque ») occupe
  616 unités pour 810 disponibles au-dessus du bouton ; la marge fond quand la taille d'interface
  monte. Rien vu au 2.8 sommaire du 2026-10-15 ; à revérifier avec l'interface définitive
  (`a-tester.md` § 5).
- 🔬 **Le trait des nœuds verrouillés n'est pas pointillé** comme sur la maquette (le sprite du
  cadre est plein ; constat du 2026-08-30). Les liens, eux, le sont depuis le 2026-08-30.
- 📖 **Des textes du jeu ne suivent pas le lore du 2026-10-16** (GDD, « Lore »). Relevé dans
  `fr.json` le jour même, puis relu en entier (337 clés, nœuds du lot C2 compris). À reprendre
  avec le tutoriel et les messages de la console, que ce lore doit nourrir (demande de Bertrand) ;
  on ne change que les valeurs, les clés restent :
  - **Contredisent le lore.** `UI_VICTORY_BODY` dit « L'A.M.I., elle, te cherche toujours » alors
    qu'elle vient d'être effacée : dire qu'elle se reconstruit. Il dit aussi « Tu le savais
    déjà » de la photo de chat, alors que le thriller garde le contenu de la clé secret jusqu'au
    bout. `PRESTIGE_P_STARTING_FUNDS_2_DESC` (lot C2) invente « un commanditaire anonyme » à côté
    du client anonyme : en faire le client, qui avance des fonds. `LOG_PURGE_5` compile le Noyau
    avec « les erreurs » : c'est désormais la paie du client. `UI_RUN_END_CLEAN_NARRATIVE` (« Ils
    fouillent un appartement vide ») suppose une planque trouvée, alors qu'une extraction se fait
    avant la triangulation.
  - **Manquent au récit.** La séquence de victoire (`LOG_VICTORY_1` à `_6`) ouvre la clé sans
    que le joueur retourne d'abord sa puissance contre l'A.M.I. `UI_RUN_END_SEIZED_NARRATIVE` ne
    dit pas qu'il s'enfuit avec la clé, sans quoi il ne pourrait pas la rebrancher. Le début de
    run (`LOG_NETWORK_CONNECTED`, `LOG_BREACH_STARTED`) ne parle ni de la clé, ni de l'A.M.I.
  - **Vocabulaire.** `LOG_PROXY_WIPE`, `UI_PRESTIGE_EFFECT_INTERCEPT` et `LOG_FIREWALL_ROUTINE`
    (lot C2) disent « Brouillage » ou « acheté » là où le lore dit qu'un proxy est « brûlé ». La
    sortie volontaire porte **quatre noms** : « WIPE SYSTÈME » (`UI_EXFIL_LOCKED`, `_READY`),
    « EFFACEMENT PROPRE » (`UI_RUN_END_CLEAN_NARRATIVE`), « Clean Exit », en anglais
    (`UI_RUN_END_STAT_CLEAN_BONUS`), et « Extraction » (paliers, nœuds, lore). Le tutoriel aura
    besoin d'un seul verbe : « Extraction », celui du lore.
  - **Mineur.** `PRESTIGE_P_EXTRACT_1` à `_3` racontent la fuite (trajet à blanc, tunnel) alors
    que la branche augmente la prime du client sur le bloc : parler d'assemblage ou de négociation.
  - **À garder, proposition à confirmer avec le GD.** `UPG_HW_20_DESC` (Singularité Contenue :
    « Elle tient dans une clé USB ») laisse croire qu'ARES est une IA — c'est la fausse piste qui
    fait tomber la photo de chat, et ce qui survit de l'« IA libre » écartée.
- 📖 **Le `CLAUDE.md` décrit l'A.M.I. comme « le système de sécurité du réseau »** (§ « Le projet
  en trois phrases ») ; le lore du 2026-10-16 en fait la sentinelle intégrée à la clé ARES.

## 5. Architecture et cycle de vie

- 📖 **La liste `autoInjectGameObjects` de `GameSceneLifetimeScope` se tient à la main.** Un
  `LocalizedText` oublié affiche sa clé brute, sans erreur ni warning. Deux cas trouvés le
  2026-09-22, corrigés le 2026-10-02 ; aucun contrôle automatique n'empêche le suivant. Consigne
  en vigueur : un texte statique nouveau est écrit par son presenter, pas par un `LocalizedText`.
- 📖 **`SteamCloudSaveService` et `SaveServiceComposite` compilent mais ne sont pas enregistrés**
  (TODO dans `RootLifetimeScope`), et le premier appelle `SteamManager.Initialized` en statique.
  **Piste à évaluer au lot Steam** : l'**Auto-Cloud** de Steamworks, réglé sur `savegame.json` seul
  (`settings.json` décrit la machine et ne doit pas voyager), synchronise sans une ligne de code
  — les deux classes deviendraient supprimables. Sinon, voir `persistence.md` § Steam.
- 📖 **Namespaces ≠ dossiers**, et deux dossiers pour la sauvegarde (`Services/Persistence` et
  `Services/Save`). Détail dans `architecture.md` § Dette structurelle.

## 6. Performance

Le jeu tourne sans ralentissement perceptible. Rien ici n'a été profilé.

- 📖 **`ILocalizationService.GetText<T0..T3>`** promet « Zero Boxing » mais passe par
  `string.Format(string, object…)`, qui boxe chaque type valeur (TODO dans
  `JsonLocalizationService`). ⚠️ La piste notée jusqu'ici, « `ZString.Format<T>`, déjà
  disponible avec UniTask », est fausse : ZString est un paquet distinct, **absent du projet**
  (vérifié le 2026-10-15 dans `Packages/manifest.json`). Et le remède devra formater avec
  `ILocalizationService.Culture` (règle du 2026-10-14) : vérifier qu'il accepte un
  `IFormatProvider` avant de l'adopter.
- ⚖️ **Repeinte de l'arbre** : 21 nœuds × 3 passes par achat de prestige, chacune avec un
  `Math.Pow` et une chaîne par nœud. Jamais mesuré, et probablement sans importance (achat rare,
  écran modal). Le remède, si besoin, est celui de `GeneratorPresenter` : cache et
  `DistinctUntilChanged`.

## 7. Outillage et qualité

- 📖 **Aucun `.asmdef`, aucun test automatisé.** Tout `Assets/Core` recompile avec le reste, et
  rien n'empêche une dépendance de `Models` vers `UI`. Proposition : `Ares.Core.Domain`,
  `Ares.Core.Presentation`, `Ares.Core.Editor`, prérequis aux tests EditMode. `UpgradeModel`,
  `PrestigeManager`, `ThreatManager`, `CurrencyFormatter` et `UserCurrencies` ne touchent presque
  pas Unity : c'est le meilleur retour sur investissement du projet. Le défaut corrigé dans
  `a772e67` (deux points et demi comptés pour trois) est exactement ce qu'un test attrape.

## 8. Accessibilité

- 📖 **Pas d'option « réduire les animations ».** Le jeu s'appuie sur le mouvement comme canal
  d'accessibilité : c'est la **pulsation** qui départage un nœud abordable d'un nœud trop cher
  (`architecture.md` § Palettes), ce qui gêne la sensibilité vestibulaire et la photosensibilité.
  L'option devra rendre un autre canal à ces nœuds, pas simplement tout figer.
- ⚖️ **Le joueur ne voit pas l'effet du mode daltonien au moment de le choisir.** Les couleurs qui
  changent n'apparaissent que dans l'arbre, sur la jauge en fin de run, ou sur des boutons encore
  verrouillés en début de partie — jamais derrière le menu d'options. Le GD lui-même, en testant
  le 2026-10-13, a conclu à « aucune différence ». Piste : une rangée de pastilles d'aperçu
  (abordable, trop cher, lien ouvert, palier incertain) dans l'onglet Accessibilité, repeinte à
  chaque changement de mode.

## 9. Décisions de game design en attente

Pas des bugs : des arbitrages que seul le GD peut rendre. Les trancher débloque l'entrée liée.

| Question | Entrée liée |
|---|---|
| Quand remonter la victoire à 1e12 ? | § 1 |
| L'écran méta masque la jauge de Trace pendant une run : ajouter un rappel de Trace dans son header ? | — |
| Brouillard sur les nœuds verrouillés : le nom reste visible (choix de Bertrand, pour planifier). À rediscuter si le GD tient au mystère. | — |
| Branche de prestige dédiée aux capteurs : la visibilité ne s'achète aujourd'hui qu'en Hardware (GDD, § Capteur). | — |
| Taille d'interface : jusqu'où la proposer ? Aujourd'hui 80 à 150 % ; à 150 % le canvas ne fait plus que 720 unités de haut, et le panneau d'options n'y tient pas. | § 4 |
| Le tutoriel : quelle forme (écran au premier lancement, run 1 guidée, messages de console) ? Le jeu n'en a aucun ; le GD veut la run 1 en « tutoriel déguisé » (cibles du 2026-10-15) ; le lore du 2026-10-16 lui fournit son pitch. | § 4 |

---

## Pièges à retenir

- **Le projet est sur le NOUVEL Input System.** `UnityEngine.Input` y lève une
  `InvalidOperationException` à chaque appel — une par frame si c'est dans un `Tick`. Utiliser
  `Keyboard.current`, et tester sa nullité : elle vaut null sans clavier connecté.
- **Unity n'appelle pas `Awake()` sur un objet inactif dans la hiérarchie.** Tout composant dont
  une méthode est appelée pendant la construction d'un panneau masqué doit résoudre ses
  références en **lazy**, jamais dans `Awake`. C'est ce qui avait cassé l'arbre de prestige, et
  ce qui laissait les générateurs des onglets masqués non injectés jusqu'au 2026-10-15 (ils
  naissent désormais injectés, par `IObjectResolver.Instantiate`).
- **Lancer le Play Mode depuis la GameScene est cassé par construction.** Sans `RootScene`, le
  scope de scène n'a pas de parent et aucun service n'est résoluble. Toujours partir de
  `Assets/Data/Scenes/RootScene.unity`. Depuis le 2026-10-15, `RootSceneGuard` le dit en une
  erreur `[BOOT]` et la scène reste inerte (GameScene comme MainMenuScene) ; avant, VContainer
  accusait un `LocalizedText` (« No such registration of type: ILocalizationService »), ce qui se
  lisait comme un bug de localisation.
- **Le `VictorySystem` voit chaque valeur intermédiaire des TFlops nettes**, pas seulement la
  valeur finale d'une opération. Tout ce qui les ABAISSE (le sacrifice du Data Wiper) se pose donc
  AVANT les générateurs et se retire APRÈS eux — au wipe de fin de run comme au chargement. Inverser
  l'ordre a déclenché une victoire à tort le 2026-10-15. Même vigilance pour tout futur malus.
- **Les valeurs de `PrestigeBonusType` s'appendent en FIN d'enum.** L'index est sérialisé dans les
  `.asset` : une insertion au milieu redéfinit silencieusement des nœuds existants. Déjà arrivé.
  Retirer une valeur n'est permis qu'en relançant **dans la foulée** le générateur de l'arbre,
  qui réécrit tous les `.asset` — fait le 2026-10-13 et le 2026-10-14 pour la refonte.
- **Le player loop de l'éditeur est figé tant que la fenêtre Unity n'a pas le focus**
  (`Time.frameCount` ne bouge pas), et `runInBackground` n'y change rien. Le Play Mode reste alors
  bloqué en transition. Pour vérifier un `ITickable` sans focus, appeler `Tick()` à la main ; pour
  le reste, mettre Unity au premier plan.
- **En éditeur et en build de développement, le bouton d'exfiltration reste cliquable même sans
  point** (`ExfiltrationSystem.BypassUnlockCondition`). L'état AFFICHÉ, lui, est le vrai. Le
  verrou « au moins un point » ne s'observe que dans un build de production.
