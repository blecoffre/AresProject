using System;
using TMPro;
using Core.UI.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Prestige
{
    /// <summary>
    /// L'inspecteur de l'arbre : ce qu'est le nœud sélectionné, et ce qu'il faudrait pour le
    /// compiler. Deux blocs, comme la maquette — l'identité d'un côté, l'état de l'autre.
    /// </summary>
    public class PrestigeDetailsView : MonoBehaviour
    {
        [Header("Racine")]
        [Tooltip("Masquée tant qu'aucun nœud n'est sélectionné.")]
        [SerializeField] private GameObject _root;

        [Header("Bloc identité")]
        [SerializeField] private TextMeshProUGUI _nameText;

        [Tooltip("Le lore du nœud.")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

        [Tooltip("L'effet chiffré, par niveau, et le niveau actuel.")]
        [SerializeField] private TextMeshProUGUI _effectsText;

        [Header("Bloc état")]
        [SerializeField] private TextMeshProUGUI _prerequisiteText;
        [SerializeField] private TextMeshProUGUI _costText;

        [Tooltip("La ligne d'avertissement : consultation seule pendant une run. Vide sinon.")]
        [SerializeField] private TextMeshProUGUI _noticeText;

        [Tooltip("Facultatif : le cadre du bloc état, teinté à la couleur de la situation.")]
        [SerializeField] private Image _statusFrame;

        [Header("Action")]
        [SerializeField] private Button _buyButton;
        [SerializeField] private TextMeshProUGUI _buyLabel;

        // Couleurs d'état : champs Inspector* de la palette, tenus à part de ceux des nœuds —
        // l'inspecteur est du texte sur fond sombre, un nœud est une pastille, la teinte lisible
        // n'est pas forcément la même (voir SetPalette).
        private UiPaletteSO _palette;

        public event Action OnBuyClicked;

        private void Awake()
        {
            _buyButton.onClick.AddListener(RaiseBuy);

            // SURTOUT PAS de `_root.SetActive(false)` ici.
            //
            // `_root` est le GameObject qui porte ce composant. Il démarre inactif, donc Unity
            // diffère cet Awake jusqu'à sa première activation — c'est-à-dire jusqu'au premier
            // RenderIdentity(). Le masquage se serait donc exécuté DANS l'activation qu'il était
            // censé précéder : le premier nœud cliqué n'affichait rien, et il fallait en cliquer
            // un second pour que l'inspecteur apparaisse enfin.
            //
            // Le masquage initial appartient au presenter, qui appelle Clear() au démarrage.
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveListener(RaiseBuy);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void RaiseBuy() => OnBuyClicked?.Invoke();

        /// <summary>
        /// Couleurs d'état du mode de vision courant (daltonisme). Posée par le presenter AVANT
        /// le premier rendu, puis à chaque changement d'option — suivi d'un nouveau rendu.
        /// </summary>
        public void SetPalette(UiPaletteSO palette) => _palette = palette;

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// Le bloc d'identité : qui est ce nœud, ce qu'il fait, où en est le joueur dessus.
        /// Séparé du bloc d'état pour épouser les deux encadrés de la maquette — les deux se
        /// repeignent ensemble, mais ils ne répondent pas à la même question.
        /// </summary>
        public void RenderIdentity(string name, string description, string effects)
        {
            if (_root != null) _root.SetActive(true);

            if (_nameText != null) _nameText.text = name;
            if (_descriptionText != null) _descriptionText.text = description;
            if (_effectsText != null) _effectsText.text = effects;
        }

        /// <summary>
        /// Le bloc qui vit : prérequis, coût, avertissement éventuel, et l'état du bouton.
        /// </summary>
        /// <param name="notice">Chaîne vide pour masquer la ligne d'avertissement.</param>
        /// <param name="buyLabel">
        /// Porte aussi le REFUS : « fonds insuffisants », « verrouillé », « hors run ». Un bouton
        /// grisé sans explication laisse le joueur chercher pourquoi.
        /// </param>
        public void RenderStatus(string prerequisite, string cost, string notice, string buyLabel, bool canBuy, PrestigeNodeState state)
        {
            Color stateColor = ResolveStateColor(state);

            if (_prerequisiteText != null)
            {
                _prerequisiteText.text = prerequisite;
                _prerequisiteText.color = stateColor;
            }

            if (_costText != null)
            {
                _costText.text = cost;
                _costText.color = stateColor;
            }

            if (_noticeText != null)
            {
                _noticeText.text = notice;
                _noticeText.gameObject.SetActive(!string.IsNullOrEmpty(notice));
            }

            if (_statusFrame != null) _statusFrame.color = stateColor;

            if (_buyLabel != null) _buyLabel.text = buyLabel;
            _buyButton.interactable = canBuy;
        }

        private Color ResolveStateColor(PrestigeNodeState state)
        {
            switch (state)
            {
                case PrestigeNodeState.Locked: return _palette.InspectorLocked;
                case PrestigeNodeState.TooExpensive: return _palette.InspectorTooExpensive;
                case PrestigeNodeState.Affordable: return _palette.InspectorAffordable;
                case PrestigeNodeState.InProgress: return _palette.InspectorInProgress;
                default: return _palette.InspectorMaxed;
            }
        }
    }
}
