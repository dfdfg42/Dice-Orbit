using UnityEngine;
using DiceOrbit.Core;

namespace DiceOrbit.UI
{
    public class RewardUI : MonoBehaviour
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
