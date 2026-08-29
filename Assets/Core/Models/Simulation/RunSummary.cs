namespace Core.Models.Simulation
{
    /// <summary>
    /// Comment une run s'est terminée. Le GDD leur donne deux narrations distinctes, et
    /// l'Effacement Propre vaut un bonus qu'il faut pouvoir célébrer.
    /// </summary>
    public enum RunEndReason
    {
        /// <summary>Saisie Fédérale : la Trace a atteint 100 %. Aucun bonus.</summary>
        Seized,

        /// <summary>Effacement Propre : le joueur est sorti de lui-même, avec le bonus Clean Exit.</summary>
        CleanExit
    }

    /// <summary>
    /// Photographie de la run qui vient de s'achever.
    ///
    /// Existe parce que <c>OnSessionEnded</c> ne transportait qu'un <c>double</c> — le gain — et
    /// que l'écran de fin ne pouvait donc pas distinguer une saisie d'une sortie volontaire, ni
    /// rien raconter d'autre que ce chiffre.
    ///
    /// <b>Capturée AVANT le wipe</b>, obligatoirement : <c>ResolveRunEnd</c> efface la run —
    /// argent, Trace, compteurs — juste après avoir crédité le gain. Lue après, cette structure
    /// ne contiendrait que des zéros.
    ///
    /// Struct readonly : une fin de run ne se produit qu'une fois par partie, mais la passer par
    /// valeur évite qu'un abonné puisse la modifier sous le nez des suivants.
    /// </summary>
    public readonly struct RunSummary
    {
        public readonly RunEndReason Reason;

        /// <summary>CPU Cycles réellement crédités, bonus Clean Exit compris.</summary>
        public readonly double CpuCyclesEarned;

        /// <summary>
        /// Ce que la run aurait rapporté sans le bonus. Sert à afficher l'écart — un joueur qui
        /// ne voit que le total ne mesure pas ce que l'Effacement Propre lui a valu.
        /// </summary>
        public readonly double CpuCyclesBeforeBonus;

        /// <summary>Datas générées sur la run, dépensées ou non.</summary>
        public readonly double DataGenerated;

        /// <summary>
        /// Jauge de Trace au moment de la fin, de 0 à 1. Vaut 1 sur une saisie ; sur une sortie
        /// volontaire, c'est le vrai indicateur d'audace — sortir à 95 % n'est pas sortir à 30 %.
        /// </summary>
        public readonly float ThreatAtEnd;

        /// <summary>Nombre d'activations du Data Wiper sur la run.</summary>
        public readonly int EmergencyUses;

        public RunSummary(
            RunEndReason reason,
            double cpuCyclesEarned,
            double cpuCyclesBeforeBonus,
            double dataGenerated,
            float threatAtEnd,
            int emergencyUses)
        {
            Reason = reason;
            CpuCyclesEarned = cpuCyclesEarned;
            CpuCyclesBeforeBonus = cpuCyclesBeforeBonus;
            DataGenerated = dataGenerated;
            ThreatAtEnd = threatAtEnd;
            EmergencyUses = emergencyUses;
        }

        /// <summary>Le bonus Clean Exit a-t-il réellement rapporté quelque chose à afficher.</summary>
        public bool HasCleanExitBonus =>
            Reason == RunEndReason.CleanExit && CpuCyclesEarned > CpuCyclesBeforeBonus;
    }
}
