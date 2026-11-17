using UnityEngine;

namespace Lifeline
{
    /// <summary>Next-step copy only. Values come from the same public tuning used by gameplay.</summary>
    public static class UpgradeSummary
    {
        static string F(string source, params object[] values) => string.Format(UiLocalization.Text(source), values);
        static string N(float value) => value.ToString("0.#");
        static float At(float[] values, int level) => values[Mathf.Clamp(level, 0, values.Length - 1)];
        public static int DisplayTier(UpgradeKind kind, int level) => level + (kind == UpgradeKind.Searchlight ? 1 : 0);

        public static string NextEffect(GameSession game, UpgradeKind kind)
        {
            int level = game.GetUpgrade(kind), next = level + 1;
            if (level >= game.MaxUpgradeLevel(kind)) return UiLocalization.Text("已满级");
            var t = game.tuning;
            switch (kind)
            {
                case UpgradeKind.Rope:
                    string rope = F("绳长 {0}m", N(At(t.ropeLengths, next)));
                    if (next == 2) return rope + F("；判定范围再 +{0}%", N(t.winchT2Bonus * 100));
                    if (next == 3) return rope + UiLocalization.Text("；有电自动回收，保留手动校准");
                    if (next == 4) return rope + F("；拉回效率 +{0}%", N(t.winchT4RecoveryBonus * 100));
                    return rope;
                case UpgradeKind.Oxygen: return F("氧气上限 {0}s", N(At(t.oxygenLevels, next)));
                case UpgradeKind.Hull: return F("船体耐久上限 {0}", N(At(t.hullLevels, next) * (game.infinitePower ? t.eelHullMultiplier : 1)));
                case UpgradeKind.Sail:
                    return next == 1 ? F("航速 +{0}%", N(game.sailSpeedBonus * 100))
                        : F("风暴漂移减少 {0}%", N((1 - t.sailT2DriftMultiplier) * 100));
                case UpgradeKind.Searchlight: return UiLocalization.Text("夜潜全视野；海鸥刷新减少40%");
                case UpgradeKind.Backpack:
                    string bag = F("背包 +{0}格", t.backpackSlotsByLevel[next] - t.backpackSlotsByLevel[level]);
                    return next == 1 || next == 3 ? bag + F("；采集速度 +{0}%", N(t.backpackCollectionBonus * 100)) : bag;
                case UpgradeKind.DivingSuit:
                    return next >= 3 ? F("每次受击 {0}%概率免疫", N(t.upgradedSuitImmunity * 100))
                        : F("承伤上限 {0}次", next == 1 ? t.upgradedDiverHitCapacity : t.suitT2HitCapacity);
                default:
                    string craft = F("制作时间 {0}s", N(At(t.craftSecondsByLevel, next)));
                    if (next == 1) return craft + F("；QTE判定范围 +{0}%", N((t.workbenchWindowMultiplier - 1) * 100));
                    return next >= 3 ? craft + UiLocalization.Text("；自动制作同时2个") : craft;
            }
        }
    }
}
