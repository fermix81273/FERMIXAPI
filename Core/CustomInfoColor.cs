namespace FermixAPI.Core
{
    /// <summary>
    /// Цвет для CustomInfo / тег-зон. Заимствовано из Hazbin.Core
    /// (Hazbin/Core/Enums/CustomInfoColor.cs) — добавлено как утилита,
    /// чтобы система <see cref="FermixAPI.Systems.FermixPlayerXp"/> могла
    /// настраивать цвета подписей уровней общим перечислением, а не сырыми
    /// hex-строками. Значения совпадают с цветовой палитрой SCP:SL для
    /// CustomInfo (см. внутриигровой <c>NicknameSync</c>).
    /// </summary>
    public enum CustomInfoColor
    {
        None = -1,
        Pink = 0,
        Red = 1,
        Brown = 2,
        Silver = 3,
        LightGreen = 4,
        Crimson = 5,
        Cyan = 6,
        Aqua = 7,
        DeepPink = 8,
        Tomato = 9,
        Yellow = 10,
        Magenta = 11,
        BlueGreen = 12,
        Orange = 13,
        Lime = 14,
        Green = 15,
        Emerald = 16,
        Carmine = 17,
        Nickel = 18,
        Mint = 19,
        ArmyGreen = 20,
        Pumpkin = 21,
        Black = 22,
        White = 23,
    }

    /// <summary>
    /// Расширения для <see cref="CustomInfoColor"/>: возвращает hex без
    /// решётки (готов к подстановке в <c>&lt;color=#...&gt;</c>).
    /// Значения берутся из <c>NicknameSync.ValidateCustomInfo</c> — там идёт
    /// case-sensitive Contains-проверка по массиву <c>Misc.AcceptedColours</c>,
    /// поэтому регистр и точные hex-коды должны совпадать с игровым списком.
    /// </summary>
    public static class CustomInfoColorExtensions
    {
        public static string GetHexColor(this CustomInfoColor color) => color switch
        {
            CustomInfoColor.Pink => "FF96DE",
            CustomInfoColor.Red => "C50000",
            CustomInfoColor.Brown => "944710",
            CustomInfoColor.Silver => "A0A0A0",
            CustomInfoColor.LightGreen => "32CD32",
            CustomInfoColor.Crimson => "DC143C",
            CustomInfoColor.Cyan => "00B7EB",
            CustomInfoColor.Aqua => "00FFFF",
            CustomInfoColor.DeepPink => "FF1493",
            CustomInfoColor.Tomato => "FF6448",
            CustomInfoColor.Yellow => "FAFF86",
            CustomInfoColor.Magenta => "FF0090",
            CustomInfoColor.BlueGreen => "4DFFB8",
            CustomInfoColor.Orange => "FF9966",
            CustomInfoColor.Lime => "BFFF00",
            CustomInfoColor.Green => "228B22",
            CustomInfoColor.Emerald => "50C878",
            CustomInfoColor.Carmine => "960018",
            CustomInfoColor.Nickel => "727472",
            CustomInfoColor.Mint => "98FB98",
            CustomInfoColor.ArmyGreen => "4B5320",
            CustomInfoColor.Pumpkin => "EE7600",
            CustomInfoColor.Black => "000000",
            CustomInfoColor.White => "FFFFFF",
            _ => "FFFFFF",
        };
    }
}
