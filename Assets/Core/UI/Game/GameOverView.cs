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
        [SerializeField] private TextMeshProUGUI _narrativeText; // L'alerte : "CONNEXION PERDUE..."
        [SerializeField] private TextMeshProUGUI _bilanText;     // Le gain : "+X CPU Cycles"
        [SerializeField] private Button _restartButton;

        public event Action OnRestartClicked;

        private void Awake()
        {
            _panel.SetActive(false);
            _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveAllListeners();
        }

        // On reçoit désormais les deux textes séparément
        public void ShowGameOver(string narrativeMessage, string bilanMessage)
        {
            if (_narrativeText != null) _narrativeText.text = narrativeMessage;
            if (_bilanText != null) _bilanText.text = bilanMessage;

            _panel.SetActive(true);
        }
    }
}