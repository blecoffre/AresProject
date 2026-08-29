using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    public class GameOverView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;

        [Tooltip("L'alerte : « CONNEXION PERDUE… » ou « EFFACEMENT PROPRE ». Le ton change selon " +
                 "la façon dont la run s'est terminée.")]
        [SerializeField] private TextMeshProUGUI _narrativeText;

        [Tooltip("Le bilan chiffré, une ligne par statistique. Reçoit un texte déjà composé et " +
                 "multi-ligne : la vue n'assemble rien, elle affiche.")]
        [SerializeField] private TextMeshProUGUI _bilanText;

        [SerializeField] private Button _restartButton;

        [Tooltip("Facultatif : ouvre l'arbre de prestige depuis l'écran de fin. C'est le moment " +
                 "naturel pour dépenser ce qu'on vient de gagner.")]
        [SerializeField] private Button _prestigeButton;

        [Header("Couleurs du titre")]
        [Tooltip("Saisie Fédérale — le joueur s'est fait prendre.")]
        [SerializeField] private Color _seizedColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Effacement Propre — il est sorti de lui-même, avec le bonus.")]
        [SerializeField] private Color _cleanExitColor = new Color(0.29f, 0.96f, 0.15f, 1f);

        public event Action OnRestartClicked;
        public event Action OnPrestigeClicked;

        public Color SeizedColor => _seizedColor;
        public Color CleanExitColor => _cleanExitColor;

        private void Awake()
        {
            _panel.SetActive(false);
            _restartButton.onClick.AddListener(RaiseRestart);
            if (_prestigeButton != null) _prestigeButton.onClick.AddListener(RaisePrestige);
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveListener(RaiseRestart);
            if (_prestigeButton != null) _prestigeButton.onClick.RemoveListener(RaisePrestige);
        }

        // Méthodes nommées plutôt que des lambdas : un retrait de lambda ne désabonne rien.
        private void RaiseRestart() => OnRestartClicked?.Invoke();
        private void RaisePrestige() => OnPrestigeClicked?.Invoke();

        /// <summary>
        /// Affiche l'écran de fin. Le titre porte sa propre couleur : rouge pour une saisie, vert
        /// pour un effacement propre — le joueur doit savoir en un coup d'œil s'il s'est fait
        /// avoir ou s'il a bien joué, avant même d'avoir lu une ligne.
        /// </summary>
        public void ShowGameOver(string narrativeMessage, string bilanMessage, Color titleColor)
        {
            if (_narrativeText != null)
            {
                _narrativeText.text = narrativeMessage;
                _narrativeText.color = titleColor;
            }

            if (_bilanText != null) _bilanText.text = bilanMessage;

            _panel.SetActive(true);
        }

        public void Hide()
        {
            _panel.SetActive(false);
        }
    }
}
