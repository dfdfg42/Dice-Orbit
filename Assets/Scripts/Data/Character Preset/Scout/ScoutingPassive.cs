using UnityEngine;
using System.Collections.Generic;
using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using DiceOrbit.Data.Passives;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Data.Characters.Scout
{
    // ── 정찰 버프 (런타임, 아군 PassiveManager에 추가됨) ──────────────────

    [System.Serializable]
    public class ScoutDamageBuff : IPassive
    {
        private float   multiplier;
        private int     remainingTurns;
        private Unit    owner;

        public string PassiveName          => "정찰 강화";
        public string Description          => "정찰병이 지나간 타일에서 피해량 증가";
        public bool   IsStackable          => false;
        public int    CurrentLevel         => 1;
        public int    Priority             => 50;

        public ScoutDamageBuff(float multiplier)
        {
            this.multiplier    = multiplier;
            remainingTurns     = 2;
        }

        public void     Initialize(Unit unit)            => owner = unit;
        public void     SetLevel(int level)              { }
        public IPassive Clone()                          => new ScoutDamageBuff(multiplier);
        public void     OnOwnerSelected(Character c)     { }
        public void     OnOwnerDeselected()              { }

        public bool AllowSamePassive(IPassive incoming)
        {
            // 중복 스택 불가, 단 기존 버프의 지속시간을 갱신
            if (incoming is ScoutDamageBuff next)
                remainingTurns = Mathf.Max(remainingTurns, next.remainingTurns);
            return false;
        }

        public string GetDynamicDescription() =>
            $"피해량 +{(multiplier - 1f) * 100f:0.#}%  ({remainingTurns}턴)";

        public void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (owner == null) return;

            // 턴 시작마다 잔여 턴 차감 → 0이면 자기 자신 제거
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn &&
                context.SourceUnit == owner)
            {
                remainingTurns--;
                if (remainingTurns <= 0)
                {
                    var pm = (owner as MonoBehaviour)?.GetComponent<DiceOrbit.Systems.Passives.PassiveManager>();
                    pm?.RemovePassive(this);
                }
                return;
            }

            // 공격 데미지 배율 적용
            if (trigger == CombatTrigger.OnCalculateOutput &&
                context.Action.Type == ActionType.Attack &&
                context.SourceUnit == owner)
            {
                context.OutputValue *= multiplier;
            }
        }
    }

    // ── 정찰 패시브 ──────────────────────────────────────────────────────

    [System.Serializable]
    public class ScoutingPassive : CharacterPassive
    {
        [Header("레벨별 피해 배율 (1.0 = 100%, 2.0 = +100% 증가)")]
        [SerializeField] private float[] multiplierByLevel = { 1.05f, 1.26f, 1.48f, 1.74f, 2.00f };

        public override int Priority => 80;

        // 이동 직전 타일 (턴 시작 시 기록)
        private TileData _preMoveStartTile;

        public override string GetDynamicDescription()
        {
            float m = ResolveMultiplier(currentLevel);
            return $"이동 경로 아군 피해량 +{(m - 1f) * 100f:0.#}% (현재 + 다음 턴)";
        }

        protected override void ApplyLevel(int level) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (context == null || context.Action == null || owner == null) return;
            if (context.SourceUnit != owner) return;

            // 턴 시작: 이동 시작 위치 기록
            if (trigger == CombatTrigger.OnPreAction &&
                context.Action.Type == ActionType.OnStartTurn)
            {
                _preMoveStartTile = (owner as Character)?.CurrentTile;
                return;
            }

            // 이동 완료: 경로 위 아군에게 버프 부여
            if (trigger == CombatTrigger.OnPostAction &&
                context.Action.Type == ActionType.Move)
            {
                int steps = Mathf.RoundToInt(context.Action.BaseValue);
                ApplyBuffOnPath(steps);
                _preMoveStartTile = (owner as Character)?.CurrentTile;
            }
        }

        private void ApplyBuffOnPath(int steps)
        {
            var scout = owner as Character;
            if (scout == null || scout.CurrentTile == null) return;

            var partyManager = PartyManager.Instance;
            if (partyManager == null) return;

            // 도착 타일부터 prevTile 방향으로 steps 개 수집
            var traversedTiles = new List<TileData>(steps);
            var tile = scout.CurrentTile;
            for (int i = 0; i < steps && tile != null; i++)
            {
                traversedTiles.Add(tile);
                tile = tile.PreviousTile;
            }

            float multiplier = ResolveMultiplier(currentLevel);
            var allies = partyManager.GetAliveCharacters();

            foreach (var ally in allies)
            {
                if (ally == null) continue;
                if (!traversedTiles.Contains(ally.CurrentTile)) continue;

                var pm = ally.GetComponent<DiceOrbit.Systems.Passives.PassiveManager>();
                if (pm == null) continue;

                var buff = new ScoutDamageBuff(multiplier);
                buff.Initialize(ally);
                pm.AddPassive(buff);

                Debug.Log($"[정찰] {ally.Stats.CharacterName}에게 피해 +{(multiplier - 1f) * 100f:0.#}% 버프 적용");
            }
        }

        private float ResolveMultiplier(int level)
        {
            if (multiplierByLevel == null || multiplierByLevel.Length == 0)
                return 1.05f;
            int idx = Mathf.Clamp(level - 1, 0, multiplierByLevel.Length - 1);
            return Mathf.Max(1f, multiplierByLevel[idx]);
        }
    }
}
