using JHS.Fishing;
using UnityEngine;


namespace RMS.Fishing
{
    public class FishingInitializer : MonoBehaviour
    {
        [SerializeField] private AutoFishingController _autoFishing;
        [SerializeField] private FishingMinigameSelector _minigameSelector;

        private void Awake()
        {
            _autoFishing.SetManualFishing(_minigameSelector);
        }
    }
}