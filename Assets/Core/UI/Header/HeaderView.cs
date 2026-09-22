using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Header
{
    public class HeaderView : MonoBehaviour
    {
        /// <summary>
        /// Un palier d'extraction posé sur la jauge de Trace : un trait, et son libellé.
        ///
        /// Les paliers vivent sur la MÊME jauge que le danger, à dessein. C'est un seul et même
        /// arbitrage — « je pousse encore ou je sors ? » — et le séparer en deux instruments
        /// obligerait le joueur à quitter des yeux la chose qui le menace pour lire ce qu'elle
        /// lui rapporte.
        /// </summary>
        [Serializable]
        public sealed class TraceTierMarker
        {
            [Tooltip("La racine du marqueur. Ses ancres X sont réécrites pour le poser au seuil.")]
            [SerializeField] private RectTransform _root;

            [Tooltip("Le trait vertical posé sur la jauge.")]
            [SerializeField] private Image _tick;

            [Tooltip("Le libellé sous le trait : le seuil et ce qu'il rapporte.")]
            [SerializeField] private TextMeshProUGUI _label;

            /// <summary>
            /// Le libellé est FACULTATIF : sur une jauge étroite, trois libellés se chevauchent et
            /// la position du trait dit déjà le seuil. Le trait, lui, est le marqueur.
            /// </summary>
            public bool IsValid => _root != null && _tick != null;

            /// <summary>Pose le marqueur au seuil et écrit son libellé. Appelé rarement.</summary>
            public void Configure(float threshold, string label)
            {
                if (!IsValid) return;

                _root.anchorMin = new Vector2(threshold, _root.anchorMin.y);
                _root.anchorMax = new Vector2(threshold, _root.anchorMax.y);
                if (_label != null) _label.SetText(label);
            }

            /// <summary>Repeint le marqueur. Appelé à chaque frame : aucune allocation ici.</summary>
            public void Paint(Color color, float thickness)
            {
                if (!IsValid) return;

                _tick.color = color;
                if (_label != null) _label.color = color;

                Vector2 size = _tick.rectTransform.sizeDelta;
                if (!Mathf.Approximately(size.x, thickness))
                {
                    size.x = thickness;
                    _tick.rectTransform.sizeDelta = size;
                }
            }

            public void SetVisible(bool visible)
            {
                if (_root != null && _root.gameObject.activeSelf != visible)
                    _root.gameObject.SetActive(visible);
            }
        }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _computerPowerText;

        [Tooltip("L'unité affichée sous la capacité de calcul. Posée UNE fois au démarrage par " +
                 "le presenter, pas par un LocalizedText : la liste autoInjectGameObjects du " +
                 "scope de scène s'entretient à la main, et tout objet oublié dedans affiche sa " +
                 "clé brute à l'écran sans que rien ne le signale.")]
        [SerializeField] private TextMeshProUGUI _computerPowerUnitText;

        [Tooltip("Le nom de la monnaie de Cycles, sous sa valeur. Même raison que ci-dessus : " +
                 "il portait un LocalizedText sur la case d'ICÔNE, et « Cycles » — six " +
                 "caractères — s'écrasait sur le montant.")]
        [SerializeField] private TextMeshProUGUI _cpuCyclesUnitText;
        [SerializeField] private TextMeshProUGUI _datasYieldText;
        [SerializeField] private TextMeshProUGUI _cpuCyclesText;

        [Header("Threat Gauge")]
        [SerializeField] private TextMeshProUGUI _traceText;
        [SerializeField] private Image _traceGaugeFill; // La nouvelle jauge visuelle

        [Tooltip("Zone d'incertitude : où la Trace peut se trouver AU PIRE depuis le dernier " +
                 "relevé. À placer DERRIÈRE la jauge principale, en teinte atténuée. " +
                 "Facultatif — tant qu'il est vide, la jauge se contente de se figer.")]
        [SerializeField] private Image _traceGaugeBand;
        [SerializeField] private Gradient _gaugeColorGradient; // Pour passer du vert au rouge

        [Header("Paliers d'extraction")]
        [Tooltip("Un marqueur par palier, dans l'ordre du BalancingConfig. Facultatif : tant " +
                 "qu'aucun n'est câblé, la jauge se comporte comme avant.")]
        [SerializeField] private TraceTierMarker[] _traceTierMarkers;

        [Tooltip("Palier FRANCHI : le relevé confirmé l'a dépassé, le bonus est acquis.")]
        [SerializeField] private Color _tierReachedColor = new Color(0.29f, 0.96f, 0.15f, 1f);

        [Tooltip("Palier INCERTAIN : la zone d'incertitude l'enjambe. Le joueur ne sait pas de " +
                 "quel côté il se trouve — c'est là que se joue « je tente ou pas ».")]
        [SerializeField] private Color _tierUncertainColor = new Color(0.94f, 0.63f, 0.15f, 1f);

        [Tooltip("Palier À VENIR : hors de portée du relevé comme de la fourchette.")]
        [SerializeField] private Color _tierUpcomingColor = new Color(0.24f, 0.35f, 0.24f, 1f);

        [Tooltip("Palier VERROUILLÉ : il attend son nœud de prestige.")]
        [SerializeField] private Color _tierLockedColor = new Color(0.60f, 0.60f, 0.60f, 1f);

        [Header("Layout Reference")]
        [SerializeField] private RectTransform _headerContainer; // Le RectTransform parent qui contient le Layout Group

        private bool _isInitialized = false;

        // État des paliers, gardé ici plutôt que recomposé à chaque frame : seuls le seuil et le
        // verrou décident de la couleur, et ils ne bougent qu'à l'achat d'un nœud de prestige.
        private float[] _tierThresholds;
        private bool[] _tierLocked;
        private int _tierCount;

        private float _lastKnownFraction;
        private float _estimatedMaxFraction;

        /// <summary>
        /// La vue n'ASSEMBLE plus de texte : elle pose ce que le presenter lui donne.
        ///
        /// Elle composait « : {valeur} TFlops » — un libellé en dur, donc intraduisible, et une
        /// chaîne réallouée à chaque tick de production. L'unité est devenue un libellé statique
        /// à côté de la valeur, et le « : » a disparu avec lui : l'icône de monnaie est un
        /// élément de layout à part entière, le séparateur ne séparait plus rien.
        /// </summary>
        public void UpdateMoneyDisplay(string formattedValue)
        {
            if (_moneyText != null) _moneyText.SetText(formattedValue);
            RefreshLayoutIfNeeded();
        }

        public void UpdateComputerPowerDisplay(string formattedValue)
        {
            if (_computerPowerText != null) _computerPowerText.SetText(formattedValue);
            RefreshLayoutIfNeeded();
        }

        /// <summary>
        /// Écrit l'unité des TFlops. Appelée UNE fois au démarrage : l'unité ne change qu'avec la
        /// langue, et la partie ne la change pas en cours de route.
        /// </summary>
        public void SetComputerPowerUnit(string label)
        {
            if (_computerPowerUnitText != null) _computerPowerUnitText.SetText(label);
        }

        /// <summary>Écrit le nom de la monnaie de Cycles. Appelée UNE fois au démarrage.</summary>
        public void SetCpuCyclesUnit(string label)
        {
            if (_cpuCyclesUnitText != null) _cpuCyclesUnitText.SetText(label);
        }

        /// <summary>Le « +…/s » est composé et localisé en amont : ici, on pose.</summary>
        public void UpdateMoneyYieldDisplay(string formattedYield)
        {
            if (_datasYieldText != null) _datasYieldText.SetText(formattedYield);
            RefreshLayoutIfNeeded();
        }

        public void UpdateCpuCyclesDisplay(string formattedValue)
        {
            if (_cpuCyclesText != null) _cpuCyclesText.SetText(formattedValue);
            RefreshLayoutIfNeeded();
        }

        /// <summary>
        /// Peint la dernière position CONNUE de la Trace. Le libellé est composé par le presenter
        /// — c'est lui qui a la localisation — et porte l'âge du relevé : une jauge qui se fige
        /// sans dire pourquoi se lit comme un bug avant de se lire comme une menace.
        /// </summary>
        public void UpdateTraceDisplay(string label, float lastKnownFraction)
        {
            if (_traceText != null)
            {
                _traceText.SetText(label);
            }

            _lastKnownFraction = lastKnownFraction;

            if (_traceGaugeFill != null)
            {
                _traceGaugeFill.fillAmount = lastKnownFraction;

                // Bonus visuel : la couleur change dynamiquement selon le remplissage
                if (_gaugeColorGradient != null)
                {
                    _traceGaugeFill.color = _gaugeColorGradient.Evaluate(lastKnownFraction);
                }
            }

            RefreshTierMarkers();
            RefreshLayoutIfNeeded();
        }

        /// <summary>
        /// Pose les paliers sur la jauge : leur seuil, et le libellé déjà composé et localisé par
        /// le presenter. Appelée au démarrage et à chaque achat de prestige — jamais par frame,
        /// car composer ces libellés alloue.
        ///
        /// Les marqueurs en trop sont éteints plutôt que détruits : le nombre de paliers est un
        /// réglage, et il peut redescendre.
        /// </summary>
        public void ConfigureTraceTiers(float[] thresholds, string[] labels, bool[] locked)
        {
            if (_traceTierMarkers == null || _traceTierMarkers.Length == 0) return;
            if (thresholds == null || labels == null || locked == null) return;

            _tierThresholds = thresholds;
            _tierLocked = locked;
            _tierCount = Mathf.Min(thresholds.Length, _traceTierMarkers.Length);

            for (int i = 0; i < _traceTierMarkers.Length; i++)
            {
                bool used = i < _tierCount;
                _traceTierMarkers[i].SetVisible(used);
                if (used) _traceTierMarkers[i].Configure(thresholds[i], labels[i]);
            }

            RefreshTierMarkers();
        }

        /// <summary>
        /// Repeint les marqueurs selon la position du relevé et l'étendue de la fourchette.
        ///
        /// Appelée à CHAQUE FRAME depuis <see cref="UpdateTraceBand"/> : pas une allocation, pas
        /// une closure, pas de chaîne composée. La pulsation du palier incertain se lit dans
        /// <c>Time.unscaledTime</c> plutôt que dans un Update dédié — la frame est déjà payée.
        /// </summary>
        private void RefreshTierMarkers()
        {
            if (_tierCount <= 0 || _traceTierMarkers == null) return;

            // Pulsation lente entre la teinte et une version plus vive. Elle ne sert qu'au palier
            // que la fourchette enjambe : c'est le seul dont le joueur ignore le côté.
            float pulse = 0.65f + 0.35f * Mathf.PingPong(Time.unscaledTime * 1.4f, 1f);

            for (int i = 0; i < _tierCount; i++)
            {
                float threshold = _tierThresholds[i];
                Color color;
                float thickness = 2f;

                if (_tierLocked[i])
                {
                    color = _tierLockedColor;
                }
                else if (_lastKnownFraction >= threshold)
                {
                    color = _tierReachedColor;
                }
                else if (_estimatedMaxFraction >= threshold)
                {
                    // La vérité est quelque part entre le relevé et le bord de la fourchette :
                    // ce palier est peut-être déjà acquis, peut-être pas.
                    color = _tierUncertainColor * pulse;
                    color.a = 1f;
                    thickness = 3f;
                }
                else
                {
                    color = _tierUpcomingColor;
                }

                _traceTierMarkers[i].Paint(color, thickness);
            }
        }

        /// <summary>
        /// Étend la zone d'incertitude. Appelée à chaque frame, contrairement au relevé :
        /// aucune allocation ici, et pas de RefreshLayoutIfNeeded — seule une largeur bouge.
        /// </summary>
        public void UpdateTraceBand(float estimatedMaxFraction)
        {
            _estimatedMaxFraction = estimatedMaxFraction;

            if (_traceGaugeBand != null) _traceGaugeBand.fillAmount = estimatedMaxFraction;

            RefreshTierMarkers();
        }

        /// <summary>
        /// Recalcule la géométrie du Header à la première mise à jour pour éviter la superposition.
        /// </summary>
        private void RefreshLayoutIfNeeded()
        {
            // On ne force le rebuild qu'une seule fois au démarrage pour préserver le CPU
            if (!_isInitialized)
            {
                _isInitialized = true;

                if (_headerContainer != null)
                {
                    // Force uGUI à calculer les largeurs de texte TMP et réagencer les éléments
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_headerContainer);
                }
            }
        }
    }
}