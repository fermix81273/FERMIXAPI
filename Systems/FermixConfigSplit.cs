using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Раскладывает плоский <see cref="Config"/> EXILED по подпапкам внутри
    /// <c>EXILED/Configs/&lt;port&gt;/FermixAPI/</c>: для каждой подсистемы — отдельный
    /// yml-файл (Coin/coin.yml, Chat/chat.yml, ...).
    /// 
    /// Поведение на старте:
    /// <list type="bullet">
    /// <item>Если sub-yml существует — читаем его и применяем значения к <see cref="FermixCore.Config"/>
    /// (через reflection); тем самым sub-файлы имеют приоритет над <c>Fermix-API.yml</c>.</item>
    /// <item>Если sub-yml нет — создаём его из текущих значений Config (миграция первого запуска).</item>
    /// </list>
    /// 
    /// Удалить sub-файл = откатиться к значениям из основного <c>Fermix-API.yml</c>.
    /// </summary>
    public static class FermixConfigSplit
    {
        private sealed class Section
        {
            public string Folder { get; set; }
            public string FileName { get; set; }
            public string Description { get; set; }
            public string[] Props { get; set; }
        }

        private static readonly Section[] Sections =
        {
            new Section { Folder = "Coin", FileName = "coin", Description = "FermixCoin — монетка-судьба, исходы и автоспавн.", Props = new[] {
                "CoinEnabled","CoinMaxUses","MegaJackpotChance","RarityGlowEnabled","ShowCommentHints","BroadcastMegaJackpot",
                "CoinAutoSpawnEnabled","CoinAutoSpawnCount","CoinAutoSpawnDelay" } },

            new Section { Folder = "Items", FileName = "remote-keycard", Description = "RemoteKeycard — открывание дверей карты из инвентаря.", Props = new[] {
                "RemoteKeycardEnabled","RemoteKeycardWorksOnDoors","RemoteKeycardWorksOnLockers",
                "RemoteKeycardWorksOnGenerators","RemoteKeycardShowHint" } },

            new Section { Folder = "Chat", FileName = "chat", Description = "FermixChat — глобальный чат через .say.", Props = new[] {
                "ChatEnabled","ChatHistorySize","ChatMessageLifetime","ChatCooldown","ChatMaxLength" } },

            new Section { Folder = "Hud", FileName = "generator", Description = "FermixGeneratorHud — HUD генераторов для SCP-079.", Props = new[] {
                "GeneratorHudEnabled","GeneratorHudUpdateInterval" } },

            new Section { Folder = "Items", FileName = "scramble", Description = "FermixScramble — SCP-1344 как глушитель триггера 096.", Props = new[] {
                "ScrambleEnabled","ScrambleSpawnCount","ScrambleSpawnDelay","ScrambleActiveDuration","ScrambleCooldown" } },

            new Section { Folder = "Callvote", FileName = "callvote", Description = "FermixCallvote — голосования игроков.", Props = new[] {
                "CallvoteEnabled","CallvoteDuration","CallvoteCooldown" } },

            new Section { Folder = "Scp106", FileName = "scp106", Description = "FermixScp106Plus — расширения SCP-106 (.106 stalk/tp).", Props = new[] {
                "Scp106PlusEnabled","Scp106PlusVigorCost","Scp106BindingsEnabled" } },

            new Section { Folder = "Goc", FileName = "goc", Description = "FermixGoc — фракция Global Occult Coalition.", Props = new[] {
                "GocEnabled","GocWaveStartMinuteThreshold","GocWaveChance","GocOneWavePerRound",
                "GocManualWaveSize","GocCassiePhonemes","GocCassieSubtitles" } },

            new Section { Folder = "Roles", FileName = "squad-classes", Description = "FermixSquadClasses — кастомные классы NTF/Chaos/G.O.C.", Props = new[] {
                "SquadClassesEnabled","SquadClassesMedicRadius","SquadClassesMedicHealPerSec",
                "SquadClassesMedicHealInterval","SquadClassesCommanderDamageMult" } },

            new Section { Folder = "Items", FileName = "nvg", Description = "FermixNvg — прибор ночного видения.", Props = new[] {
                "NvgEnabled","NvgSpawnCount","NvgSpawnDelay","NvgRemove1344Effect","NvgEffectIntensity",
                "NvgLightRange","NvgLightIntensity","NvgLightSpotAngle","NvgLightInnerAngle",
                "NvgTrackCamera","NvgTrackInterval" } },

            new Section { Folder = "NoRules", FileName = "infinity", Description = "FermixInfinity — бесконечная рация / авто-добив магазина.", Props = new[] {
                "InfinityStuffEnabled" } },

            new Section { Folder = "NoRules", FileName = "hitmarkers", Description = "FermixHitmarkers — урон/убийство-маркеры.", Props = new[] {
                "HitmarkersEnabled" } },

            new Section { Folder = "Xp", FileName = "xp", Description = "FermixPlayerXp — система опыта/уровней (см. также Xp/levels.yml).", Props = new[] {
                "PlayerXpEnabled" } },

            new Section { Folder = "NoRules", FileName = "scp-swap", Description = "FermixScpSwap — смена SCP-роли в первые секунды раунда (.swap).", Props = new[] {
                "ScpSwapEnabled","ScpSwapWindowSeconds" } },
        };

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        public static void Initialize()
        {
            try
            {
                var cfg = FermixCore.Config;
                if (cfg == null) return;

                var rootDir = FermixConfigUtils.ConfigDirectory;
                if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);

                // Миграция предыдущих локаций (v2.6.4 → v2.6.5).
                MigrateLegacyFile(rootDir, "levels.yml", Path.Combine(rootDir, "Xp", "levels.yml"));

                int loaded = 0, created = 0;
                foreach (var sect in Sections)
                {
                    var subDir = Path.Combine(rootDir, sect.Folder);
                    if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
                    var path = Path.Combine(subDir, sect.FileName + ".yml");

                    if (File.Exists(path))
                    {
                        ApplyFromFile(cfg, sect, path);
                        loaded++;
                    }
                    else
                    {
                        WriteFromConfig(cfg, sect, path);
                        created++;
                    }
                }

                if (FermixCore.Config?.Debug == true)
                    FermixLog.Debug($"FermixConfigSplit: загружено {loaded} файлов, создано {created} новых.");
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixConfigSplit: {ex.Message}");
            }
        }

        private static void ApplyFromFile(Config cfg, Section sect, string path)
        {
            var yaml = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(yaml)) return;

            // Деserialize в Dictionary<string, object> с подчёркнуто-кейсом ключей.
            Dictionary<string, object> dict;
            try { dict = Deserializer.Deserialize<Dictionary<string, object>>(yaml) ?? new Dictionary<string, object>(); }
            catch (Exception ex) { FermixLog.Warn($"FermixConfigSplit: не смог прочитать {path}: {ex.Message}"); return; }

            var t = cfg.GetType();
            foreach (var propName in sect.Props)
            {
                var snakeKey = ToSnake(propName);
                if (!dict.TryGetValue(snakeKey, out var raw)) continue;
                var pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (pi == null || !pi.CanWrite) continue;
                try
                {
                    var converted = ConvertTo(raw, pi.PropertyType);
                    pi.SetValue(cfg, converted);
                }
                catch (Exception ex) { FermixLog.Warn($"FermixConfigSplit: {propName} ({path}) — {ex.Message}"); }
            }
        }

        private static void WriteFromConfig(Config cfg, Section sect, string path)
        {
            var t = cfg.GetType();
            var dict = new Dictionary<string, object>();
            foreach (var propName in sect.Props)
            {
                var pi = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (pi == null) continue;
                dict[ToSnake(propName)] = pi.GetValue(cfg);
            }
            var header = $"# {sect.Description}\n# Этот файл переопределяет соответствующие поля в Fermix-API.yml.\n# Удалите файл, чтобы вернуться к значениям из основного конфига.\n\n";
            File.WriteAllText(path, header + Serializer.Serialize(dict));
        }

        private static void MigrateLegacyFile(string rootDir, string oldName, string newPath)
        {
            try
            {
                var oldPath = Path.Combine(rootDir, oldName);
                if (!File.Exists(oldPath) || File.Exists(newPath)) return;
                var parent = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                File.Move(oldPath, newPath);
                FermixLog.Info($"FermixConfigSplit: миграция {oldName} → {Path.GetFileName(Path.GetDirectoryName(newPath))}/{Path.GetFileName(newPath)}");
            }
            catch (Exception ex) { FermixLog.Warn($"FermixConfigSplit: миграция {oldName} не удалась: {ex.Message}"); }
        }

        private static string ToSnake(string pascal)
        {
            if (string.IsNullOrEmpty(pascal)) return pascal;
            var sb = new System.Text.StringBuilder(pascal.Length + 6);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(pascal[i - 1]) || (i + 1 < pascal.Length && char.IsLower(pascal[i + 1]))))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static object ConvertTo(object raw, Type target)
        {
            if (raw == null) return null;
            if (target.IsInstanceOfType(raw)) return raw;
            var rawStr = raw.ToString();
            if (target == typeof(bool)) return bool.Parse(rawStr);
            if (target == typeof(int)) return int.Parse(rawStr, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(long)) return long.Parse(rawStr, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(float)) return float.Parse(rawStr, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(double)) return double.Parse(rawStr, System.Globalization.CultureInfo.InvariantCulture);
            if (target == typeof(string)) return rawStr;
            if (target.IsEnum) return Enum.Parse(target, rawStr, true);
            return Convert.ChangeType(raw, target, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
