using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DiceOrbit.UI.Skin;

namespace DiceOrbit.UI
{
    /// <summary>보상 진행 표시의 점 하나 (전리품 · 강화 · 주사위) — 씬 템플릿에 붙어 복제된다.</summary>
    public class RewardStepPip : MonoBehaviour
    {
        public enum State { Upcoming, Current, Done }

        [SerializeField] private Image dot;
        [SerializeField] private TextMeshProUGUI label;

        public void Bind(string text) => label.text = text;

        public void SetState(State state)
        {
            var skin = UiSkin.Current;
            switch (state)
            {
                case State.Current:
                    skin.ApplyCircle(dot, skin.Primary);
                    label.color = skin.Ink;
                    label.fontStyle = FontStyles.Bold;
                    break;
                case State.Done:
                    skin.ApplyCircle(dot, skin.Ink);
                    label.color = skin.InkMuted;
                    label.fontStyle = FontStyles.Normal;
                    break;
                default:
                    skin.ApplyCircle(dot, skin.PaperDeep);
                    label.color = skin.InkMuted;
                    label.fontStyle = FontStyles.Normal;
                    break;
            }
        }
    }
}
