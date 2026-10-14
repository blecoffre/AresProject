using Cysharp.Threading.Tasks;
using R3;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Core.Services.Localization
{
    public interface ILocalizationService
    {
        /// <summary>
        /// Code de la langue chargée (« fr », « en »…). Émet à CHAQUE changement de langue, une
        /// fois le nouveau dictionnaire en place : toute vue qui affiche du texte s'y abonne pour
        /// se réécrire à chaud. Émet aussi sa valeur courante à l'abonnement, comme toute
        /// ReactiveProperty — l'abonnement peut donc servir de premier rendu.
        /// </summary>
        ReadOnlyReactiveProperty<string> CurrentLanguage { get; }

        /// <summary>
        /// La culture de la langue chargée (« fr » → fr-FR) : séparateur décimal et format des
        /// nombres. <b>Celle de la langue du JEU, jamais celle de Windows</b> — un joueur qui joue
        /// en anglais sur un Windows français doit lire « 1.5 s », pas « 1,5 s ». Les gabarits de
        /// <see cref="GetText{T0}"/> l'appliquent d'eux-mêmes ; tout nombre formaté hors gabarit
        /// (CurrencyFormatter) la reçoit en paramètre. Invariante tant qu'aucune langue n'est
        /// chargée. Mise à jour AVANT l'émission de <see cref="CurrentLanguage"/>.
        /// </summary>
        CultureInfo Culture { get; }

        /// <summary>Codes des langues dont un fichier existe, triés.</summary>
        IReadOnlyList<string> AvailableLanguages { get; }

        bool IsAvailable(string languageCode);

        /// <summary>La langue du système si le jeu la parle, sinon l'anglais, sinon la première.</summary>
        string ResolveSystemLanguage();

        UniTask LoadLanguageAsync(string languageCode, CancellationToken ct);

        /// <summary>
        /// Retourne le texte brut associé à la clé.
        /// </summary>
        string GetText(string key);

        /// <summary>
        /// Retourne le texte formaté avec 1 argument (Zero Boxing).
        /// </summary>
        string GetText<T0>(string key, T0 arg0);

        /// <summary>
        /// Retourne le texte formaté avec 2 arguments (Zero Boxing).
        /// </summary>
        string GetText<T0, T1>(string key, T0 arg0, T1 arg1);

        /// <summary>
        /// Retourne le texte formaté avec 3 arguments (Zero Boxing).
        /// </summary>
        string GetText<T0, T1, T2>(string key, T0 arg0, T1 arg1, T2 arg2);

        /// <summary>
        /// Retourne le texte formaté avec 4 arguments (Zero Boxing).
        /// </summary>
        string GetText<T0, T1, T2, T3>(string key, T0 arg0, T1 arg1, T2 arg2, T3 arg3);
    }
}