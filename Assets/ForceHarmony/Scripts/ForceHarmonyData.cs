using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ForceHarmony
{
    public enum WeaponStyle
    {
        Sentinel,
        Striker,
        Punisher,
        Resonance
    }

    public enum CcType
    {
        None,
        Weaken,
        Stun,
        Freeze
    }

    public sealed class CharacterStat
    {
        public string CharacterId;
        public string Name;
        public string StringId;
        public WeaponStyle WeaponStyle;
        public int DefaultSpd;
        public int MaxBpLimit;
        public string ChainSkillId;
        public string BasicSkillId;
        public int MaxHp;
        public int MaxShield;
    }

    public sealed class SkillCombat
    {
        public string SkillId;
        public int NumericId;
        public string SkillName;
        public string StringId;
        public float CostSkill;
        public int ShieldBreakPower;
        public CcType CcType;
        public float CcChance;
        public bool QteTrigger;
        public int BaseDamage;
        public float AdvanceGauge;
        public int BpSpent;
    }

    public sealed class MonsterStat
    {
        public int MonsterId;
        public string Name;
        public string StringId;
        public WeaponStyle WeaknessStyle;
        public int DefaultSpd;
        public int MaxHp;
        public int MaxShield;
        public string BasicSkillId;
    }

    public sealed class StageCombat
    {
        public int StageId;
        public string StageName;
        public string StringId;
        public List<int> EnemyIndices = new List<int>();
        public List<int> GimmickIndices = new List<int>();
    }

    public sealed class GlobalCombatRules
    {
        public float QteWindowTime = 3f;
        public int ForceOverheatTurn = 3;
        public int ForceOverheatTick = 3000;
        public float BreakDamageMultiplier = 2f;
        public float StunDamageModifier = 1.3f;
    }

    public sealed class CommandActionMapping
    {
        public int ActionIndex;
        public string CommandName;
        public float CostSkillCoefficient;
        public int BpChangeValue;
        public bool IsQteTriggerable;
        public string WeaponStyleType;
    }

    public sealed class ForceHarmonyDatabase
    {
        public readonly Dictionary<string, CharacterStat> Characters = new Dictionary<string, CharacterStat>();
        public readonly Dictionary<string, SkillCombat> Skills = new Dictionary<string, SkillCombat>();
        public readonly Dictionary<int, MonsterStat> Monsters = new Dictionary<int, MonsterStat>();
        public readonly Dictionary<int, StageCombat> Stages = new Dictionary<int, StageCombat>();
        public readonly Dictionary<int, CommandActionMapping> CommandActions = new Dictionary<int, CommandActionMapping>();
        public readonly Dictionary<string, string> KoreanText = new Dictionary<string, string>();
        public readonly GlobalCombatRules Rules = new GlobalCombatRules();
        public readonly List<string> Warnings = new List<string>();

        public static ForceHarmonyDatabase LoadFromResources()
        {
            var database = new ForceHarmonyDatabase();
            database.LoadCharacters();
            database.LoadSkills();
            database.LoadMonsters();
            database.LoadStages();
            database.LoadRules();
            database.LoadCommandActions();
            database.LoadLanguage();
            database.ValidateReferences();
            return database;
        }

        public string Text(string stringId, string fallback)
        {
            if (!string.IsNullOrEmpty(stringId) && KoreanText.TryGetValue(stringId, out var value))
            {
                return value;
            }

            return fallback;
        }

        private void LoadCharacters()
        {
            foreach (var row in LoadRows("character_stat"))
            {
                var data = new CharacterStat
                {
                    CharacterId = row.String("character_id"),
                    Name = row.String("name"),
                    StringId = row.String("string_id"),
                    WeaponStyle = ParseEnum(row.String("weapon_style"), WeaponStyle.Striker),
                    DefaultSpd = row.Int("default_spd"),
                    MaxBpLimit = row.Int("max_bp_limit"),
                    ChainSkillId = row.String("chain_skill_id"),
                    BasicSkillId = row.String("basic_skill_id"),
                    MaxHp = row.Int("max_hp"),
                    MaxShield = row.Int("max_shield")
                };

                if (!string.IsNullOrEmpty(data.CharacterId))
                {
                    Characters[data.CharacterId] = data;
                }
            }
        }

        private void LoadSkills()
        {
            foreach (var row in LoadRows("skill_combat"))
            {
                var data = new SkillCombat
                {
                    SkillId = row.String("skill_id"),
                    NumericId = row.Int("numeric_id"),
                    SkillName = row.String("skill_name"),
                    StringId = row.String("string_id"),
                    CostSkill = row.Float("cost_skill"),
                    ShieldBreakPower = row.Int("shield_break_power"),
                    CcType = ParseEnum(row.String("cc_type"), CcType.None),
                    CcChance = row.Float("cc_chance"),
                    QteTrigger = row.Bool("qte_trigger"),
                    BaseDamage = row.Int("base_damage"),
                    AdvanceGauge = row.Float("advance_gauge"),
                    BpSpent = row.Int("bp_spent")
                };

                if (data.NumericId != 0 && (data.NumericId < 4000 || data.NumericId > 4999))
                {
                    Warnings.Add($"Skill numeric id out of range: {data.SkillId} = {data.NumericId}");
                }

                if (!string.IsNullOrEmpty(data.SkillId))
                {
                    Skills[data.SkillId] = data;
                }
            }
        }

        private void LoadMonsters()
        {
            foreach (var row in LoadRows("monster_stat"))
            {
                var data = new MonsterStat
                {
                    MonsterId = row.Int("monster_id"),
                    Name = row.String("name"),
                    StringId = row.String("string_id"),
                    WeaknessStyle = ParseEnum(row.String("weakness_style"), WeaponStyle.Striker),
                    DefaultSpd = row.Int("default_spd"),
                    MaxHp = row.Int("max_hp"),
                    MaxShield = row.Int("max_shield"),
                    BasicSkillId = row.String("basic_skill_id")
                };

                if (data.MonsterId != 0 && (data.MonsterId < 1000 || data.MonsterId > 1299))
                {
                    Warnings.Add($"Monster id out of range: {data.MonsterId}");
                }

                if (data.MonsterId != 0)
                {
                    Monsters[data.MonsterId] = data;
                }
            }
        }

        private void LoadStages()
        {
            foreach (var row in LoadRows("stage_combat"))
            {
                var data = new StageCombat
                {
                    StageId = row.Int("stage_id"),
                    StageName = row.String("stage_name"),
                    StringId = row.String("string_id"),
                    EnemyIndices = row.IntList("enemy_indices"),
                    GimmickIndices = row.IntList("gimmick_indices")
                };

                foreach (var gimmickId in data.GimmickIndices)
                {
                    if (gimmickId != 0 && (gimmickId < 3000 || gimmickId > 3099))
                    {
                        Warnings.Add($"Gimmick id out of range in stage {data.StageId}: {gimmickId}");
                    }
                }

                if (data.StageId != 0)
                {
                    Stages[data.StageId] = data;
                }
            }
        }

        private void LoadRules()
        {
            foreach (var row in LoadRows("global_combat_rule"))
            {
                var key = row.String("rule_key");
                var value = row.Float("value");

                if (key == "QTE_WINDOW_TIME") Rules.QteWindowTime = value;
                if (key == "FORCE_OVERHEAT_TURN") Rules.ForceOverheatTurn = Mathf.RoundToInt(value);
                if (key == "FORCE_OVERHEAT_TICK") Rules.ForceOverheatTick = Mathf.RoundToInt(value);
                if (key == "BREAK_DMG_MULTIPLIER") Rules.BreakDamageMultiplier = value;
                if (key == "STUN_DMG_MODIFIER") Rules.StunDamageModifier = value;
            }
        }

        private void LoadCommandActions()
        {
            foreach (var row in LoadRows("command_action_mapping"))
            {
                var data = new CommandActionMapping
                {
                    ActionIndex = row.Int("action_index"),
                    CommandName = row.String("command_name"),
                    CostSkillCoefficient = row.Float("cost_skill_coefficient"),
                    BpChangeValue = row.Int("bp_change_value"),
                    IsQteTriggerable = row.Bool("is_qte_triggerable"),
                    WeaponStyleType = row.String("weapon_style_type")
                };

                if (data.ActionIndex != 0)
                {
                    CommandActions[data.ActionIndex] = data;
                }
            }
        }

        private void LoadLanguage()
        {
            foreach (var row in LoadRows("language"))
            {
                var stringId = row.String("string_id");
                if (!string.IsNullOrEmpty(stringId))
                {
                    KoreanText[stringId] = row.String("ko");
                }
            }
        }

        private List<CsvRow> LoadRows(string resourceName)
        {
            var tableText = LoadTableText(resourceName);
            if (string.IsNullOrEmpty(tableText))
            {
                Warnings.Add($"Missing data table: {resourceName}");
                return new List<CsvRow>();
            }

            return ForceHarmonyCsvParser.Parse(tableText, resourceName, Warnings);
        }

        private string LoadTableText(string resourceName)
        {
#if UNITY_EDITOR
            var dataTablePath = Path.Combine(Application.dataPath, "DataTable", $"{resourceName}.csv");
            if (File.Exists(dataTablePath))
            {
                return File.ReadAllText(dataTablePath, Encoding.UTF8);
            }
#endif

            var streamingText = LoadStreamingAssetsText(resourceName);
            if (!string.IsNullOrEmpty(streamingText))
            {
                return streamingText;
            }

            var asset = Resources.Load<TextAsset>($"ForceHarmony/Data/{resourceName}");
            return asset != null ? asset.text : string.Empty;
        }

        private string LoadStreamingAssetsText(string resourceName)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "ForceHarmony", "Data", $"{resourceName}.csv");
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }

            if (!path.Contains("://"))
            {
                return string.Empty;
            }

            using (var request = UnityWebRequest.Get(path))
            {
                var operation = request.SendWebRequest();
                var startedAt = Time.realtimeSinceStartup;
                while (!operation.isDone && Time.realtimeSinceStartup - startedAt < 3f)
                {
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return request.downloadHandler.text;
                }
            }

            return string.Empty;
        }

        private void ValidateReferences()
        {
            foreach (var character in Characters.Values)
            {
                WarnMissingSkill(character.BasicSkillId, $"character {character.CharacterId} basic_skill_id");
                WarnMissingSkill(character.ChainSkillId, $"character {character.CharacterId} chain_skill_id");
            }

            foreach (var monster in Monsters.Values)
            {
                WarnMissingSkill(monster.BasicSkillId, $"monster {monster.MonsterId} basic_skill_id");
            }

            foreach (var stage in Stages.Values)
            {
                if (stage.EnemyIndices.Count > 5)
                {
                    Warnings.Add($"Stage {stage.StageId} has {stage.EnemyIndices.Count} enemies. Battle v1 supports up to 5.");
                }

                foreach (var enemyId in stage.EnemyIndices)
                {
                    if (!Monsters.ContainsKey(enemyId))
                    {
                        Warnings.Add($"Stage {stage.StageId} references missing monster id: {enemyId}");
                    }
                }
            }
        }

        private void WarnMissingSkill(string skillId, string owner)
        {
            if (!string.IsNullOrEmpty(skillId) && !Skills.ContainsKey(skillId))
            {
                Warnings.Add($"Missing skill reference from {owner}: {skillId}");
            }
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (Enum.TryParse(value, true, out T parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

    public sealed class CsvRow
    {
        private readonly Dictionary<string, string> values;

        public CsvRow(Dictionary<string, string> values)
        {
            this.values = values;
        }

        public string String(string key)
        {
            if (values.TryGetValue(key, out var value))
            {
                return value.Trim();
            }

            return string.Empty;
        }

        public int Int(string key)
        {
            return int.TryParse(String(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        public float Float(string key)
        {
            var raw = String(key).Replace("%", string.Empty);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return 0f;
            }

            return String(key).Contains("%") ? value / 100f : value;
        }

        public bool Bool(string key)
        {
            var raw = String(key);
            if (bool.TryParse(raw, out var value))
            {
                return value;
            }

            return raw == "1" || raw.Equals("yes", StringComparison.OrdinalIgnoreCase) || raw.Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        public List<int> IntList(string key)
        {
            var result = new List<int>();
            var raw = String(key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var parts = raw.Split(';');
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0)
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }

    public static class ForceHarmonyCsvParser
    {
        public static List<CsvRow> Parse(string csvText, string tableName, List<string> warnings)
        {
            var records = ParseRecords(csvText);
            var rows = new List<CsvRow>();
            if (records.Count < 5)
            {
                warnings.Add($"Table {tableName} has fewer than 5 rows.");
                return rows;
            }

            var keys = records[2];
            var types = records[3];
            for (var rowIndex = 4; rowIndex < records.Count; rowIndex++)
            {
                var record = records[rowIndex];
                if (record.Count == 0 || string.IsNullOrWhiteSpace(record[0]))
                {
                    continue;
                }

                var values = new Dictionary<string, string>();
                for (var columnIndex = 0; columnIndex < keys.Count; columnIndex++)
                {
                    var key = keys[columnIndex].Trim();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var type = columnIndex < types.Count ? types[columnIndex].Trim().TrimStart('*') : "string";
                    var value = columnIndex < record.Count ? record[columnIndex] : string.Empty;
                    values[key] = NormalizeEmpty(value, type);
                }

                rows.Add(new CsvRow(values));
            }

            return rows;
        }

        private static string NormalizeEmpty(string value, string type)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (type == "int" || type == "float")
            {
                return "0";
            }

            if (type == "bool")
            {
                return "false";
            }

            return string.Empty;
        }

        private static List<List<string>> ParseRecords(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    row.Add(field.ToString());
                    field.Length = 0;
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
}
