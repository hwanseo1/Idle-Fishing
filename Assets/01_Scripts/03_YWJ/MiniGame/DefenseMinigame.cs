using JHS.Fishing;
using RMS.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RMS.Fishing
{
    public class DefenseMinigame : MonoBehaviour, IManualFishing
    {
        [Header("UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _multiButton;
        [SerializeField] private GameObject _biteInteractButton;

        [Header("Root")]
        [SerializeField] private RectTransform _playArea;

        [Header("Trap")]
        [SerializeField] private RectTransform _trap;
        [SerializeField] private RectTransform _trapStartPoint;
        [SerializeField] private RectTransform _trapEndPoint;

        [Header("Harpoon Shooter")]
        [SerializeField] private HarpoonShooterController _harpoonShooter;

        [Header("Harpoon")]
        [SerializeField] private DefenseHarpoon _harpoonPrefab;
        [SerializeField] private RectTransform _harpoonFirePoint;
        [SerializeField] private float _harpoonSpeed = 500f;
        [SerializeField] private float _shootCooldown = 0.25f;

        [Header("Predator")]
        [SerializeField] private DefensePredator _predatorPrefab;
        [SerializeField] private RectTransform[] _leftSpawnPoints;
        [SerializeField] private RectTransform[] _rightSpawnPoints;

        [Header("Difficulty")]
        [SerializeField] private float _baseDefenseTime = 8f;
        [SerializeField] private float _defenseTimeDifficultyScale = 6f;

        [SerializeField] private float _baseSpawnInterval = 1.5f;
        [SerializeField] private float _spawnIntervalDifficultyScale = 0.7f;
        [SerializeField] private float _minSpawnInterval = 0.45f;

        [SerializeField] private float _basePredatorSpeed = 250f;
        [SerializeField] private float _predatorSpeedDifficultyScale = 250f;

        private readonly List<DefensePredator> _activePredators = new();
        private readonly List<DefenseHarpoon> _activeHarpoons = new();

        private bool _isRunning;
        private bool _failed;

        private float _defenseTime;
        private float _spawnInterval;
        private float _predatorSpeed;
        private float _lastShootTime;

        public IEnumerator RunMiniGame(FishData fish, Action<ManualResult> onDone)
        {
            float difficulty = fish != null ? fish.ManualDifficulty : 0.3f;
            yield return RunDefenseCore(difficulty, onDone);
        }

        public IEnumerator RunMiniGame(BossData boss, Action<ManualResult> onDone)
        {
            float difficulty = boss != null ? boss.ManualDifficulty : 0.5f;
            yield return RunDefenseCore(difficulty, onDone);
        }

        private IEnumerator RunDefenseCore(float difficulty, Action<ManualResult> onDone)
        {
            InitDifficulty(difficulty);
            ResetState();
            ShowPanel();

            float elapsed = 0f;
            float spawnTimer = 0f;

            _isRunning = true;
            _failed = false;
            _lastShootTime = -999f;

            while (_isRunning)
            {
                float dt = Time.deltaTime;

                elapsed += dt;
                spawnTimer += dt;

                UpdateTrapPosition(elapsed / _defenseTime);

                if (spawnTimer >= _spawnInterval)
                {
                    spawnTimer = 0f;
                    SpawnPredator();
                }

                HandleInput();
                CheckCollision();
                RemoveDestroyedObjects();

                if (_failed)
                    break;

                if (elapsed >= _defenseTime)
                    break;

                yield return null;
            }

            ManualResult result = _failed ? ManualResult.Fail : ManualResult.Success;

            _isRunning = false;

            yield return new WaitForSeconds(0.4f);

            ResetState();
            HidePanel();

            onDone?.Invoke(result);
        }

        private void InitDifficulty(float difficulty)
        {
            difficulty = Mathf.Clamp01(difficulty);

            _defenseTime =
                _baseDefenseTime +
                difficulty * _defenseTimeDifficultyScale;

            _spawnInterval = Mathf.Max(
                _minSpawnInterval,
                _baseSpawnInterval -
                difficulty * _spawnIntervalDifficultyScale
            );

            _predatorSpeed =
                _basePredatorSpeed +
                difficulty * _predatorSpeedDifficultyScale;
        }

        private void ResetState()
        {
            _isRunning = false;
            _failed = false;

            ClearPredators();
            ClearHarpoons();
            ResetTrap();
        }

        private void ResetTrap()
        {
            if (_trap == null || _trapStartPoint == null)
                return;

            _trap.anchoredPosition = _trapStartPoint.anchoredPosition;
        }

        private void UpdateTrapPosition(float t)
        {
            if (_trap == null || _trapStartPoint == null || _trapEndPoint == null)
                return;

            t = Mathf.Clamp01(t);

            _trap.anchoredPosition = Vector2.Lerp(
                _trapStartPoint.anchoredPosition,
                _trapEndPoint.anchoredPosition,
                t
            );
        }

        private void HandleInput()
        {
            if (Mouse.current == null)
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
                ShootHarpoon();
        }

        private void ShootHarpoon()
        {
            if (Time.time - _lastShootTime < _shootCooldown)
                return;

            if (_harpoonPrefab == null || _playArea == null)
                return;

            if (_harpoonShooter == null)
                return;

            _lastShootTime = Time.time;

            DefenseHarpoon harpoon = Instantiate(
                _harpoonPrefab,
                _playArea
            );

            RectTransform harpoonRect = harpoon.Rect;

            Vector3 fireWorldPos = _harpoonShooter.GetFireWorldPosition();

            Vector2 fireLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _playArea,
                RectTransformUtility.WorldToScreenPoint(null, fireWorldPos),
                null,
                out fireLocalPos
            );

            harpoonRect.anchoredPosition = fireLocalPos;

            Vector2 direction = _harpoonShooter.GetFireDirection();

            harpoon.Init(direction, _harpoonSpeed);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            harpoonRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            _activeHarpoons.Add(harpoon);
        }

        private void SpawnPredator()
        {
            if (_predatorPrefab == null || _trap == null)
                return;

            bool spawnLeft = UnityEngine.Random.value < 0.5f;

            RectTransform spawnPoint = GetRandomSpawnPoint(
                spawnLeft ? _leftSpawnPoints : _rightSpawnPoints
            );

            if (spawnPoint == null)
                return;

            DefensePredator predator = Instantiate(
                _predatorPrefab,
                _playArea
            );

            predator.Rect.anchoredPosition = spawnPoint.anchoredPosition;

            if (spawnLeft)
                predator.Rect.localScale = new Vector3(1f, 1f, 1f);
            else
                predator.Rect.localScale = new Vector3(-1f, 1f, 1f);

            predator.Init(_trap, _predatorSpeed);

            _activePredators.Add(predator);
        }

        private RectTransform GetRandomSpawnPoint(RectTransform[] points)
        {
            if (points == null || points.Length == 0)
                return null;

            return points[UnityEngine.Random.Range(0, points.Length)];
        }

        private void CheckCollision()
        {
            for (int i = _activePredators.Count - 1; i >= 0; i--)
            {
                DefensePredator predator = _activePredators[i];

                if (predator == null || predator.Rect == null)
                    continue;

                if (_trap != null && IsOverlap(predator.Rect, _trap))
                {
                    Fail();
                    return;
                }

                for (int j = _activeHarpoons.Count - 1; j >= 0; j--)
                {
                    DefenseHarpoon harpoon = _activeHarpoons[j];

                    if (harpoon == null || harpoon.Rect == null)
                    {
                        _activeHarpoons.RemoveAt(j);
                        continue;
                    }

                    if (IsOverlap(predator.Rect, harpoon.Rect))
                    {
                        Debug.Log("작살 명중!");

                        predator.Retreat();

                        Destroy(harpoon.gameObject);
                        _activeHarpoons.RemoveAt(j);

                        break;
                    }
                }
            }
        }

        private bool IsOverlap(RectTransform a, RectTransform b)
        {
            if (a == null || b == null)
                return false;

            Rect rectA = GetWorldRect(a);
            Rect rectB = GetWorldRect(b);

            bool overlap = rectA.Overlaps(rectB);

            return overlap;
        }

        private Rect GetWorldRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float minX = corners[0].x;
            float maxX = corners[0].x;
            float minY = corners[0].y;
            float maxY = corners[0].y;

            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private void Fail()
        {
            _failed = true;
            _isRunning = false;
        }

        private void RemoveDestroyedObjects()
        {
            for (int i = _activePredators.Count - 1; i >= 0; i--)
            {
                if (_activePredators[i] == null)
                    _activePredators.RemoveAt(i);
            }

            for (int i = _activeHarpoons.Count - 1; i >= 0; i--)
            {
                if (_activeHarpoons[i] == null)
                    _activeHarpoons.RemoveAt(i);
            }
        }

        private void ClearPredators()
        {
            for (int i = _activePredators.Count - 1; i >= 0; i--)
            {
                if (_activePredators[i] != null)
                    Destroy(_activePredators[i].gameObject);
            }

            _activePredators.Clear();
        }

        private void ClearHarpoons()
        {
            for (int i = _activeHarpoons.Count - 1; i >= 0; i--)
            {
                if (_activeHarpoons[i] != null)
                    Destroy(_activeHarpoons[i].gameObject);
            }

            _activeHarpoons.Clear();
        }

        private void ShowPanel()
        {
            if (_panel != null)
                _panel.SetActive(true);

            if (_multiButton != null)
                _multiButton.SetActive(false);

            if (_biteInteractButton != null)
                _biteInteractButton.SetActive(false);
        }

        private void HidePanel()
        {
            if (_panel != null)
                _panel.SetActive(false);

            if (_multiButton != null)
                _multiButton.SetActive(true);

            if (_biteInteractButton != null)
                _biteInteractButton.SetActive(true);
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            ResetState();
            HidePanel();
        }
    }
}