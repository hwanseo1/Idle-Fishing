using Fusion;
using JHS.Fishing;
using RMS.Data;
using RMS.Fishing;
using Crew;
using System.Collections.Generic;
using UnityEngine;


namespace RMS.Multiplay
{
    // 낚시 경쟁 미니게임(멀티) 플레이어. 고정 위치 스폰, 개인 기여도 점수 동기화,
    // 점수 변경 시 FishingGameManager의 전체 기여도/점수판 갱신을 담당한다.
    public class FishingPlayerController : NetworkBehaviour
    {
        #region Inspector
        [Header("희귀도별 기여도 배율 (멀티플레이 전용)")]
        [SerializeField] private float _commonContributionMultiplier = 2f;
        [SerializeField] private float _rareContributionMultiplier = 4f;
        [SerializeField] private float _epicContributionMultiplier = 6f;
        [SerializeField] private float _legendaryContributionMultiplier = 10f;

        [Header("진행도 기반 물고기 풀 (멀티플레이 전용)")]
        [Tooltip("싱글 모드 1지역 첫 스테이지(1-1). 여기서부터 NextStage를 따라가며 본인의 maxStageId까지 FishEntries를 누적한다.")]
        [SerializeField] private StageData _firstSingleStage;

        [Header("멀티플레이 전용 캐릭터 크기 배율")]
        [Tooltip("원본 프리팹 크기는 그대로 두고, 스폰된 인스턴스에만 곱해서 적용한다. 1 = 원본 크기.")]
        [SerializeField] private float _multiplayScaleMultiplier = 3.0f;
        #endregion


        #region 네트워크 동기화
        [Networked] public int PlayerIndex { get; set; } // 1, 2, 3
        [Networked] public int Score { get; set; }
        private int _prevScore;

        // 1번 슬롯 대표 선원의 CrewDatabase 인덱스. -1 = 아직 미설정(또는 1번 슬롯 비어있음).
        [Networked] public int RepresentativeCrewIndex { get; set; } = -1;
        private int _prevCrewIndex = -2; // -1(미설정값)과 구분하기 위해 -2로 시작 → 최초 1회는 반드시 갱신됨
        #endregion


        #region 로컬
        private AutoFishingController _autoFishing;
        private GameObject _spawnedCrewVisual;
        #endregion


        #region Fusion 생명주기
        public override void Spawned()
        {
            ApplyMultiplayScale();
            RefreshScoreUI();

            // 본인 클라이언트만 구독 (타인의 낚시 결과가 내 점수에 합산되는 것을 방지)
            if (HasInputAuthority)
            {
                _autoFishing = FindFirstObjectByType<AutoFishingController>();
                if (_autoFishing != null)
                    _autoFishing.OnFishCaught += HandleFishCaught;
                else
                    Debug.LogWarning("[FishingPlayerController] AutoFishingController를 찾을 수 없습니다.");

                ApplyProgressBasedFishPool();
                RequestRepresentativeCrewSync();
            }
        }

        private void ApplyMultiplayScale()
        {
            if (Mathf.Approximately(_multiplayScaleMultiplier, 1f)) return;
            transform.localScale *= _multiplayScaleMultiplier;
        }

        // 본인의 maxStageId까지 누적된 FishEntries 풀을 계산해 FishSpawnManager에 주입한다.
        // (멀티 매치는 같은 풀이 아니라 플레이어 개인별로 다른 풀을 사용 — 진행도가 적은 플레이어는
        //  본인이 깬 스테이지까지의 물고기만, 진행도가 많은 플레이어는 그만큼 더 넓은 풀에서 낚인다.)
        private void ApplyProgressBasedFishPool()
        {
            if (_firstSingleStage == null)
            {
                Debug.LogWarning("[FishingPlayerController] _firstSingleStage가 비어 있어 진행도 기반 물고기 풀을 적용할 수 없습니다.");
                return;
            }

            var spawnManager = FindFirstObjectByType<FishSpawnManager>();
            if (spawnManager == null)
            {
                Debug.LogWarning("[FishingPlayerController] FishSpawnManager를 찾을 수 없어 진행도 기반 물고기 풀을 적용할 수 없습니다.");
                return;
            }

            string maxStageId = PlayFabDataStore.Instance != null
                ? PlayFabDataStore.Instance.GetPlayerInfo()?.stage?.maxStageId
                : null;

            if (string.IsNullOrEmpty(maxStageId))
            {
                Debug.LogWarning("[FishingPlayerController] maxStageId를 가져오지 못해 1-1 기준으로 폴백합니다.");
                maxStageId = "1-1";
            }

            FishEntry[] pool = StageData.GetFishEntriesUpToStage(_firstSingleStage, maxStageId);
            StageData rarityWeightStage = StageData.GetRarityWeightStage(_firstSingleStage, maxStageId);

            spawnManager.SetMultiplayFishPool(pool, rarityWeightStage);

            string poolNames = string.Join(", ", System.Array.ConvertAll(pool,
                e => e.fishData != null ? e.fishData.DisplayName : "?"));

            Debug.Log($"<color=#00FFAA>[FishingPlayerController] 진행도 기반 물고기 풀 적용: maxStageId={maxStageId}, " +
                $"누적 종 수={pool.Length}, 가중치 기준 스테이지={(rarityWeightStage != null ? rarityWeightStage.StageId : "null")}, " +
                $"풀=[{poolNames}]</color>");

            ShowMyStageIdUI(maxStageId);
        }

        // 본인의 maxStageId를 화면 UI에 표시
        private void ShowMyStageIdUI(string maxStageId)
        {
            var gameManager = FindFirstObjectByType<FishingGameManager>();
            if (gameManager != null)
                gameManager.UpdateMyStageIdUI(maxStageId);
        }


        #region 대표 선원 외형 (멀티플레이 전용)
        // 본인의 1번 슬롯(SlotIndex == 0) 선원을 찾아 CrewDatabase 인덱스로 변환 후 Host에 전파.
        // 화면 표시 전용이며, 패시브/스탯 계산은 CrewManager._equippedCrews(전체 장착 선원) 그대로 사용한다.
        private void RequestRepresentativeCrewSync()
        {
            if (CrewManager.Instance == null)
            {
                Debug.LogWarning("[FishingPlayerController] CrewManager.Instance가 없어 대표 선원을 적용할 수 없습니다.");
                return;
            }

            var firstSlot = CrewManager.Instance.CrewSlots.Find(s => s.SlotIndex == 0);
            string crewId = firstSlot?.EquippedCrew?.CrewId;

            if (string.IsNullOrEmpty(crewId))
            {
                Debug.LogWarning("[FishingPlayerController] 1번 슬롯에 배치된 선원이 없어 대표 선원 외형을 적용할 수 없습니다.");
                return;
            }

            var database = CrewManager.Instance.CrewDataBase;
            int index = database != null ? database.Crews.FindIndex(c => c.CrewId == crewId) : -1;

            if (index < 0)
            {
                Debug.LogWarning($"[FishingPlayerController] CrewDatabase에서 CrewId={crewId}를 찾을 수 없습니다.");
                return;
            }

            Rpc_SetRepresentativeCrewIndex(index);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SetRepresentativeCrewIndex(int index)
        {
            RepresentativeCrewIndex = index;
        }

        // RepresentativeCrewIndex 동기화 시 모든 클라이언트(본인 포함)에서 호출됨.
        private void ApplyCrewVisual(int index)
        {
            if (_spawnedCrewVisual != null)
            {
                Destroy(_spawnedCrewVisual);
                _spawnedCrewVisual = null;
            }

            if (index < 0) return;

            var database = CrewManager.Instance != null ? CrewManager.Instance.CrewDataBase : null;
            if (database == null || index >= database.Crews.Count)
            {
                Debug.LogWarning($"[FishingPlayerController] CrewDatabase 조회 실패. index={index}");
                return;
            }

            CrewData crewData = database.Crews[index];
            if (crewData == null || crewData.CrewPrefab == null)
            {
                Debug.LogWarning($"[FishingPlayerController] CrewPrefab이 없습니다. index={index}");
                return;
            }

            _spawnedCrewVisual = Instantiate(crewData.CrewPrefab, transform.position, transform.rotation, transform);
        }
        #endregion


        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            StopCollectingMatchResults();

            if (_spawnedCrewVisual != null)
                Destroy(_spawnedCrewVisual);
        }

        // 매치 종료(정산 UI 표시) 시점에 즉시 호출. 디스폰을 기다리면 그 사이 자동낚시가
        // 계속 점수/포획 목록에 반영되므로, 정산 시작과 동시에 구독을 끊는다.
        public void StopCollectingMatchResults()
        {
            if (_autoFishing != null)
            {
                _autoFishing.OnFishCaught -= HandleFishCaught;
                _autoFishing = null;
            }
        }

        public override void Render()
        {
            if (Score != _prevScore)
            {
                _prevScore = Score;
                RefreshScoreUI();
            }

            if (RepresentativeCrewIndex != _prevCrewIndex)
            {
                _prevCrewIndex = RepresentativeCrewIndex;
                ApplyCrewVisual(RepresentativeCrewIndex);
            }
        }
        #endregion


        #region UI 활성화
        // 점수판은 FishingGameManager가 화면 고정 UI로 갖고 있어 여기선 처리 없음(호출부 호환용).
        public void ShowScoreUI() { }
        #endregion


        #region 낚시 이벤트
        // 다중 포획(ResolveManual) 발동 시 마릿수만큼 이 콜백이 그대로 여러 번 호출된다.
        // 호출 1회당 1마리분만 처리하면 되므로 별도 판정 없이 누적만 한다.
        private void HandleFishCaught(FishData fish, float contribution, bool isManual)
        {
            float rarityMultiplier = fish != null ? GetRarityContributionMultiplier(fish.Rarity) : 1f;
            int score = Mathf.Max(1, Mathf.RoundToInt(contribution * rarityMultiplier));

            AddScore(score);

            // if (fish != null)
            //     AddPendingCatch(fish.FishId);
        }

        private float GetRarityContributionMultiplier(FishRarity rarity)
        {
            switch (rarity)
            {
                case FishRarity.Rare: return _rareContributionMultiplier;
                case FishRarity.Epic: return _epicContributionMultiplier;
                case FishRarity.Legendary: return _legendaryContributionMultiplier;
                default: return _commonContributionMultiplier;
            }
        }
        #endregion


        #region [폐기] 매치 종료 후 정산용 포획 누적 (개인별, 가방 미반영)
        // 2026-06-26 변경: 아래 _pendingCatches 관련 코드는 더 이상 사용하지 않음.
        //
        // 의도: 매치 중 낚은 물고기를 fishId별 개수로만 누적(가방 미반영)해뒀다가,
        // 매치 종료 후 RewardService 연동 시 한 번에 가방으로 보낼 계획이었음.
        //
        // 만약 추후 "매치 중 낚은 물고기도 정산 후 지급" 방향으로 가게 된다면, 아래 주석을 풀고
        // 1) _pendingCatches를 Host로 전달하는 RPC, 2) Host의 서버 검증, 3) 물고기 itemId+count를
        // 받는 별도 CloudScript 핸들러를 새로 추가하는 작업이 필요함.
        //
        // private readonly Dictionary<string, int> _pendingCatches = new();
        //
        // public IReadOnlyDictionary<string, int> PendingCatches => _pendingCatches;
        //
        // private void AddPendingCatch(string fishId)
        // {
        //     if (string.IsNullOrEmpty(fishId)) return;
        //     _pendingCatches.TryGetValue(fishId, out int count);
        //     _pendingCatches[fishId] = count + 1;
        // }
        //
        // // Host만 호출. 매치 시작 시 ResetScore와 함께 호출해 로비 대기 중 쌓인 포획이 섞이지 않게 한다.
        // public void ResetPendingCatches() => _pendingCatches.Clear();
        #endregion


        #region 점수
        public void AddScore(int amount)
        {
            if (!HasInputAuthority) return;
            Rpc_AddScore(amount);
        }

        // Host만 호출. 매치 시작 시 로비 대기 중 쌓인 점수를 정리해 TotalContribution과 기준을 맞춘다.
        public void ResetScore()
        {
            if (!HasStateAuthority) return;
            Score = 0;
            Debug.Log($"<color=#FFA500>[FishingPlayerController] ResetScore: PlayerIndex={PlayerIndex} → Score=0</color>");
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_AddScore(int amount)
        {
            Score += amount;

            var gameManager = FindFirstObjectByType<FishingGameManager>();
            if (gameManager != null)
                gameManager.AddTotalContribution(amount);
        }

        private void RefreshScoreUI()
        {
            var gameManager = FindFirstObjectByType<FishingGameManager>();
            if (gameManager != null)
                gameManager.UpdatePlayerScoreUI(PlayerIndex, Score);
        }
        #endregion
    }
}