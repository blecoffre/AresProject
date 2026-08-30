using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    /// <summary>
    /// Fabrique le sprite de tiret utilisé par les liens verrouillés de l'arbre de prestige.
    ///
    /// Un utilitaire plutôt qu'un PNG déposé à la main : le rythme du pointillé est un réglage
    /// visuel qu'on voudra ajuster en voyant l'arbre, et deux constantes se relisent mieux qu'une
    /// image binaire. Régénérer écrase le fichier, rien d'autre.
    ///
    /// Le sprite est fait pour être TUILÉ le long de la ligne (Image.type = Tiled). Toutes ses
    /// lignes de pixels sont identiques : la répétition verticale est donc invisible, ce qui
    /// permet de faire varier l'épaisseur du trait sans jamais couper un tiret de travers.
    /// </summary>
    public static class DashedLineSpriteGenerator
    {
        private const string OutputPath = "Assets/Data/Sprites/DashedLine.png";

        /// <summary>Longueur du tiret, en pixels.</summary>
        private const int DashLength = 32;

        /// <summary>Longueur du vide entre deux tirets.</summary>
        private const int GapLength = 16;

        /// <summary>
        /// Hauteur de la texture. Quatre pixels et non un seul : une texture d'un pixel de haut
        /// se fait parfois maltraiter par les réglages d'import et le filtrage. Quatre coûtent
        /// autant et ne posent aucune question.
        /// </summary>
        private const int Height = 4;

        [MenuItem("Tools/Core/Générer le sprite de ligne pointillée")]
        public static void Generate()
        {
            int width = DashLength + GapLength;

            // Sans alpha, pas de vide entre les tirets : le format doit le porter.
            Texture2D texture = new Texture2D(width, Height, TextureFormat.RGBA32, mipChain: false);

            Color32 ink = new Color32(255, 255, 255, 255);
            Color32 empty = new Color32(255, 255, 255, 0);

            // Blanc partout, y compris dans le vide : c'est l'ALPHA qui creuse le pointillé. Un
            // pixel transparent noir se mettrait à baver en gris sur les bords dès qu'un filtrage
            // interpole entre deux pixels voisins.
            Color32[] pixels = new Color32[width * Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = x < DashLength ? ink : empty;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string directory = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings();

            Debug.Log(
                $"<color=green>[Sprite]</color> Ligne pointillée générée : {OutputPath} " +
                $"({width} × {Height} px, tiret {DashLength} / vide {GapLength}).");
        }

        /// <summary>
        /// Les réglages d'import comptent autant que les pixels. Un filtrage bilinéaire
        /// estomperait les bords du tiret, et une compression inventerait des demi-transparences
        /// là où on veut une coupure franche.
        /// </summary>
        private static void ApplyImportSettings()
        {
            TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Clamp et non Repeat : la répétition est produite par la géométrie du mode Tiled,
            // pas par l'échantillonnage. Laisser Repeat n'apporterait rien et masquerait un jour
            // un débordement d'UV au lieu de le rendre visible.
            importer.wrapMode = TextureWrapMode.Clamp;

            importer.SaveAndReimport();
        }
    }
}
