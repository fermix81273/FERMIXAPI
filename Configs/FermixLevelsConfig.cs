using System.Collections.Generic;
using System.ComponentModel;
using FermixAPI.Core;

namespace FermixAPI.Configs
{
    /// <summary>
    /// Описание одного уровня для системы <see cref="FermixAPI.Systems.FermixPlayerXp"/>.
    /// Игроку присваивается <b>максимальный</b> уровень, чьё значение
    /// <see cref="MinXp"/> не превышает текущий накопленный опыт.
    /// </summary>
    public class FermixLevel
    {
        [Description("Минимальное количество опыта для получения уровня.")]
        public float MinXp { get; set; }

        [Description("Подпись уровня в CustomInfo (выводится рядом с ником).")]
        public string Text { get; set; } = string.Empty;

        [Description("Цвет подписи уровня (значение из enum CustomInfoColor).")]
        public CustomInfoColor Color { get; set; } = CustomInfoColor.White;
    }

    /// <summary>
    /// Конфигурация уровней игроков. Хранится отдельным YAML файлом
    /// (<c>FermixAPI/levels.yml</c>) — это сделано осознанно: список
    /// уровней может быть длинным и часто редактироваться без риска
    /// сломать основной конфиг плагина. Загружается через
    /// <see cref="Utils.FermixConfigUtils.Load{T}"/>.
    /// </summary>
    public class FermixLevelsConfig
    {
        [Description("Включить кастомные подписи уровней. Если выключено — игрокам ставится «Неизвестно».")]
        public bool Enabled { get; set; } = true;

        [Description("Делитель полученного опыта. По умолчанию 3 — точно как в Hazbin.NoRules.PlayerXp.")]
        public float XpDivisor { get; set; } = 3f;

        [Description("Текст для игроков с DoNotTrack или без записи в БД.")]
        public string UnknownText { get; set; } = "Неизвестно";

        [Description("Цвет «неизвестного» уровня (значение из enum CustomInfoColor).")]
        public CustomInfoColor UnknownColor { get; set; } = CustomInfoColor.Brown;

        [Description("Список уровней. Должен быть отсортирован по возрастанию MinXp; если нет — будет отсортирован при загрузке.")]
        public List<FermixLevel> Levels { get; set; } = new List<FermixLevel>
        {
            new FermixLevel { MinXp = 0f,     Text = "Новичок",      Color = CustomInfoColor.Silver },
            new FermixLevel { MinXp = 250f,   Text = "Солдат",       Color = CustomInfoColor.Cyan },
            new FermixLevel { MinXp = 1000f,  Text = "Ветеран",      Color = CustomInfoColor.LightGreen },
            new FermixLevel { MinXp = 2500f,  Text = "Капитан",      Color = CustomInfoColor.Yellow },
            new FermixLevel { MinXp = 5000f,  Text = "Майор",        Color = CustomInfoColor.Orange },
            new FermixLevel { MinXp = 10000f, Text = "Полковник",    Color = CustomInfoColor.Crimson },
            new FermixLevel { MinXp = 25000f, Text = "Генерал",      Color = CustomInfoColor.Magenta },
            new FermixLevel { MinXp = 50000f, Text = "Легенда",      Color = CustomInfoColor.Pumpkin },
        };

        [Description("Множитель опыта для никнеймов с тегом #FERMIX (case-insensitive).")]
        public float TaggedNicknameMultiplier { get; set; } = 2f;

        [Description("Тег в нике, дающий бонусный множитель (см. TaggedNicknameMultiplier).")]
        public string SpecialTag { get; set; } = "#FERMIX";

        [Description("Сколько секунд нужно прожить, чтобы получить +1 опыт. 0 — отключить пассивный набор.")]
        public float AliveTickSeconds { get; set; } = 60f;

        [Description("Опыт за активацию генератора.")]
        public float XpGenerator { get; set; } = 5f;

        [Description("Опыт за взаимодействие со шкафчиком.")]
        public float XpLocker { get; set; } = 0.5f;

        [Description("Опыт за поднятие SCP-предмета (Категория SCPItem).")]
        public float XpScpItem { get; set; } = 5f;

        [Description("Опыт за поднятие огнестрельного оружия.")]
        public float XpFirearm { get; set; } = 0.5f;

        [Description("Опыт за поднятие ключ-карты.")]
        public float XpKeycard { get; set; } = 0.2f;

        [Description("Опыт за поднятие любого другого предмета.")]
        public float XpDefaultItem { get; set; } = 0.1f;

        [Description("Опыт за использование SCP-предмета (медкит SCP-500 и т.п.).")]
        public float XpScpItemUsed { get; set; } = 10f;

        [Description("Опыт за использование обычного предмета.")]
        public float XpDefaultUsed { get; set; } = 0.5f;

        [Description("Опыт атакующему за убийство игрока-человека.")]
        public float XpKillHuman { get; set; } = 100f;

        [Description("Опыт SCP за убийство человека.")]
        public float XpKillByScp { get; set; } = 70f;

        [Description("Опыт за убийство SCP.")]
        public float XpKillScp { get; set; } = 200f;

        [Description("Опыт игроку за успешный escape с диска.")]
        public float XpEscape { get; set; } = 150f;

        [Description("Опыт тому, кто разоружил сбежавшего.")]
        public float XpEscapeDisarmer { get; set; } = 100f;
    }
}
