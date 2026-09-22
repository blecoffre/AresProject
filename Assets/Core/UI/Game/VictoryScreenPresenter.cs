using Core.Services.Economy;
using Core.Services.Localization;
using Core.Services.Simulation;
using Core.Utils;
using R3;
using System;
using VContainer.Unity;

namespace Core.UI.Game
{
    /// <summary>
    /// Allume l'écran de victoire au franchissement du seuil, et le referme quand le joueur le
    /// demande.
    ///
    /// Séparé du <see cref="VictoryPresenter"/>, qui déroule la séquence console : chacun pilote
    /// une seule vue, comme partout ailleurs dans la scène. Cette séparation a un effet utile — la
    /// séquence console continue de fonctionner même si aucune VictoryView n'est posée, alors
    /// qu'un presenter unique emporterait les deux dans sa chute.
    ///
    /// <b>Ne décide rien.</b> Le franchissement appartient au <see cref="VictorySystem"/>.
    /// </summary>
    public sealed class VictoryScreenPresenter : IStartable, IDisposable
    {
        private readonly VictorySystem _victory;
        private readonly VictoryView _view;
        private readonly UpgradeManager _upgrades;
        private readonly ILocalizationService _loc;

        private DisposableBag _disposables;

        public VictoryScreenPresenter(
            VictorySystem victory,
            VictoryView view,
            UpgradeManager upgrades,
            ILocalizationService loc)
        {
            _victory = victory;
            _view = view;
            _upgrades = upgrades;
            _loc = loc;
        }

        public void Start()
        {
            // On s'abonne à l'ÉVÉNEMENT et non à HasWon : ce dernier reste vrai pour toujours, donc
            // s'y abonner rouvrirait l'écran à chaque rechargement de la GameScene.
            _victory.OnVictory
                    .Subscribe(_ => ShowVictory())
                    .AddTo(ref _disposables);

            _view.OnCloseClicked += HandleClose;
        }

        private void ShowVictory()
        {
            // La capacité est lue MAINTENANT : c'est celle qui a franchi le seuil, et le joueur
            // voudra voir le chiffre qu'il a atteint, pas le seuil qu'il visait.
            string tflops = CurrencyFormatter.Format(_upgrades.TotalTFlops.CurrentValue);

            _view.Show(
                _loc.GetText("UI_VICTORY_TITLE"),
                _loc.GetText("UI_VICTORY_BODY", tflops));
        }

        // Méthode nommée, obligatoirement : se désabonner d'un event avec une lambda ne retire
        // rien — `-= x => ...` crée un nouveau delegate et laisse l'ancien en place.
        private void HandleClose() => _view.Hide();

        public void Dispose()
        {
            if (_view != null) _view.OnCloseClicked -= HandleClose;
            _disposables.Dispose();
        }
    }
}
