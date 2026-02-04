using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    public class RecruitUI : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
