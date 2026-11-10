using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>The air brake standards an engine can carry.</summary>
    public enum AirStandard
    {
        European,
        American,
    }

 public static class EngineStandards
    {
        private static readonly Dictionary<string, AirStandard> known = new Dictionary<string, AirStandard>
        {
            ["LocoS282A"] = AirStandard.American,
            ["LocoS060"] = AirStandard.American,
            ["LocoDE2"] = AirStandard.European,
            ["LocoDE6"] = AirStandard.European,
            ["LocoDH4"] = AirStandard.European,
            ["LocoDM3"] = AirStandard.European,
            ["LocoDM1U"] = AirStandard.European,
            ["LocoMicroshunter"] = AirStandard.European,
            ["FEF-3"] = AirStandard.American,
            ["S484-840A"] = AirStandard.American,
            ["S484-841A"] = AirStandard.American,
            ["A.T.&S.F. 2901-A"] = AirStandard.American,
            ["S464-730"] = AirStandard.American,
            ["S040F"] = AirStandard.American,
            ["DE4-740"] = AirStandard.American,
            ["DE4-740B"] = AirStandard.American,
            ["FPD-4"] = AirStandard.American,
            ["EMD-GP7"] = AirStandard.American,
            ["DE4-750"] = AirStandard.American,
            ["DE4-751"] = AirStandard.American,
            // Lilla and Cruzer's fictional 0-8-0 "for S060 equivalent work" (Nexus 1334): as the S060.
            ["Jk3A"] = AirStandard.American,
            // The QJ: China's air brake descends from Westinghouse's.
            ["S2102-870"] = AirStandard.American,
            // E_noob's DB BR95, a Prussian T 20.
            ["S2102-720"] = AirStandard.European,
        };

        /// <summary>The standard of the engine of <paramref name="liveryId"/>, else of <paramref name="typeId"/>; null when unknown.</summary>
        public static AirStandard? Known(string liveryId, string typeId)
        {
            if (liveryId != null && known.TryGetValue(liveryId, out var byLivery))
                return byLivery;
            if (typeId != null && known.TryGetValue(typeId, out var byType))
                return byType;
            return null;
        }

        /// <summary>
        /// The standard an engine runs: the setting when every engine uses it
        /// (the override, off by default), else the engine's own, else the
        /// setting.
        /// </summary>
        public static AirStandard For(bool everyEngineUsesSetting, AirStandard setting, string liveryId, string typeId) =>
            everyEngineUsesSetting ? setting : Known(liveryId, typeId) ?? setting;
    }
}
