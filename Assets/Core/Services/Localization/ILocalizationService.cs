namespace Core.Services.Localization
{
    public interface ILocalizationService
    {
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
    }
}