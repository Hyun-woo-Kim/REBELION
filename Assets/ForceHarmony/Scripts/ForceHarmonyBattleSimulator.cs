using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForceHarmony
{
    public enum BattleTeam
    {
        Ally,
        Enemy
    }

    public sealed class BattleUnit
    {
        public string UnitId;
        public string DisplayName;
        public BattleTeam Team;
        public WeaponStyle WeaponStyle;
        public WeaponStyle WeaknessStyle;
        public int Spd;
        public int MaxHp;
        public int Hp;
        public int MaxShield;
        public int Shield;
        public int MaxBp;
        public int CurrentBp;
        public string BasicSkillId;
        public string ChainSkillId;
        public float NextActionTick;
        public bool IsBroken;
        public bool IsStunned;
        public bool IsWeakened;
        public bool IsGuarded;

        public bool IsAlive => Hp > 0;
        public float AvBase => Spd <= 0 ? 9999f : 10000f / Spd;
    }

    public sealed class BattleActionResult
    {
        public BattleUnit Actor;
        public BattleUnit Target;
        public SkillCombat Skill;
        public int Damage;
        public int ShieldDamage;
        public bool TriggeredQte;
        public bool TargetDefeated;
        public string Summary;
    }

    public sealed class ForceHarmonyBattleSimulator
    {
        public readonly List<BattleUnit> Allies = new List<BattleUnit>();
        public readonly List<BattleUnit> Enemies = new List<BattleUnit>();
        public readonly List<string> Log = new List<string>();

        private readonly ForceHarmonyDatabase database;
        private readonly System.Random random = new System.Random(1303);

        public float GlobalTick { get; private set; }
        public int PartyBp { get; private set; } = 3;
        public int ForceOverheatRemaining { get; private set; }
        public int ForceOverheatTickRemaining { get; private set; }
        public BattleUnit PendingQteTarget { get; private set; }
        public float PendingQteTimeRemaining { get; private set; }
        public BattleUnit ActiveCommandUnit { get; private set; }
        public BattleUnit LastActor { get; private set; }
        public BattleUnit LastTarget { get; private set; }
        public string LastTurnLabel { get; private set; } = "Battle Ready";
        public string LastActionSummary { get; private set; } = "Waiting for first action.";
        public bool HasPendingQte => PendingQteTarget != null && PendingQteTimeRemaining > 0f;
        public bool IsWaitingForCommand => ActiveCommandUnit != null && ActiveCommandUnit.IsAlive;
        public bool BattleEnded => !Allies.Any(unit => unit.IsAlive) || !Enemies.Any(unit => unit.IsAlive);

        public ForceHarmonyBattleSimulator(ForceHarmonyDatabase database)
        {
            this.database = database;
        }

        public void StartStage(StageCombat stage)
        {
            Allies.Clear();
            Enemies.Clear();
            Log.Clear();
            GlobalTick = 0f;
            PartyBp = 3;
            ForceOverheatRemaining = 0;
            ForceOverheatTickRemaining = 0;
            PendingQteTarget = null;
            PendingQteTimeRemaining = 0f;
            ActiveCommandUnit = null;
            LastActor = null;
            LastTarget = null;
            LastTurnLabel = "Battle Ready";
            LastActionSummary = "Waiting for first action.";

            AddPartyMember("CH_001");
            AddPartyMember("CH_002");
            AddPartyMember("CH_003");

            var enemyIds = stage.EnemyIndices.Take(5).ToList();
            for (var i = 0; i < enemyIds.Count; i++)
            {
                if (database.Monsters.TryGetValue(enemyIds[i], out var monster))
                {
                    Enemies.Add(CreateEnemy(monster, i));
                }
            }

            foreach (var unit in Units)
            {
                unit.NextActionTick = unit.AvBase;
            }

            AddLog($"Stage {database.Text(stage.StringId, stage.StageName)} loaded. Allies 3 / Enemies {Enemies.Count}");
        }

        public void UpdateQteTimer(float deltaTime)
        {
            if (!HasPendingQte)
            {
                return;
            }

            PendingQteTimeRemaining -= deltaTime;
            if (PendingQteTimeRemaining <= 0f)
            {
                AddLog("Force Chain missed. QTE window expired.");
                PendingQteTarget = null;
                PendingQteTimeRemaining = 0f;
            }
        }

        public BattleActionResult StepAutoAction()
        {
            if (BattleEnded || HasPendingQte || IsWaitingForCommand)
            {
                return null;
            }

            var actor = Units.Where(unit => unit.IsAlive).OrderBy(unit => unit.NextActionTick).FirstOrDefault();
            if (actor == null)
            {
                return null;
            }

            var elapsedTick = Mathf.Max(0f, actor.NextActionTick - GlobalTick);
            if (ForceOverheatTickRemaining > 0)
            {
                ForceOverheatTickRemaining = Mathf.Max(0, ForceOverheatTickRemaining - Mathf.RoundToInt(elapsedTick));
            }

            GlobalTick = actor.NextActionTick;
            actor.IsGuarded = false;

            if (actor.IsStunned)
            {
                actor.IsStunned = false;
                actor.NextActionTick = GlobalTick + actor.AvBase;
                LastActor = actor;
                LastTarget = null;
                LastTurnLabel = actor.Team == BattleTeam.Ally ? "ALLY TURN - LOCKDOWN" : "ENEMY TURN - LOCKDOWN";
                LastActionSummary = $"{actor.DisplayName} skips the action because of Lockdown.";
                AddLog($"{actor.DisplayName} is locked down and skips this action.");
                return null;
            }

            if (actor.Team == BattleTeam.Ally)
            {
                BeginAllyCommand(actor);
                return null;
            }

            var skill = ResolveSkill(actor.BasicSkillId);
            var target = PickTarget(actor.Team == BattleTeam.Ally ? Enemies : Allies);
            if (skill == null || target == null)
            {
                return null;
            }

            var result = ResolveAttack(actor, target, skill, bpSpent: 0, shieldHitMultiplier: 1);
            LastActor = actor;
            LastTarget = target;
            LastTurnLabel = actor.Team == BattleTeam.Ally ? "ALLY TURN - ATTACK" : "ENEMY TURN - ATTACK";
            LastActionSummary = result.Summary;
            actor.NextActionTick = GlobalTick + Mathf.Max(1f, actor.AvBase * (1f + skill.CostSkill) - skill.AdvanceGauge);

            return result;
        }

        public BattleActionResult ExecuteBasicAttack(int boostLevel)
        {
            if (!CanExecuteAllyCommand(out var actor, out var target))
            {
                return null;
            }

            boostLevel = Mathf.Clamp(boostLevel, 0, 3);
            boostLevel = Mathf.Min(boostLevel, actor.CurrentBp);
            var skill = ResolveSkill(actor.BasicSkillId);
            if (skill == null)
            {
                AddLog($"{actor.DisplayName} has no basic skill data.");
                return null;
            }

            actor.CurrentBp -= boostLevel;
            var result = ResolveAttack(actor, target, skill, bpSpent: boostLevel, shieldHitMultiplier: 1 + boostLevel);
            actor.CurrentBp = Mathf.Min(actor.CurrentBp + 1, actor.MaxBp);
            EndCommandTurn(actor, skill.CostSkill, skill.AdvanceGauge, "ALLY TURN - BASIC ATTACK");
            return result;
        }

        public BattleActionResult ExecuteTacticalSkill(int boostLevel)
        {
            if (!CanExecuteAllyCommand(out var actor, out var target))
            {
                return null;
            }

            boostLevel = Mathf.Clamp(boostLevel, 0, 3);
            boostLevel = Mathf.Min(boostLevel, actor.CurrentBp);
            var skill = ResolveSkill(actor.BasicSkillId);
            if (skill == null)
            {
                AddLog($"{actor.DisplayName} has no tactical skill data.");
                return null;
            }

            actor.CurrentBp -= boostLevel;
            var result = ResolveAttack(actor, target, skill, bpSpent: boostLevel, shieldHitMultiplier: 1 + boostLevel);
            EndCommandTurn(actor, skill.CostSkill, skill.AdvanceGauge, "ALLY TURN - TACTICAL SKILL");
            return result;
        }

        public void ExecuteGuardOrWait(bool isGuardMode)
        {
            if (ActiveCommandUnit == null || !ActiveCommandUnit.IsAlive)
            {
                return;
            }

            var actor = ActiveCommandUnit;
            actor.IsGuarded = isGuardMode;
            LastActor = actor;
            LastTarget = null;
            LastTurnLabel = isGuardMode ? "ALLY TURN - GUARD" : "ALLY TURN - WAIT";
            LastActionSummary = isGuardMode
                ? $"{actor.DisplayName} guards. Incoming damage -50% until next turn."
                : $"{actor.DisplayName} waits and pulls the next turn forward.";
            AddLog(LastActionSummary);
            EndCommandTurn(actor, -0.3f, 0f, LastTurnLabel);
        }

        public BattleActionResult ExecuteUltimateOverride()
        {
            if (!CanExecuteAllyCommand(out var actor, out var target))
            {
                return null;
            }

            var skill = ResolveSkill(actor.ChainSkillId) ?? ResolveSkill(actor.BasicSkillId);
            if (skill == null)
            {
                AddLog($"{actor.DisplayName} has no ultimate skill data.");
                return null;
            }

            var preservedTick = actor.NextActionTick;
            var result = ResolveAttack(actor, target, skill, bpSpent: 0, shieldHitMultiplier: 2);
            actor.NextActionTick = preservedTick;
            ActiveCommandUnit = null;
            LastActor = actor;
            LastTarget = target;
            LastTurnLabel = "ALLY TURN - ULTIMATE OVERRIDE";
            LastActionSummary = $"{actor.DisplayName} overrides the timeline. Tick order preserved.";
            AddLog(LastActionSummary);
            return result;
        }

        public void ResolveForceChain()
        {
            if (!HasPendingQte || PendingQteTarget == null || !PendingQteTarget.IsAlive)
            {
                PendingQteTarget = null;
                PendingQteTimeRemaining = 0f;
                return;
            }

            var target = PendingQteTarget;
            PendingQteTarget = null;
            PendingQteTimeRemaining = 0f;
            ForceOverheatRemaining = database.Rules.ForceOverheatTurn;
            ForceOverheatTickRemaining = database.Rules.ForceOverheatTick;
            LastActor = null;
            LastTarget = target;
            LastTurnLabel = "ALLY TURN - FORCE CHAIN";
            LastActionSummary = $"Force Chain triggered against {target.DisplayName}.";

            var chainActors = Allies.Where(unit => unit.IsAlive).OrderBy(unit => unit.NextActionTick).Take(2).ToList();
            AddLog($"Force Chain success. {chainActors.Count} allies cut into the timeline.");

            foreach (var actor in chainActors)
            {
                var skill = ResolveSkill(actor.ChainSkillId);
                if (skill == null || !target.IsAlive)
                {
                    continue;
                }

                var result = ResolveAttack(actor, target, skill, bpSpent: 0, shieldHitMultiplier: 1);
                LastActor = actor;
                LastTarget = target;
                LastActionSummary = result.Summary;
            }

            if (target.IsAlive)
            {
                target.IsStunned = true;
                AddLog($"{target.DisplayName} remains stunned for Lockdown.");
            }
        }

        public string TimelineText()
        {
            return string.Join("\n", Units.Where(unit => unit.IsAlive)
                .OrderBy(unit => unit.NextActionTick)
                .Take(6)
                .Select(unit => $"{(unit.Team == BattleTeam.Ally ? "ALLY " : "ENEMY")}  {unit.DisplayName}  T+{Mathf.Max(0f, unit.NextActionTick - GlobalTick):0.0}"));
        }

        private IEnumerable<BattleUnit> Units => Allies.Concat(Enemies);

        private void BeginAllyCommand(BattleUnit actor)
        {
            ActiveCommandUnit = actor;
            actor.CurrentBp = Mathf.Min(actor.CurrentBp + 1, actor.MaxBp);
            LastActor = actor;
            LastTarget = null;
            LastTurnLabel = "ALLY TURN - INPUT WAIT";
            LastActionSummary = $"{actor.DisplayName}'s turn. BP charged to {actor.CurrentBp}/{actor.MaxBp}.";
            AddLog(LastActionSummary);
        }

        private bool CanExecuteAllyCommand(out BattleUnit actor, out BattleUnit target)
        {
            actor = ActiveCommandUnit;
            target = PickTarget(Enemies);
            return actor != null && actor.IsAlive && target != null && target.IsAlive;
        }

        private void EndCommandTurn(BattleUnit actor, float costSkill, float advanceGauge, string label)
        {
            actor.NextActionTick = GlobalTick + Mathf.Max(1f, actor.AvBase * (1f + costSkill) - advanceGauge);
            ActiveCommandUnit = null;
            LastTurnLabel = label;
        }

        private void AddPartyMember(string characterId)
        {
            if (!database.Characters.TryGetValue(characterId, out var character))
            {
                return;
            }

            Allies.Add(new BattleUnit
            {
                UnitId = character.CharacterId,
                DisplayName = database.Text(character.StringId, character.Name),
                Team = BattleTeam.Ally,
                WeaponStyle = character.WeaponStyle,
                WeaknessStyle = character.WeaponStyle,
                Spd = character.DefaultSpd,
                MaxHp = character.MaxHp,
                Hp = character.MaxHp,
                MaxShield = character.MaxShield,
                Shield = character.MaxShield,
                MaxBp = Mathf.Max(1, character.MaxBpLimit),
                CurrentBp = 0,
                BasicSkillId = character.BasicSkillId,
                ChainSkillId = character.ChainSkillId
            });
        }

        private BattleUnit CreateEnemy(MonsterStat monster, int slotIndex)
        {
            return new BattleUnit
            {
                UnitId = monster.MonsterId.ToString(),
                DisplayName = database.Text(monster.StringId, monster.Name),
                Team = BattleTeam.Enemy,
                WeaponStyle = monster.WeaknessStyle,
                WeaknessStyle = monster.WeaknessStyle,
                Spd = monster.DefaultSpd,
                MaxHp = monster.MaxHp,
                Hp = monster.MaxHp,
                MaxShield = monster.MaxShield,
                Shield = monster.MaxShield,
                BasicSkillId = monster.BasicSkillId
            };
        }

        private BattleUnit PickTarget(List<BattleUnit> candidates)
        {
            return candidates.FirstOrDefault(unit => unit.IsAlive);
        }

        private SkillCombat ResolveSkill(string skillId)
        {
            database.Skills.TryGetValue(skillId, out var skill);
            return skill;
        }

        private BattleActionResult ResolveAttack(BattleUnit actor, BattleUnit target, SkillCombat skill, int bpSpent, int shieldHitMultiplier)
        {
            if (skill == null)
            {
                return null;
            }

            var shieldDamage = 0;
            if (target.MaxShield > 0 && actor.WeaponStyle == target.WeaknessStyle)
            {
                shieldDamage = Mathf.Min(target.Shield, Mathf.Max(1, skill.ShieldBreakPower) * Mathf.Max(1, shieldHitMultiplier));
                target.Shield -= shieldDamage;
                if (target.Shield <= 0)
                {
                    target.IsBroken = true;
                }
            }

            var breakMultiplier = target.IsBroken ? database.Rules.BreakDamageMultiplier : 1f;
            var ccModifier = target.IsStunned || target.IsWeakened ? database.Rules.StunDamageModifier : 1f;
            var damageFloat = skill.BaseDamage * (1f + bpSpent * 0.5f) * breakMultiplier * ccModifier;
            if (target.IsGuarded)
            {
                damageFloat *= 0.5f;
            }
            var damage = Mathf.Max(1, Mathf.RoundToInt(damageFloat));
            target.Hp = Mathf.Max(0, target.Hp - damage);

            var ccApplied = TryApplyCc(skill, target);
            var qteTriggered = actor.Team == BattleTeam.Ally
                && skill.QteTrigger
                && ccApplied
                && ForceOverheatTickRemaining <= 0
                && target.IsAlive;

            if (qteTriggered)
            {
                PendingQteTarget = target;
                PendingQteTimeRemaining = database.Rules.QteWindowTime;
            }

            if (target.Hp <= 0)
            {
                target.IsStunned = false;
                target.IsWeakened = false;
            }

            var result = new BattleActionResult
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                Damage = damage,
                ShieldDamage = shieldDamage,
                TriggeredQte = qteTriggered,
                TargetDefeated = target.Hp <= 0,
                Summary = $"{actor.DisplayName} used {database.Text(skill.StringId, skill.SkillName)} on {target.DisplayName}: {damage} damage"
            };

            AddLog(result.Summary + (shieldDamage > 0 ? $" / Shield -{shieldDamage}" : string.Empty));
            if (target.IsBroken && shieldDamage > 0)
            {
                AddLog($"{target.DisplayName} shield broken. Break multiplier active.");
            }

            if (ccApplied)
            {
                AddLog($"{target.DisplayName} affected by {skill.CcType}.");
            }

            if (qteTriggered)
            {
                AddLog($"Force Chain ready. Tap within {database.Rules.QteWindowTime:0.0}s.");
            }

            if (target.Hp <= 0)
            {
                AddLog($"{target.DisplayName} defeated.");
            }

            return result;
        }

        private bool TryApplyCc(SkillCombat skill, BattleUnit target)
        {
            if (skill.CcType == CcType.None || skill.CcChance <= 0f || target.Hp <= 0)
            {
                return false;
            }

            if (random.NextDouble() > skill.CcChance)
            {
                return false;
            }

            if (skill.CcType == CcType.Stun || skill.CcType == CcType.Freeze)
            {
                target.IsStunned = true;
            }

            if (skill.CcType == CcType.Weaken)
            {
                target.IsWeakened = true;
            }

            return true;
        }

        private void AddLog(string message)
        {
            Log.Add(message);
            while (Log.Count > 9)
            {
                Log.RemoveAt(0);
            }

            Debug.Log($"[ForceHarmony] {message}");
        }
    }
}
