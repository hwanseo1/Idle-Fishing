using System.Collections;
using UnityEngine;

public class CrewMotionController : MonoBehaviour
{
    [Header("Fishing Rod")]
    [SerializeField] private GameObject _fishingRod;

    [Header("Animator")]
    [SerializeField] private Animator _animator;

    [Header("Duration")]
    [SerializeField] private Vector2 _idleTimeRange = new Vector2(1.5f, 3f);
    [SerializeField] private Vector2 _walkingTimeRange = new Vector2(2f, 3f);

    [Header("Fishing Motion Accuracy")]
    [SerializeField] private float _fishingMotionAccuracy = 0.7f;

    [Header("Fishing Rod Motion")]
    [SerializeField] private float _rodLiftDuration = 3f;
    [SerializeField] private float _rodReturnDuration = 0.5f;
    [SerializeField] private float _rodLiftAngle = -30f;


    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int FishingHash = Animator.StringToHash("Fishing");


    private Coroutine _rodMotionRoutine;
    private Coroutine _motionRoutine;
    private Transform _transform;


    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_transform == null)
            _transform = GetComponentInParent<Transform>();
    }

    private void OnEnable()
    {
        _motionRoutine = StartCoroutine(MotionRoutine());
    }

    private void OnDisable()
    {
        if (_motionRoutine != null)
            StopCoroutine(_motionRoutine);
    }

    private IEnumerator MotionRoutine()
    {
        while (true)
        {
            // Idle
            SetIdle();
            yield return new WaitForSeconds(Random.Range(_idleTimeRange.x, _idleTimeRange.y));

            // Walking 또는 Fishing 랜덤
            //bool walking = Random.value < _fishingMotionAccuracy;
            bool walking = false;

            if (walking)
            {
                SetWalking();

                yield return new WaitForSeconds(Random.Range(_walkingTimeRange.x, _walkingTimeRange.y));
            }
            else
            {
                if (_fishingRod != null)
                {
                    _fishingRod.SetActive(true);

                    if (_rodMotionRoutine != null)
                        StopCoroutine(_rodMotionRoutine);

                    _rodMotionRoutine = StartCoroutine(FishingRodMotionRoutine());
                }

                SetFishing();

                yield return new WaitForSeconds(8.8f);

                if (_rodMotionRoutine != null)
                {
                    StopCoroutine(_rodMotionRoutine);
                    _rodMotionRoutine = null;
                }

                if (_fishingRod != null)
                {
                    _fishingRod.transform.localRotation = Quaternion.Euler(0, 135, 0);
                    _fishingRod.SetActive(false);
                }

            }
        }
    }

    private void SetIdle()
    {
        _animator.SetBool(WalkingHash, false);
        _animator.SetBool(FishingHash, false);
    }

    private void SetWalking()
    {
        _animator.SetBool(WalkingHash, true);
        _animator.SetBool(FishingHash, false);
    }

    private void SetFishing()
    {
        _animator.SetBool(WalkingHash, false);
        _animator.SetBool(FishingHash, true);
    }

    private IEnumerator FishingRodMotionRoutine()
    {
        Transform rod = _fishingRod.transform;

        Quaternion defaultRot = Quaternion.Euler(0f, 135f, 0f);
        Quaternion liftRot = Quaternion.Euler(_rodLiftAngle, 135f, 0f);

        // 시작 자세
        rod.localRotation = defaultRot;

        // 천천히 낚싯대 들어올리기
        float time = 0f;

        while (time < _rodLiftDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, time / _rodLiftDuration);

            rod.localRotation = Quaternion.Lerp(defaultRot, liftRot, t);

            yield return null;
        }

        rod.localRotation = liftRot;

        // 잠깐 유지
        yield return new WaitForSeconds(0.2f);

        // 다시 원래 자세
        time = 0f;

        while (time < _rodReturnDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, time / _rodReturnDuration);

            rod.localRotation = Quaternion.Lerp(liftRot, defaultRot, t);

            yield return null;
        }

        rod.localRotation = defaultRot;
    }
}