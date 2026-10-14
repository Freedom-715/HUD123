using UnityEngine;
using UnityEngine.InputSystem; // Для Unity 6
using TMPro;
using UnityEngine.UI;

public class VTVLOrbitalSimulator : MonoBehaviour
{
    [Header("UI Текстовые элементы (TMP)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI heightText;
    [SerializeField] private TextMeshProUGUI variometerText;
    [SerializeField] private TextMeshProUGUI gForceText;
    [SerializeField] private TextMeshProUGUI fuelPercentageText;
    [SerializeField] private Image fuelBarImage;

    [Header("UI Тексты Полетных Фаз (Опционально)")]
    [SerializeField] private TextMeshProUGUI currentPhaseText;

    // --- ДИРЕКТИВНЫЕ ТТХ ИЗ РЕГЛАМЕНТА (ТАБЛИЦА 2) ---
    private const float M_DRY = 15000f;          // Сухая масса корабля (кг)
    private const float M_PROP_MAX = 165000f;    // Масса заправляемого топлива (кг)
    private const float T_MAX = 2700000f;        // Максимальная суммарная тяга (2.7 МН)
    private const float S_MID = 15.90f;          // Площадь миделевого сечения (м²)
    private const float ISP_VAC = 385.0f;        // Удельный импульс в вакууме (с)
    private const float ISP_SEA = 330.0f;        // Удельный импульс на уровне моря (с)
    private const float G0 = 9.81f;              // Стандартное ускорение свободного падения

    // Ограничители интерфейса
    private const float MAX_SPEED = 20000f;      // Предел по ТЗ: 20 000 м/с
    private const float MAX_HEIGHT = 200000f;    // Предел по ТЗ: 200 000 м
    private const float MAX_VARIOMETER = 1000f;  // Предел по ТЗ: 1000 м/с
    private const float R_EARTH = 6371000f;      // Радиус Земли для гравитационной модели

    // Переменные состояния системы
    private float currentFuel;
    private float throttle = 0f;                 // Дросселирование от 0.0 до 1.0
    private Vector2 position;                    // X - Дальность (Cross-Range), Y - Высота
    private Vector2 velocity;                    // Вектор скорости (м/с)
    private float pitchAngle = 90f;              // Угол тангажа (90 = вертикальный старт)

    // Перечисление 6 базовых фаз полета согласно Регламенту
    public enum FlightPhase
    {
        Ascent_1,        // 1. Активный участок выведения
        Orbital_2,       // 2. Орбитальный полет
        DeorbitBurn_3,   // 3. Импульс схода с орбиты
        BellyFlop_4,     // 4. Гиперзвуковой аэродинамический спуск
        CrossRange_5,    // 5. Аэродинамический боковой маневр
        Hoverslam_6      // 6. Терминальный маневр и посадка VTVL
    }
    private FlightPhase currentPhase = FlightPhase.Ascent_1;

    void Start()
    {
        currentFuel = M_PROP_MAX; // Полная заправка корабля при старте
        position = new Vector2(0f, 0f);
        velocity = Vector2.zero;
        currentPhase = FlightPhase.Ascent_1;
    }

    void Update()
    {
        float currentMass = M_DRY + currentFuel;

        // 1. СЧИТЫВАНИЕ ВВОДА УПРАВЛЕНИЯ (W/S - Дроссель, A/D - Тангаж)
        if (Keyboard.current != null)
        {
            // Управление дросселем двигателя (W/S)
            if (Keyboard.current.wKey.isPressed)
                throttle = Mathf.MoveTowards(throttle, 1f, Time.deltaTime * 0.5f);
            if (Keyboard.current.sKey.isPressed)
                throttle = Mathf.MoveTowards(throttle, 0f, Time.deltaTime * 0.7f);

            // Ограничение глубокого дросселирования посадочных ДУ (30% - 100%) согласно Таблице 2
            if (throttle > 0.01f && currentPhase == FlightPhase.Hoverslam_6)
                throttle = Mathf.Clamp(throttle, 0.30f, 1f);

            // Управление креном и тангажом (A/D)
            if (Keyboard.current.aKey.isPressed) pitchAngle += 15f * Time.deltaTime; // Поворот влево
            if (Keyboard.current.dKey.isPressed) pitchAngle -= 15f * Time.deltaTime; // Поворот вправо

            // --- ДОБАВЛЯЕМ ОГРАНИЧЕНИЕ УГЛОВ ТАНГАЖА ТУТ ---
            // Запрещаем ракете наклоняться ниже горизонта (0 градусов справа и 180 градусов слева)
            pitchAngle = Mathf.Clamp(pitchAngle, 0f, 180f);
        }

        // Расчет мгновенного удельного импульса (Isp) в зависимости от высоты (Sea-level -> Vacuum)
        float altitudePercent = Mathf.Clamp01(position.y / 50000f); // Переход в вакуум до 50 км
        float currentIsp = Mathf.Lerp(ISP_SEA, ISP_VAC, altitudePercent);

        // Секундный расход топлива: md = T / (Isp * g0)
        float maxFuelBurnRate = T_MAX / (currentIsp * G0);
        float currentBurn = maxFuelBurnRate * throttle;

        if (currentFuel > 0 && throttle > 0f)
        {
            currentFuel -= currentBurn * Time.deltaTime;
        }
        else
        {
            throttle = 0f;
        }
        currentFuel = Mathf.Clamp(currentFuel, 0f, M_PROP_MAX);

        // Вектор направления тяги ДУ
        Vector2 thrustDirection = new Vector2(Mathf.Cos(pitchAngle * Mathf.Deg2Rad), Mathf.Sin(pitchAngle * Mathf.Deg2Rad));
        Vector2 thrustForce = thrustDirection * (T_MAX * throttle);

        // 2. АЭРОДИНАМИЧЕСКАЯ И АТМОСФЕРНАЯ МОДЕЛЬ
        float height = position.y;
        float speedMagnitude = velocity.magnitude;

        // Плотность атмосферы по барометрическому закону (экспоненциальный спад)
        float airDensity = 1.225f * Mathf.Exp(-height / 8500f);
        if (height >= 100000f) airDensity = 0f; // Линия Кармана (Вакуум)

        // Автоматическое определение текущей фазы полета по критериям регламента
        DetermineFlightPhase(height, speedMagnitude);

        // Расчет Аэродинамических Коэффициентов (Таблица 3)
        float Cd = 0.3f; // Базовое лобовое сопротивление (вертикальный полет)
        float Cl = 0.0f; // Подъемная сила

        if (currentPhase == FlightPhase.BellyFlop_4 || currentPhase == FlightPhase.CrossRange_5)
        {
            // Режим Belly-Flop: Угол атаки ~60 градусов. Резкое торможение днищем корабля
            Cd = 1.85f;  // Огромный мидель и лобовое сопротивление теплозащиты (TPS)
            Cl = 0.85f;  // Регламентный коэффициент L/D >= 0.85 для бокового маневра
        }

        // Сила лобового сопротивления (Drag) - строго против вектора скорости
        Vector2 dragForce = Vector2.zero;
        if (speedMagnitude > 0.1f)
        {
            float dragMagnitude = 0.5f * airDensity * (speedMagnitude * speedMagnitude) * Cd * S_MID;
            dragForce = -velocity.normalized * dragMagnitude;
        }

        // Подъемная сила (Lift) - перпендикулярна вектору скорости для планирования в фазе 5
        Vector2 liftForce = Vector2.zero;
        if (speedMagnitude > 0.1f && Cl > 0f)
        {
            float liftMagnitude = 0.5f * airDensity * (speedMagnitude * speedMagnitude) * Cl * S_MID;
            Vector2 liftDirection = new Vector2(-velocity.y, velocity.x).normalized; // Нормаль к траектории
            liftForce = liftDirection * liftMagnitude;
        }

        // 3. РАСЧЕТ КОРРЕКТНОЙ ПЕРЕГРУЗКИ ПО ФОРМУЛЕ РЕГЛАМЕНТА
        // n = |Тяга двигателя + Сила сопротивления воздуха + Подъемная сила| / (Масса ракеты * 9.81)
        Vector2 totalAerodynamicForce = thrustForce + dragForce + liftForce;
        float gForce = totalAerodynamicForce.magnitude / (currentMass * G0);

        // 4. ГРАВИТАЦИОННАЯ МОДЕЛЬ И ИНТЕГРАЦИЯ ДВИЖЕНИЯ
        // Закон всемирного тяготения (учитывает уменьшение веса с набором высоты)
        float localGravity = G0 * Mathf.Pow(R_EARTH / (R_EARTH + height), 2);
        Vector2 gravityForce = new Vector2(0f, -localGravity * currentMass);

        // Ускорение ракеты (F_total = F_engine + F_drag + F_lift + F_gravity)
        Vector2 totalForce = totalAerodynamicForce + gravityForce;
        Vector2 acceleration = totalForce / currentMass;

        // Расчет шага физики
        velocity += acceleration * Time.deltaTime;
        position += velocity * Time.deltaTime;

        // Фиксация на поверхности Земли
        if (position.y < 0f)
        {
            position.y = 0f;
            velocity = Vector2.zero;
        }

        // 5. ВЫВОД ДАННЫХ НА HUD С УСТАНОВЛЕННЫМИ ОГРАНИЧИТЕЛЯМИ

        // Перевод скорости в м/с (Лимит по ТЗ: 20 000 м/с)
        float hudSpeed = Mathf.Clamp(speedMagnitude, 0f, MAX_SPEED);
        if (speedText != null) speedText.text = $"{Mathf.RoundToInt(hudSpeed)} м/с";

        // Высота (Лимит по ТЗ: 200 000 м)
        float hudHeight = Mathf.Clamp(position.y, 0f, MAX_HEIGHT);
        if (heightText != null) heightText.text = $"{Mathf.RoundToInt(hudHeight)} м";

        // Вариометр (Вертикальная скорость Vy, Лимит по ТЗ: 1000 м/с)
        float hudVariometer = Mathf.Clamp(velocity.y, -MAX_VARIOMETER, MAX_VARIOMETER);
        if (variometerText != null) variometerText.text = $"{Mathf.RoundToInt(hudVariometer)} м/с";

        // Топливо (Убывающая полоска Filled и значение в %)
        float fuelRatio = currentFuel / M_PROP_MAX;
        if (fuelPercentageText != null) fuelPercentageText.text = $"{Mathf.RoundToInt(fuelRatio * 100f)} %";
        if (fuelBarImage != null) fuelBarImage.fillAmount = fuelRatio;

        // Перегрузка с цветовым индикатором риска (> 7G переходит на красный)
        if (gForceText != null)
        {
            gForceText.text = $"{gForce:F1} G";
            gForceText.color = (gForce >= 7.0f) ? Color.red : Color.white;
        }

        // Вывод текущей фазы полета на HUD (для отладки)
        if (currentPhaseText != null) currentPhaseText.text = GetPhaseNameRU(currentPhase);
    }

    /// <summary>
    /// Автоматический расчет фаз согласно полетной программе регламента
    /// </summary>
    private void DetermineFlightPhase(float h, float v)
    {
        if (h >= 200000f && v >= 7700f && currentPhase == FlightPhase.Ascent_1)
        {
            currentPhase = FlightPhase.Orbital_2; // Стабилизация на НОО
        }
        else if (currentPhase == FlightPhase.Orbital_2 && throttle > 0.1f && Vector2.Dot(velocity, transform.up) < 0)
        {
            currentPhase = FlightPhase.DeorbitBurn_3; // Выдача тормозного импульса схода
        }
        else if (h < 100000f && h >= 40000f && velocity.y < -10f && currentPhase != FlightPhase.Ascent_1)
        {
            currentPhase = FlightPhase.BellyFlop_4; // Вход в плотные слои под углом 60 градусов
        }
        else if (h < 40000f && h >= 800f && Mathf.Abs(position.x) > 100f)
        {
            currentPhase = FlightPhase.CrossRange_5; // Боковой аэродинамический маневр коррекции суточного вращения
        }
        else if (h < 800f && h > 0f)
        {
            currentPhase = FlightPhase.Hoverslam_6; // Терминальный разворот Flip и реактивная посадка
        }
    }
    private string GetPhaseNameRU(FlightPhase phase)
    {
        switch (phase)
        {
            case FlightPhase.Ascent_1: return "1. ВЫВЕДЕНИЕ НА НОО";
            case FlightPhase.Orbital_2: return "2. ОРБИТАЛЬНЫЙ ПОЛЁТ";
            case FlightPhase.DeorbitBurn_3: return "3. ИМПУЛЬС СХОДА";
            case FlightPhase.BellyFlop_4: return "4. BELLY-FLOP СПУСК";
            case FlightPhase.CrossRange_5: return "5. CROSS-RANGE МАНЕВР";
            case FlightPhase.Hoverslam_6: return "6. HOVERSLAM ПОСАДКА";
            default: return "АВТОНОМНЫЙ РЕЖИМ";
        }
    }
}
