using UnityEngine;
using Runtime;
using RMS.Fishing;
using RMS.Data;


namespace RMS.UI
{
    // 보스 스테이지 패널의 활성/비활성과 메인 패널 토글을 담당한다.
    // BossStageUI는 HP/타이머/연출만 담당하고, 패널 On/Off는 여기서 제어한다.
    public class BossStageActivator : MonoBehaviour
    {
        [SerializeField] private FishSpawnManager _spawnManager;
        [SerializeField] private RuntimeStateController _runtimeStateController;

        [Header("보스 스테이지 진입 시 숨길 UI")]
        [Tooltip("낚시 대회 참가 버튼 등 보스 스테이지에서 보이면 안 되는 UI 오브젝트 목록")]
        [SerializeField] private GameObject[] _hideOnBossStage;

        private bool _waitingForRewardConfirm;
        private bool _isBossStageActive;


        private void Awake()
        {
            if (_spawnManager == null)
                _spawnManager = FindFirstObjectByType<FishSpawnManager>();

            if (_spawnManager != null)
            {
                _spawnManager.OnStageChanged += HandleStageChanged;
                _spawnManager.OnBossCleared += HandleBossCleared;
                _spawnManager.OnBossTimeLimitExpired += HandleBossTimeLimitExpired;
            }
        }

        private void OnDestroy()
        {
            if (_spawnManager != null)
            {
                _spawnManager.OnStageChanged -= HandleStageChanged;
                _spawnManager.OnBossCleared -= HandleBossCleared;
                _spawnManager.OnBossTimeLimitExpired -= HandleBossTimeLimitExpired;
            }
        }

        public bool IsBossStageActive => _isBossStageActive;

        public void OnRewardConfirmed()
        {
            _waitingForRewardConfirm = false;
            SetHideObjects(true);
        }

        public void ReapplyBossHide()
        {
            if (_isBossStageActive)
                SetHideObjects(false);
        }

        private void HandleBossCleared(BossData boss)
        {
            _waitingForRewardConfirm = true;
        }
       
        private void HandleBossTimeLimitExpired()
        {
            _waitingForRewardConfirm = false;
            _isBossStageActive = false;
        }

        private void HandleStageChanged(StageData stage)
        {
            bool isBoss = stage != null && stage.IsBossStage;
            _isBossStageActive = isBoss;

            if (isBoss)
            {
                _runtimeStateController.CurrentState = RuntimeState.BOSSSTAGE;
                SetHideObjects(false);
            }
            else
            {
                if (_waitingForRewardConfirm) return;

                _isBossStageActive = false;
                _runtimeStateController.CurrentState = RuntimeState.MAINSTAGE;
                SetHideObjects(true);
            }
        }

        private void SetHideObjects(bool active)
        {
            if (_hideOnBossStage == null) return;
            foreach (GameObject obj in _hideOnBossStage)
                if (obj != null) obj.SetActive(active);
        }
    }
}

