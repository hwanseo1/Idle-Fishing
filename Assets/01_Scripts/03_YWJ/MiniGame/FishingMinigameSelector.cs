using System;
using System.Collections;
using JHS.Fishing;
using RMS.Data;
using UnityEngine;

namespace RMS.Fishing
{
    public class FishingMinigameSelector : MonoBehaviour, IManualFishing
    {
        [Header("Minigames")]
        [SerializeField] private ManualFishingMinigame _manualFishingMinigame;
        [SerializeField] private DefenseMinigame _defenseMinigame;

        [Header("Chance")]
        [Range(0f, 1f)]
        [SerializeField] private float _defenseChance = 0.5f;

        public IEnumerator RunMiniGame(FishData fish, Action<ManualResult> onDone)
        {
            IManualFishing selected = SelectMinigame();

            if (selected == null)
            {
                onDone?.Invoke(ManualResult.Fail);
                yield break;
            }

            yield return selected.RunMiniGame(fish, onDone);
        }

        public IEnumerator RunMiniGame(BossData boss, Action<ManualResult> onDone)
        {
            IManualFishing selected = SelectMinigame();

            if (selected == null)
            {
                onDone?.Invoke(ManualResult.Fail);
                yield break;
            }

            yield return selected.RunMiniGame(boss, onDone);
        }

        private IManualFishing SelectMinigame()
        {
            if (_manualFishingMinigame == null)
            {
                Debug.LogWarning("ManualFishingMinigame is not assigned. Defaulting to DefenseMinigame.");
                return _defenseMinigame;
            }

            if (_defenseMinigame == null)
            {
                Debug.LogWarning("DefenseMinigame is not assigned. Defaulting to ManualFishingMinigame.");
                return _manualFishingMinigame;
            }

            return UnityEngine.Random.value < _defenseChance
                ? _defenseMinigame
                : _manualFishingMinigame;
        }
    }
}