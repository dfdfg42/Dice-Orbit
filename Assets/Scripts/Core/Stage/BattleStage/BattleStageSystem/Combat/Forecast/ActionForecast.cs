using System.Collections.Generic;
using DiceOrbit.Data;
using DiceOrbit.Data.Tile;

namespace DiceOrbit.Core.Forecast
{
    public enum ForecastAttackKind
    {
        /// <summary>이번 턴에 이미 공격했다 — 이동만 한다.</summary>
        AlreadyAttacked,
        Basic,
        Combo,
        /// <summary>때릴 대상이 없어 공격이 나가지 않는다.</summary>
        NoTarget,
    }

    public enum ForecastComboChange
    {
        None,
        /// <summary>강화 공격 성공 — 다음 단계로.</summary>
        Advance,
        /// <summary>3단계 발동 — 콤보 완성, 처음으로.</summary>
        Finish,
        /// <summary>주사위가 조건을 못 맞춰 쌓인 콤보가 끊긴다.</summary>
        BrokenByDice,
        /// <summary>조건은 맞았지만 때릴 대상이 없어 쌓인 콤보가 초기화된다.</summary>
        BrokenByNoTarget,
    }

    /// <summary>대상 하나에 대한 예고 결과.</summary>
    public sealed class ForecastTargetResult
    {
        public Unit Target;
        public string Name;

        /// <summary>실제로 들어가는 타격 수 (도중에 쓰러지면 그 뒤 타격은 세지 않는다).</summary>
        public int Hits;
        /// <summary>첫 타격의 최종 피해 (보정 반영, 방어도 적용 전).</summary>
        public int DamagePerHit;

        public int HpBefore;
        public int HpAfter;
        public int ArmorBefore;
        public int ArmorAfter;
        public bool Invulnerable;
        /// <summary>대상의 회피 확률(%). 예고 수치에는 넣지 않고 알리기만 한다.</summary>
        public float DodgeChance;

        public int HpLoss => HpBefore - HpAfter;
        public int ArmorLoss => ArmorBefore - ArmorAfter;
        public bool Lethal => HpAfter <= 0;
    }

    /// <summary>도착 타일을 노리는 몬스터 행동.</summary>
    public sealed class ForecastThreat
    {
        public Monster Monster;
        public string MonsterName;
        public string SkillName;
        /// <summary>피해를 주는 행동인가 (공격 계열). 아니면 설치·특수 행동.</summary>
        public bool IsAttack;
    }

    /// <summary>
    /// 캐릭터가 이 주사위로 움직이면 벌어질 일 (행동 예고 2026-10-04). ActionForecaster.Build가 만든다.
    /// 만드는 것만으로는 아무 상태도 바뀌지 않는다.
    /// </summary>
    public sealed class ActionForecast
    {
        public Character Character;
        public int DiceValue;

        /// <summary>밟는 타일들 (순서대로, 도착 타일 포함). 걸음 수가 0이면 비어 있다.</summary>
        public readonly List<TileData> Path = new List<TileData>();
        public TileData Destination;

        public ForecastAttackKind Kind;
        /// <summary>나가는 공격의 이름 (강화 공격 단계명 또는 기본 공격).</summary>
        public string AttackName;
        /// <summary>콤보 단계 (0~2). 강화 공격이 아니면 -1.</summary>
        public int ComboStage = -1;
        public ForecastComboChange ComboChange;
        /// <summary>강화 공격의 주사위 조건 문구 (예: "주사위 눈 4 이상"). 없으면 빈 문자열.</summary>
        public string GateCondition = string.Empty;

        public readonly List<ForecastTargetResult> Targets = new List<ForecastTargetResult>();

        /// <summary>가는 길에(도착 걸음 포함) 타일이 일으키는 일.</summary>
        public readonly List<ForecastNote> PathNotes = new List<ForecastNote>();
        /// <summary>도착 타일에서 턴을 마치면 벌어지는 일.</summary>
        public readonly List<ForecastNote> EndTurnNotes = new List<ForecastNote>();
        /// <summary>도착 타일(또는 이 캐릭터)을 노리는 몬스터 행동.</summary>
        public readonly List<ForecastThreat> Threats = new List<ForecastThreat>();
    }
}
