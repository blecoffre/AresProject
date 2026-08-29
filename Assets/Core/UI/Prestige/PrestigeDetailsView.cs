using System;
using TMPro;
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

        [Header("Couleurs d'état")]
        [Tooltip("Même code que les nœuds de l'arbre, tenu à part volontairement : l'inspecteur " +
                 "est du texte sur fond sombre, un nœud est une pastille — la teinte lisible " +
                 "n'est pas forcément la même, et il faut pouvoir les régler séparément.")]
        [SerializeField] private Color _lockedColor = new Color(0.200f, 0.333f, 0.200f, 1f);
        [SerializeField] private Color _tooExpensiveColor = new Color(1f, 0.200f, 0.200f, 1f);
        [SerializeField] private Color _affordableColor = new Color(1f, 0.600f, 0f, 1f);
        [SerializeField] private Color _inProgressColor = new Color(0.290f, 0.965f, 0.149f, 1f);
        [SerializeField] private Color _maxedColor = new Color(0.290f, 0.965f, 0.149f, 1f);

        public event Action OnBuyClicked;

        private void Awake()
        {
            _buyButton.onClick.AddListener(RaiseBuy);

            // Rien n'est sélectionné au premier affichage de l'écran.
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _buyButton.onClick.RemoveListener(RaiseBuy);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void RaiseBuy() => OnBuyClicked?.Invoke();

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
                case PrestigeNodeState.Locked: return _lockedColor;
                case PrestigeNodeState.TooExpensive: return _tooExpensiveColor;
                case PrestigeNodeState.Affordable: return _affordableColor;
                case PrestigeNodeState.InProgress: return _inProgressColor;
                default: return _maxedColor;
            }
        }
    }
}
