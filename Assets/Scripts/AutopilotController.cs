using UnityEngine;
using UnityEngine.InputSystem; // Для Unity 6
using TMPro;

public class AutopilotController : MonoBehaviour
{
    [Header("UI Элементы")]
    [Tooltip("Ссылка на текстовый компонент TextMeshPro, который будет менять цвет и текст")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Настройки текста и цвета")]
    [SerializeField] private string textOn = "ВКЛ •";
    [SerializeField] private string textOff = "ВЫКЛ •";

    [SerializeField] private Color colorOn = Color.green;   // Зеленый цвет для ВКЛ
    [SerializeField] private Color colorOff = Color.red;    // Красный цвет для ВЫКЛ

    // Внутреннее состояние автопилота
    private bool isAutopilotActive = false;

    void Start()
    {
        // При старте игры принудительно задаем начальное состояние (Выкл)
        UpdateAutopilotUI();
    }

    void Update()
    {
        // Проверяем нажатие клавиши TAB через новую Input System в Unity 6
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            // Меняем состояние на противоположное
            isAutopilotActive = !isAutopilotActive;

            // Обновляем визуальное отображение на экране
            UpdateAutopilotUI();

            // Здесь можно вызвать логику включения самого автопилота для ракеты, например:
            // SendAutopilotStateToRocket(isAutopilotActive);
        }
    }

    /// <summary>
    /// Метод для обновления текста и его цвета в интерфейсе
    /// </summary>
    private void UpdateAutopilotUI()
    {
        if (statusText == null) return;

        if (isAutopilotActive)
        {
            statusText.text = textOn;
            statusText.color = colorOn;
        }
        else
        {
            statusText.text = textOff;
            statusText.color = colorOff;
        }
    }

    /// <summary>
    /// Публичный метод, если другие скрипты (например, управление ракетой) захотят узнать, включен ли автопилот
    /// </summary>
    public bool IsAutopilotEnabled()
    {
        return isAutopilotActive;
    }
}
