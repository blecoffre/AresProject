using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    public class GameOverView : MonoBehaviour
    {
        // La racine de l'écran n'appartient PLUS à cette vue.
        //
        // Depuis la fusion des deux panneaux, le bilan et l'arbre de prestige partagent le même
        // GameObject racine, et deux composants qui se disputent les mêmes SetActive finissent
        // toujours par se marcher dessus. C'est le PrestigePanelView qui possède l'écran et ses
        // deux modes ; cette vue-ci n'écrit plus que ses propres textes.
        [Header("UI References")]
        [Tooltip("L'alerte : « CONNEXION PERDUE… » ou « EFFACEMENT PROPRE ». Le ton change selon " +
                 "la façon dont la run s'est terminée.")]
        [SerializeField] private TextMeshProUGUI _narrativeText;

        [Tooltip("Le bilan chiffré, une ligne par statistique. Reçoit un texte déjà composé et " +
                 "multi-ligne : la vue n'assemble rien, elle affiche.")]
        [SerializeField] private TextMeshProUGUI _bilanText;

        [SerializeField] private Button _restartButton;

        [Header("Couleurs du titre")]
        [Tooltip("Saisie Fédérale — le joueur s'est fait prendre.")]
        [SerializeField] private Color _seizedColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Tooltip("Effacement Propre — il est sorti de lui-même, avec le bonus.")]
        [SerializeField] private Color _cleanExitColor = new Color(0.29f, 0.96f, 0.15f, 1f);

        public event Action OnRestartClicked;

        public Color SeizedColor => _seizedColor;
        public Color CleanExitColor => _cleanExitColor;

        private void Awake()
        {
            _restartButton.onClick.AddListener(RaiseRestart);
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveListener(RaiseRestart);
        }

        // Méthode nommée plutôt qu'une lambda : un retrait de lambda ne désabonne rien.
        private void RaiseRestart() => OnRestartClicked?.Invoke();

        /// <summary>
        /// Écrit le bilan. Le titre porte sa propre couleur : rouge pour une saisie, vert pour un
        /// effacement propre — le joueur doit savoir en un coup d'œil s'il s'est fait avoir ou
        /// s'il a bien joué, avant même d'avoir lu une ligne.
        ///
        /// N'ALLUME RIEN : c'est le PrestigePanelView qui bascule l'écran en mode fin de run,
        /// juste après. Écrire d'abord, montrer ensuite, jamais l'inverse — sinon le joueur voit
        /// une frame du bilan précédent.
        /// </summary>
        public void ShowGameOver(string narrativeMessage, string bilanMessage, Color titleColor)
        {
            if (_narrativeText != null)
            {
                _narrativeText.text = narrativeMessage;
                _narrativeText.color = titleColor;
            }

            if (_bilanText != null) _bilanText.text = bilanMessage;
        }
    }
}
