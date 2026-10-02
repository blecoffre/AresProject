using System;

namespace Core.Models.Settings
{
    /// <summary>Les deux modes proposés. Pas de plein écran exclusif : sous Proton (Steam Deck),
    /// le fenêtré sans bordure est le chemin le plus sûr, et l'exclusif n'apporte rien à une
    /// interface 2D.</summary>
    public enum DisplayModeSetting
    {
        Windowed = 0,
        Fullscreen = 1,
    }

    /// <summary>
    /// Mode de vision des couleurs. Une palette dédiée par type de daltonisme : chacune fait
    /// mieux qu'une palette « rouge-vert » commune, et un joueur daltonien connaît son type.
    /// </summary>
    public enum ColorVisionMode
    {
        Standard = 0,
        Protanopia = 1,
        Deuteranopia = 2,
        Tritanopia = 3,
    }

    /// <summary>
    /// Les réglages du joueur, tels qu'écrits dans <c>settings.json</c>.
    ///
    /// <b>Séparés de <c>SaveData</c>, à dessein.</b> Une résolution ou un mode d'affichage
    /// décrivent une MACHINE, pas une progression : ils ne doivent ni voyager avec la sauvegarde
    /// via Steam Cloud, ni disparaître avec elle si le joueur repart de zéro.
    ///
    /// Champs publics pour <c>JsonUtility</c>. Un champ absent du fichier garde la valeur
    /// d'initialisation ci-dessous : ajouter un réglage ne casse donc pas un fichier existant.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public int Version = 1;

        public DisplayModeSetting DisplayMode = DisplayModeSetting.Windowed;
        public int ResolutionWidth = 1280;
        public int ResolutionHeight = 720;

        public bool VSync;
        public int FrameRateCap = 60;

        /// <summary>En pourcentage entier, pas en float : 110 reste 110 après dix allers-retours,
        /// là où 1.1f accumulerait des erreurs d'arrondi à chaque pas.</summary>
        public int UiScalePercent = 100;

        /// <summary>
        /// Code de langue (« fr », « en »…). Vide tant que le joueur n'a pas choisi : c'est ce
        /// vide qui déclenche l'écran de choix au premier lancement. La langue du système n'est
        /// donc JAMAIS écrite ici d'office — seulement proposée.
        /// </summary>
        public string Language = string.Empty;

        public ColorVisionMode ColorVision = ColorVisionMode.Standard;

        // Audio. Deux canaux, chacun avec un volume et un interrupteur INDÉPENDANTS : couper la
        // musique ne doit pas faire oublier le volume choisi, pour le retrouver en la rallumant.
        /// <summary>Coupe tout le son (groupe Master), sans toucher aux réglages des canaux.</summary>
        public bool MasterMuted;

        /// <summary>
        /// Le son continue-t-il fenêtre inactive. Oui par défaut, dans la logique du jeu : la
        /// partie tourne en arrière-plan (runInBackground), et l'A.M.I. avec elle.
        /// </summary>
        public bool AudioInBackground = true;

        public int MusicVolumePercent = 80;
        public bool MusicMuted;
        public int SfxVolumePercent = 100;
        public bool SfxMuted;
    }
}
