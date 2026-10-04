using System.Collections.Generic;

namespace DiceOrbit.Data.Tile
{
    /// <summary>예고 한 줄의 성격 — 카드에서 색으로 구분한다 (나쁨 = 빨강, 좋음 = 초록).</summary>
    public enum ForecastTone
    {
        Neutral,
        Good,
        Bad,
    }

    /// <summary>예고 한 줄. 같은 문구가 여러 번 나오면 Count로 합쳐진다 ("지뢰 피해 5 ×2").</summary>
    public readonly struct ForecastNote
    {
        public readonly string Text;
        public readonly ForecastTone Tone;
        public readonly int Count;

        public ForecastNote(string text, ForecastTone tone, int count = 1)
        {
            Text = text;
            Tone = tone;
            Count = count;
        }
    }

    /// <summary>
    /// 타일 속성이 행동 예고에 적는 창구 (행동 예고 2026-10-04).
    /// TileAttribute.ForecastTraverse / ForecastEndTurn이 "지나가면 / 여기서 턴을 마치면 벌어질 일"을 적는다.
    /// 실제 효과는 내지 않는다 — 적기만 한다.
    /// </summary>
    public sealed class TileForecast
    {
        private readonly List<ForecastNote> _notes = new List<ForecastNote>();
        private readonly List<KeyValuePair<EffectType, int>> _grantedStatuses = new List<KeyValuePair<EffectType, int>>();

        public IReadOnlyList<ForecastNote> Notes => _notes;

        /// <summary>가는 길에 캐릭터가 얻을 상태 (종류, 값). 이번 공격의 피해 계산에 가상으로 끼워 넣는다 — 예: 시약 타일의 촉매.</summary>
        public IReadOnlyList<KeyValuePair<EffectType, int>> GrantedStatuses => _grantedStatuses;

        /// <summary>방금 물어본 속성이 이 통과로 사라지는가 (턴 종료 예고에서 빠진다).</summary>
        public bool Consumed { get; private set; }

        /// <summary>한 줄 적는다. 같은 문구·같은 성격이면 횟수만 올린다.</summary>
        public void Note(string text, ForecastTone tone = ForecastTone.Neutral)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            for (int i = 0; i < _notes.Count; i++)
            {
                if (_notes[i].Text != text || _notes[i].Tone != tone) continue;
                _notes[i] = new ForecastNote(text, tone, _notes[i].Count + 1);
                return;
            }
            _notes.Add(new ForecastNote(text, tone));
        }

        /// <summary>이 통과로 캐릭터가 상태를 얻는다. 같은 종류는 한 번만 기록한다 (비중첩 상태 기준).</summary>
        public void GrantStatus(EffectType type, int value)
        {
            if (HasPendingStatus(type)) return;
            _grantedStatuses.Add(new KeyValuePair<EffectType, int>(type, value));
        }

        /// <summary>이번 경로의 앞선 타일에서 이미 얻기로 된 상태인가 (불꽃 "한 턴에 하나만 끔" 같은 규칙용).</summary>
        public bool HasPendingStatus(EffectType type)
        {
            foreach (var pair in _grantedStatuses)
                if (pair.Key == type) return true;
            return false;
        }

        /// <summary>이 속성은 이번 통과로 사라진다 — 같은 타일의 턴 종료 효과는 발동하지 않는다.</summary>
        public void MarkConsumed() => Consumed = true;

        /// <summary>속성 하나를 묻기 직전에 예고 계산기가 부른다.</summary>
        public void BeginAttribute() => Consumed = false;
    }
}
