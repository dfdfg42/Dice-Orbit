using System.Collections.Generic;

namespace DiceOrbit.Core.Run
{
    /// <summary>보상 화면의 박자. Upgrade·Dice는 결정 박자, Wrap은 [계속]만 있는 마무리, Finished는 화면 종료.</summary>
    public enum RewardBeat
    {
        Upgrade,
        Dice,
        Wrap,
        Finished,
    }

    /// <summary>
    /// 보상 화면의 박자 순서와 종료 규칙 (보상 리워크 2026-10-03) — 순수 C#, 자가 테스트 대상.
    ///
    /// 규칙:
    ///  - 결정 박자는 강화 → 주사위 순서. 없는 박자는 건너뛴다.
    ///  - 마지막 결정 박자가 끝나면 자동 종료(Finished)한다 — 별도 [계속] 클릭이 없다.
    ///  - 단, 결정 박자가 하나도 없거나 못 받은 전리품(가방 가득 포션)이 남아 있으면 Wrap 박자를 거친다
    ///    — 전리품을 볼 틈도 없이 넘어가거나, 못 받은 것이 조용히 사라지지 않게.
    /// </summary>
    public sealed class RewardFlow
    {
        private readonly List<RewardBeat> _choiceBeats = new List<RewardBeat>();
        private int _index;
        private bool _wrapResolved;

        public RewardFlow(bool hasUpgrade, bool hasDice)
        {
            if (hasUpgrade) _choiceBeats.Add(RewardBeat.Upgrade);
            if (hasDice) _choiceBeats.Add(RewardBeat.Dice);
        }

        /// <summary>이 보상에 있는 결정 박자 (진행 표시용).</summary>
        public IReadOnlyList<RewardBeat> ChoiceBeats => _choiceBeats;

        /// <summary>못 받은 전리품이 남아 있다 — RewardUI가 포션 수령 상태에 맞춰 갱신한다.</summary>
        public bool HasBlockedLoot { get; set; }

        public RewardBeat Current
        {
            get
            {
                if (_index < _choiceBeats.Count) return _choiceBeats[_index];
                if (!_wrapResolved && (_choiceBeats.Count == 0 || HasBlockedLoot)) return RewardBeat.Wrap;
                return RewardBeat.Finished;
            }
        }

        public bool IsFinished => Current == RewardBeat.Finished;

        /// <summary>현재 박자를 끝낸다 (확정이든 건너뛰기든). Finished에서 부르면 예외.</summary>
        public void Resolve()
        {
            switch (Current)
            {
                case RewardBeat.Upgrade:
                case RewardBeat.Dice:
                    _index++;
                    break;
                case RewardBeat.Wrap:
                    _wrapResolved = true;
                    break;
                default:
                    throw new System.InvalidOperationException("[RewardFlow] 이미 끝난 보상 흐름을 다시 끝낼 수 없다.");
            }
        }
    }
}
