using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.Game
{
    /// <summary>
    /// L'écran de victoire. Vue autonome : elle possède sa propre racine et ne partage rien avec
    /// l'écran de fin de run.
    ///
    /// <b>Pourquoi elle ne réutilise pas <see cref="GameOverView"/>.</b> La victoire n'est pas une
    /// fin de run — la partie continue derrière. Emprunter l'écran de bilan, que
    /// <c>PrestigePanelView</c> possède et bascule en mode fin de run, ferait croire au joueur que
    /// sa run s'est arrêtée, et ferait se disputer deux composants pour le même SetActive. C'est
    /// exactement l'erreur que le commentaire en tête de GameOverView raconte.
    ///
    /// <b>Le composant vit sur un objet TOUJOURS actif</b>, et c'est <c>_panelRoot</c> qu'il
    /// allume. Un MonoBehaviour posé sur une racine éteinte ne voit jamais son Awake s'exécuter :
    /// ses listeners ne seraient jamais branchés, et le bouton de fermeture resterait mort.
    /// </summary>
    public sealed class VictoryView : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Le panneau à allumer. PAS la racine de cette vue : celle-ci doit rester active " +
                 "en permanence pour qu'Awake branche le bouton.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("Le titre : « ACCÈS TOTAL ». Reçoit un texte déjà localisé.")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Tooltip("Le corps du message, déjà composé et localisé. La vue n'assemble rien.")]
        [SerializeField] private TextMeshProUGUI _bodyText;

        [Tooltip("Referme l'écran et rend la main. La partie n'a jamais cessé de tourner derrière.")]
        [SerializeField] private Button _closeButton;

        public event Action OnCloseClicked;

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(RaiseClose);

            // L'écran ne s'affiche qu'au franchissement du seuil. Le laisser allumé dans la scène
            // le ferait apparaître dès le chargement de la partie.
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(RaiseClose);
        }

        // Méthode nommée plutôt qu'une lambda : retirer une lambda ne désabonne rien.
        private void RaiseClose() => OnCloseClicked?.Invoke();

        /// <summary>
        /// Écrit les textes PUIS allume le panneau. Jamais l'inverse : le joueur verrait une frame
        /// du contenu précédent — ici, celui d'une victoire vide.
        /// </summary>
        public void Show(string title, string body)
        {
            if (_titleText != null) _titleText.text = title;
            if (_bodyText != null) _bodyText.text = body;
            if (_panelRoot != null) _panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }
    }
}
