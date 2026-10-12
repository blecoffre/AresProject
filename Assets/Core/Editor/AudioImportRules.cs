using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Réglages d'import imposés par dossier : un son déposé au bon endroit est réglé correctement
    /// sans passer par l'inspecteur. Ces règles écrasent toute retouche manuelle à chaque import —
    /// pour une exception, changer de dossier plutôt que de régler à la main.
    ///
    /// <list type="bullet">
    /// <item><b>Data/Audio/Button/</b> — sons d'interface, très courts et joués en rafale :
    /// décompressés au chargement (aucun coût CPU au clic), ADPCM (4× plus léger que le PCM,
    /// sans perte audible sur un bruit de touche), mono.</item>
    /// <item><b>Data/Audio/Events/</b> — sons d'événements synthétiques (saisie, paliers, refus…) :
    /// décompressés au chargement, PCM non compressé, mono. Pas d'ADPCM ici : sur un son tonal pur,
    /// son bruit de quantification s'entend comme un souffle. Ce sont quelques secondes en tout.</item>
    /// <item><b>Data/Audio/Music/</b> — musiques, longues : lues en streaming depuis le disque au
    /// lieu d'occuper la mémoire en entier, Vorbis.</item>
    /// </list>
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private const string UiFolder = "Assets/Data/Audio/Button/";
        private const string EventsFolder = "Assets/Data/Audio/Events/";
        private const string MusicFolder = "Assets/Data/Audio/Music/";

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;

            if (assetPath.StartsWith(UiFolder))
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.ADPCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = true,
                };
            }
            else if (assetPath.StartsWith(EventsFolder))
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad,
                    compressionFormat = AudioCompressionFormat.PCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = true,
                };
            }
            else if (assetPath.StartsWith(MusicFolder))
            {
                importer.forceToMono = false;
                importer.loadInBackground = true;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.Streaming,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.7f,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                    preloadAudioData = false,
                };
            }
        }
    }
}
