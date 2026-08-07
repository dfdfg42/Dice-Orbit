using UnityEngine;
using DiceOrbit.Core.Pipeline;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// 이벤트(조화의 정령/제련소)로 설치되는 타일 5종. 설치는 EventRunState가 전투 시작마다 수행.
    /// 공격 보정은 파이프라인 OnCalculateOutput에서 (IsSimulation 게이트 없음 — 예상 피해 프리뷰에도 반영),
    /// 회복/피해는 파이프라인 컨텍스트 경유 (직접 HP 대입 금지).
    /// 타일 훅(OnTraverse/OnEndTurn)은 Character 전용 — 몬스터에는 미적용 (현 구조).
    /// </summary>
    public class SharpTile : TileAttribute
    {
        public SharpTile(int percent = 10) : base(TileAttributeType.Sharp, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f + Value / 100f;
        }

        public override string GetDescription() => $"이 타일에서 공격 시 피해 +{Value}%";
    }

    public class DullTile : TileAttribute
    {
        public DullTile(int percent = 10) : base(TileAttributeType.Dull, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            if (!(context.SourceUnit is Core.Character c) || c.CurrentTile != Owner) return;
            atk.OutputValue *= 1f - Value / 100f;
        }

        public override string GetDescription() => $"이 타일에서 공격 시 피해 -{Value}%";
    }

    /// <summary>통과(마지막 걸음 포함) 및 턴 종료 시 방어도. OnArrive는 쓰지 않는다 —
    /// 이동 루프가 도착 타일에도 OnTraverse를 호출하므로 (Character.cs) 겹치면 이중 지급.</summary>
    public class SturdyTile : TileAttribute
    {
        public SturdyTile(int armor = 10) : base(TileAttributeType.Sturdy, armor, -1) { }

        public override void OnTraverse(Core.Character character) => Grant(character);
        public override void OnEndTurn(Core.Character character) => Grant(character);

        private void Grant(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            character.Stats.TempArmor += Value;
            DiceOrbit.UI.CombatNotifier.Notify(character, $"방어도 +{Value}", new Color(0.6f, 0.75f, 1f));
        }

        public override string GetDescription() => $"통과·턴 종료 시 방어도 +{Value}";
    }

    public class HarmonyTile : TileAttribute
    {
        public HarmonyTile(int percent = 5) : base(TileAttributeType.Harmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var heal = new HealContext(null, character, "조화", amount);
            CombatPipeline.Instance?.Process(heal);
        }

        public override string GetDescription() => $"이 타일에서 턴 종료 시 최대체력 {Value}% 회복";
    }

    public class DisharmonyTile : TileAttribute
    {
        public DisharmonyTile(int percent = 5) : base(TileAttributeType.Disharmony, percent, -1) { }

        public override void OnEndTurn(Core.Character character)
        {
            if (character == null || !character.IsAlive || character.Stats == null) return;
            int amount = Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHP * Value / 100f));
            var hit = new AttackContext(null, character, "부조화", amount);
            CombatPipeline.Instance?.Process(hit);
        }

        public override string GetDescription() => $"이 타일에서 턴 종료 시 최대체력 {Value}% 피해";
    }

    /// <summary>
    /// 홀짝 극성 타일 (TilePolarityInstaller가 전투 시작마다 0번 제외 전 타일에 설치).
    /// 공격 타일: 이 타일 위 캐릭터의 공격 피해 +V% 그리고 받는 피해 +V% (딜↑·받피↑ = 글래스 캐논).
    /// Sharp/Dull과 동일하게 OnCalculateOutput에서 보정(IsSimulation 게이트 없음 → 예상 피해 프리뷰 반영).
    /// 실제 적용(비-시뮬) 시 CombatNotifier로 플로팅 알림. 몬스터엔 CurrentTile이 없어 캐릭터 전용.
    /// </summary>
    public class AttackTile : TileAttribute
    {
        private static readonly Color Tint = new Color(1f, 0.42f, 0.38f);   // 붉은 계열

        public AttackTile(int percent = 30) : base(TileAttributeType.Attack, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            float mult = 1f + Value / 100f;

            // 이 타일 위 캐릭터가 공격 → 주는 피해 +V%
            if (context.SourceUnit is Core.Character sc && sc.CurrentTile == Owner)
            {
                atk.OutputValue *= mult;
                if (!context.IsSimulation) DiceOrbit.UI.CombatNotifier.Notify(sc, $"공격 +{Value}%", Tint);
            }
            // 이 타일 위 캐릭터가 피격 → 받는 피해 +V%
            if (context.Target is Core.Character tc && tc.CurrentTile == Owner)
            {
                atk.OutputValue *= mult;
                if (!context.IsSimulation) DiceOrbit.UI.CombatNotifier.Notify(tc, $"받는 피해 +{Value}%", Tint);
            }
        }

        public override string GetDescription() => $"이 타일에서 공격 피해 +{Value}%, 받는 피해 +{Value}%";
    }

    /// <summary>
    /// 방어 타일: 이 타일 위 캐릭터의 공격 피해 -V% 그리고 받는 피해 -V% (딜↓·받피↓ = 탱킹).
    /// 구조는 AttackTile과 대칭.
    /// </summary>
    public class DefenseTile : TileAttribute
    {
        private static readonly Color Tint = new Color(0.42f, 0.66f, 1f);   // 푸른 계열

        public DefenseTile(int percent = 30) : base(TileAttributeType.Defense, percent, -1) { }

        public override void OnReact(CombatTrigger trigger, CombatContext context)
        {
            if (trigger != CombatTrigger.OnCalculateOutput) return;
            if (!(context is AttackContext atk)) return;
            float mult = 1f - Value / 100f;

            // 이 타일 위 캐릭터가 공격 → 주는 피해 -V%
            if (context.SourceUnit is Core.Character sc && sc.CurrentTile == Owner)
            {
                atk.OutputValue *= mult;
                if (!context.IsSimulation) DiceOrbit.UI.CombatNotifier.Notify(sc, $"공격 -{Value}%", Tint);
            }
            // 이 타일 위 캐릭터가 피격 → 받는 피해 -V%
            if (context.Target is Core.Character tc && tc.CurrentTile == Owner)
            {
                atk.OutputValue *= mult;
                if (!context.IsSimulation) DiceOrbit.UI.CombatNotifier.Notify(tc, $"받는 피해 -{Value}%", Tint);
            }
        }

        public override string GetDescription() => $"이 타일에서 공격 피해 -{Value}%, 받는 피해 -{Value}%";
    }
}
