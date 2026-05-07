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
    /// </summary>
    public static class CustomInfoColorExtensions
    {
        public static string GetHexColor(this CustomInfoColor color) => color switch
        {
            CustomInfoColor.Pink => "ff96de",
            CustomInfoColor.Red => "c50000",
            CustomInfoColor.Brown => "944710",
            CustomInfoColor.Silver => "a0a0a0",
            CustomInfoColor.LightGreen => "32cd32",
            CustomInfoColor.Crimson => "dc143c",
            CustomInfoColor.Cyan => "00b7eb",
            CustomInfoColor.Aqua => "00ffff",
            CustomInfoColor.DeepPink => "ff1493",
            CustomInfoColor.Tomato => "ff6347",
            CustomInfoColor.Yellow => "fae00c",
            CustomInfoColor.Magenta => "ff0090",
            CustomInfoColor.BlueGreen => "4ddcb6",
            CustomInfoColor.Orange => "ff9966",
            CustomInfoColor.Lime => "bfff00",
            CustomInfoColor.Green => "228b22",
            CustomInfoColor.Emerald => "50c878",
            CustomInfoColor.Carmine => "960018",
            CustomInfoColor.Nickel => "727472",
            CustomInfoColor.Mint => "98fb98",
            CustomInfoColor.ArmyGreen => "4b5320",
            CustomInfoColor.Pumpkin => "ff7518",
            CustomInfoColor.Black => "000000",
            CustomInfoColor.White => "ffffff",
            _ => "ffffff",
        };
    }
}
