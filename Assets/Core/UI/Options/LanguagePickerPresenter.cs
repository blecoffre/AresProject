using Core.Services.Localization;
using Core.Services.Settings;
using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace Core.UI.Options
{
    /// <summary>
    /// Premier lancement : propose les langues, la langue du système présélectionnée.
    ///
    /// Cliquer une langue l'APPLIQUE aussitôt, à titre d'aperçu : le joueur lit l'écran dans la
    /// langue qu'il s'apprête à valider, ce qui est la meilleure confirmation possible. Rien
    /// n'est mémorisé avant « Confirmer » — fermer le jeu sur cet écran le fera réapparaître au
    /// lancement suivant.
    ///
    /// L'écran ne revient plus jamais une fois validé ; la langue se change ensuite dans les
    /// options.
    /// </summary>
    public class LanguagePickerPresenter : IStartable, IDisposable
    {
        private readonly LanguagePickerView _view;
        private readonly ILocalizationService _loc;
        private readonly ISettingsService _settings;

        private readonly List<string> _languages = new();

        private CancellationTokenSource _cts;
        private IDisposable _languageSubscription;
        private bool _isSwitching;

        public LanguagePickerPresenter(LanguagePickerView view, ILocalizationService loc, ISettingsService settings)
        {
            _view = view;
            _loc = loc;
            _settings = settings;
        }

        public bool IsOpen => _view.IsVisible;

        public void Start()
        {
            if (_settings.HasChosenLanguage)
            {
                _view.Hide();
                return;
            }

            _cts = new CancellationTokenSource();

            for (int i = 0; i < _loc.AvailableLanguages.Count; i++)
            {
                _languages.Add(_loc.AvailableLanguages[i]);
            }

            _view.BuildLanguageButtons(_languages.Count);
            _view.OnLanguageClicked += HandleLanguageClicked;
            _view.OnConfirmClicked += HandleConfirm;

            // La langue présélectionnée est celle que le GameBootstrapper a chargée faute de
            // choix : celle du système. L'abonnement en fait le premier rendu.
            _languageSubscription = _loc.CurrentLanguage.Subscribe(_ => Render());

            _view.Show();
        }

        private void Render()
        {
            _view.SetTexts(
                _loc.GetText("LANGUAGE_PICKER_TITLE"),
                _loc.GetText("LANGUAGE_PICKER_HINT"),
                _loc.GetText("LANGUAGE_PICKER_CONFIRM"));

            string current = _loc.CurrentLanguage.CurrentValue;

            for (int i = 0; i < _languages.Count; i++)
            {
                string name = _loc.GetText(LocalizationKeys.LanguageName(_languages[i]));

                // La sélection se lit dans le libellé lui-même, par un gabarit localisé : un
                // simple changement de couleur ne suffirait pas à un joueur daltonien.
                _view.SetLanguageLabel(i, _languages[i] == current
                    ? _loc.GetText("LANGUAGE_PICKER_SELECTED", name)
                    : name);
            }
        }

        private void HandleLanguageClicked(int index)
        {
            if (_isSwitching || index < 0 || index >= _languages.Count) return;
            if (_languages[index] == _loc.CurrentLanguage.CurrentValue) return;

            PreviewAsync(_languages[index], _cts.Token).Forget();
        }

        private async UniTaskVoid PreviewAsync(string languageCode, CancellationToken ct)
        {
            _isSwitching = true;
            _view.SetLanguageButtonsInteractable(false);

            try
            {
                await _loc.LoadLanguageAsync(languageCode, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LANGUAGE] Échec du changement de langue : {ex}");
            }

            _isSwitching = false;
            _view.SetLanguageButtonsInteractable(true);
        }

        private void HandleConfirm()
        {
            if (_isSwitching) return;

            _settings.SetLanguage(_loc.CurrentLanguage.CurrentValue);
            _view.Hide();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _languageSubscription?.Dispose();

            _view.OnLanguageClicked -= HandleLanguageClicked;
            _view.OnConfirmClicked -= HandleConfirm;
        }
    }
}
