# Registre des licences audio

Tout fichier audio ajouté au projet est inscrit ici **au moment où il entre dans le dépôt**. Le jeu est vendu sur Steam : une licence non commerciale (« NC ») est exclue, sans exception.

| Fichier (dans `Assets/`) | Usage | Source (URL) | Auteur | Licence | Attribution exigée |
|---|---|---|---|---|---|
| `Data/Audio/Button/Typing/ui_key_b_*.wav` (36) | Famille `KeyPress` | [Pixabay — « Keyboard Typing »](https://pixabay.com/fr/sound-effects/la-technologie-keyboard-typing-5997/), publié le 2021-08-05 par le compte freesound_community (republication depuis Freesound) | Trollarch2 (Freesound) | Licence de contenu Pixabay (usage commercial gratuit, modification autorisée) | Non, confirmé par le résumé de la licence (créditer Trollarch2 reste apprécié) |
| `Data/Audio/Button/Validation/ui_validation_*.wav` (5) | Famille `Validation` | Dérivés de la même prise (frappes les plus graves, ralenties, impact grave ajouté) | idem | idem | idem |
| `Data/Audio/Button/_Archive/MacBook/ui_key_-*.wav` (14) | Archivés, hors de toute famille : **non inclus dans le jeu** (aucune famille ne les référence) | Prise « keyboard typing and keystrokes, Apple MacBook Pro 2018 » — source à relever **avant** de les réutiliser | — | — | — |
| `Data/Audio/Events/sfx_*.wav` (11) | Refus, paliers de Trace, saisie, victoire, prestige, exfiltration, Data Wiper, Zéro-Day | Synthétisés par `tools/audio/synth_sfx.py` (2026-10-12) | Projet Ares | Propriété du projet | Non |
| `Data/Audio/Music/music_*_placeholder.wav` (2) | Musiques menu et jeu, **bouche-trou** | Synthétisées par `tools/audio/synth_music.py` (2026-10-12) | Projet Ares | Propriété du projet | Non |

**Contenu produit avec une IA.** Les sons synthétisés l'ont été par des scripts écrits par Claude (Anthropic). Steam demande de déclarer le contenu créé avec l'aide d'outils d'IA : par prudence, le mentionner dans le questionnaire de la page du jeu tant que ces sons y sont.

## Rappels

- **CC0** : rien à faire. **CC-BY** : le nom de l'auteur doit figurer dans les crédits du jeu — un écran de crédits deviendra alors nécessaire.
- **Freesound, OpenGameArt** : la licence est propre à chaque fichier, pas au site. La relever fichier par fichier.
- **Musique** : vérifier qu'elle n'est pas déposée sur le Content ID de YouTube (réclamations contre les streamers qui joueraient au jeu).
- Conserver une copie de la page de licence (capture ou PDF) quand la source peut disparaître.
- **Pixabay** : interdit de vendre ou distribuer un son « tel quel », sans apport créatif. Intégré au jeu (et ici découpé, nettoyé, retravaillé), c'est autorisé. En revanche, ne jamais publier ces frappes **à part** : pas de pack de sons, pas de DLC « bande-son » ou d'archive d'assets qui les contiendrait.
