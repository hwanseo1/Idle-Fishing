using UnityEngine;
using UnityEngine.UI;

public class MiniGameFishController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform fishRect;
    [SerializeField] private RectTransform mouthPoint;
    [SerializeField] private RectTransform rodPoint;
    [SerializeField] private Image targetGauge;

    [Header("Move")]
    [SerializeField] private bool useSmoothMove = false;
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Wiggle")]
    [SerializeField] private float xMoveAmount = 40f;
    [SerializeField] private float xMoveSpeed = 4f;
    [SerializeField] private float rotationAmount = 8f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Struggle")]
    [SerializeField] private bool isStruggling = false;
    [SerializeField] private float struggleMultiplier = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swimClip;
    [SerializeField] private float swimSoundInterval = 1.5f;
    [SerializeField] private Vector2 swimPitchRange = new Vector2(0.9f, 1.1f);

    private float swimSoundTimer;

    private Vector2 startFishPos;

    private void Awake()
    {
        if (fishRect == null)
            fishRect = GetComponent<RectTransform>();

        startFishPos = fishRect.anchoredPosition;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        swimSoundTimer = Random.Range(0f, swimSoundInterval);
    }

    private void Update()
    {
        MoveByFillAmount();
        UpdateSwimSound();
    }

    private void MoveByFillAmount()
    {
        if (fishRect == null || mouthPoint == null || rodPoint == null || targetGauge == null)
            return;

        float t = Mathf.Clamp01(targetGauge.fillAmount);

        RectTransform parent = fishRect.parent as RectTransform;

        Vector2 rodLocalPos = WorldToLocal(parent, rodPoint.position);
        Vector2 mouthLocalPos = WorldToLocal(parent, mouthPoint.position);
        Vector2 fishLocalPos = fishRect.anchoredPosition;

        Vector2 mouthOffsetFromFish = mouthLocalPos - fishLocalPos;
        Vector2 targetFishPos = rodLocalPos - mouthOffsetFromFish;

        Vector2 basePos = Vector2.Lerp(startFishPos, targetFishPos, t);

        float multiplier = isStruggling ? struggleMultiplier : 1f;

        float xOffset = Mathf.Sin(Time.time * xMoveSpeed)
                        * xMoveAmount
                        * multiplier;

        float zRot = Mathf.Sin(Time.time * rotationSpeed)
                     * rotationAmount
                     * multiplier;

        Vector2 finalPos = basePos;
        finalPos.x += xOffset;

        if (useSmoothMove)
        {
            fishRect.anchoredPosition = Vector2.Lerp(
                fishRect.anchoredPosition,
                finalPos,
                Time.deltaTime * smoothSpeed
            );
        }
        else
        {
            fishRect.anchoredPosition = finalPos;
        }

        fishRect.localRotation = Quaternion.Euler(0f, 0f, zRot - 90f);
    }

    public void SetStruggling(bool value)
    {
        isStruggling = value;
    }

    public void ResetPosition()
    {
        fishRect.anchoredPosition = startFishPos;
        fishRect.localRotation = Quaternion.identity;
        isStruggling = false;
    }

    public void SaveStartPosition()
    {
        startFishPos = fishRect.anchoredPosition;
    }

    private Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out Vector2 localPos
        );

        return localPos;
    }

    private void UpdateSwimSound()
    {
        if (audioSource == null || swimClip == null)
            return;

        swimSoundTimer -= Time.deltaTime;

        if (swimSoundTimer > 0f)
            return;

        audioSource.pitch = Random.Range(swimPitchRange.x, swimPitchRange.y);
        audioSource.PlayOneShot(swimClip);

        swimSoundTimer = swimSoundInterval;
    }
}