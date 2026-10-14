using UnityEngine;
using UnityEngine.InputSystem; // Для Unity 6

public class FlightHorizonController : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Подвижный фон неба и земли (крутится по Z и двигается по Y)")]
    [SerializeField] private RectTransform skyEarthBackground;

    [Tooltip("Подвижная горизонтальная черточка-указатель справа от прибора")]
    [SerializeField] private RectTransform pitchIndicatorLine;

    [Header("Настройки чувствительности")]
    [SerializeField] private float pitchSpeed = 30f;
    [SerializeField] private float rollSpeed = 50f;

    [Header("Множители и Ограничения")]
    [Tooltip("Сколько пикселей смещения UI приходится на 1 градус тангажа")]
    [SerializeField] private float pixelsPerDegree = 5f;

    [Tooltip("Максимальный угол тангажа (вверх/вниз) в градусах")]
    [SerializeField] private float maxPitchAngle = 30f;

    private float currentPitch = 0f;
    private float currentRoll = 0f;

    void Update()
    {
        float pitchInput = 0f;
        float rollInput = 0f;

        // 1. Считывание ввода клавиатуры (Unity 6 / Input System)
        if (Keyboard.current != null)
        {
            // W/S - Тангаж
            if (Keyboard.current.wKey.isPressed) pitchInput = 1f;
            if (Keyboard.current.sKey.isPressed) pitchInput = -1f;

            // ИНВЕРСИЯ КРЕНА: A - летит вправо, D - летит влево
            if (Keyboard.current.aKey.isPressed) rollInput = -1f; // Было 1
            if (Keyboard.current.dKey.isPressed) rollInput = 1f;  // Было -1
        }

        // 2. Расчет тангажа и крена с ограничениями
        currentPitch += pitchInput * pitchSpeed * Time.deltaTime;
        currentPitch = Mathf.Clamp(currentPitch, -maxPitchAngle, maxPitchAngle);

        currentRoll += rollInput * rollSpeed * Time.deltaTime;

        // 3. Обновление Искусственного Горизонта (Небо/Земля)
        if (skyEarthBackground != null)
        {
            skyEarthBackground.localRotation = Quaternion.Euler(0f, 0f, currentRoll);

            // Если при наборе высоты (W) фон едет не в ту сторону, 
            // замените "-currentPitch" на "currentPitch"
            float targetYOffset = -currentPitch * pixelsPerDegree;
            skyEarthBackground.localPosition = new Vector3(0f, targetYOffset, 0f);
        }

        // 4. Обновление Движения ЧЕРТОЧКИ справа
        if (pitchIndicatorLine != null)
        {
            // Черточка двигается вверх/вниз в зависимости от тангажа.
            // Если она должна идти ВВЕРХ при наборе высоты (W), оставляем знак ПЛЮС.
            // Если она должна идти ВНИЗ, поставьте минус перед currentPitch.
            float scaleYOffset = currentPitch * pixelsPerDegree;
            pitchIndicatorLine.localPosition = new Vector3(pitchIndicatorLine.localPosition.x, scaleYOffset, 0f);
        }
    }
}
