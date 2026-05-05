---
name: add-fermixcoin-outcome
description: Шаги для добавления нового исхода (Outcome) в плагин FermixCoin.
---

# SKILL — Добавить новый Outcome в FermixCoin

## Когда применять

Пользователь просит добавить новый эффект, который должен
срабатывать при броске монетки. Например: «сделай исход — игрок
становится зомби на 30 сек», «добавь, чтобы у игрока в инвентаре
появилась дубль каждой вещи», и т.п.

## Шаги

### 1. Определи категорию

Все исходы в `FermixCoin/Outcomes/` разнесены по тематикам:

| Файл | Категории |
| --- | --- |
| `ItemOutcomes.cs` | A1-A4 — предметы, эффекты, HP, SCP-айтемы |
| `GrenadeOutcomes.cs` | B1-B6 — гранаты, SCP-018, теслы, дисраптор |
| `TeleportOutcomes.cs` | C1-C5 — перемещение, Pocket Dimension |
| `RoleOutcomes.cs` | D1-D6 — смена класса, зомбификация |
| `HealthOutcomes.cs` | E1-E7 — HP, инвентарь, экипировка |
| `LocalOutcomes.cs` | F1-F5 — blackout, двери |
| `RoundOutcomes.cs` | G1-G3 — на других игроков / раунд |
| `RareOutcomes.cs` | I1, I3-I5 — risk/reward редкие |

Подбери файл, который тематически подходит. Если ни один не
подходит — создай новый, например
`FermixCoin/Outcomes/EnvironmentOutcomes.cs`,
по образцу существующих. Namespace: `FermixAPI.FermixCoin.Outcomes`.

### 2. Прикинь Rarity и weightMultiplier

```
Rarity         базовый вес  применяется при
Common         100          обычные не-имбовые штуки
Uncommon        50          приколы, мелкая выгода
Rare            20          средне-имбовые (граната, шотган)
Epic             7          сильные исходы (полный медкит)
Legendary        2          вау-моменты (jackpot)
```

`weightMultiplier` — дополнительный множитель. Используй его, чтобы
точечно занерфить или забустить отдельный исход внутри одной
редкости. Например, B1 = Rare, но `weightMultiplier = 0.15`, потому
что HE-граната под ноги — слишком ломательно для рандома.

### 3. Напиши Outcome

Каждый исход — это `new Outcome(...)`, добавляемый в `List<Outcome> sink`.
Шаблон:

```csharp
sink.Add(new Outcome(
    id: "B7",
    name: "Краткое название на русском",
    rarity: Rarity.Uncommon,
    message: "Главный хинт игроку (большой, цветной).",
    comment: "Маленький подзаголовок снизу — прикольный коммент.",
    weightMultiplier: 1.0f,                // 0..2 типично, 1 = по умолчанию
    action: p =>
    {
        // Логика. p — Exiled.API.Features.Player.
        // Не зови player.ShowHint напрямую — message и comment
        // покажутся автоматически через CoinHandler.
        FermixScheduler.Delay(2f, () =>
        {
            // что-то отложенное
        });
    }
));
```

Ключевые моменты:

- **id** — короткий уникальный код (буква категории + номер).
  Не переиспользуй уже занятые. Текущие занятые: A1-A4, B1-B6,
  C1-C5, D1-D6, E1-E7, F1-F5 (без F4), G2, G3, I1, I3, I4, I5.
- **name** — на русском, кратко (для лога / админ-команд если
  будут).
- **message** — основной хинт игроку. Может содержать
  `<color=#FF0000>...</color>` теги, размеры, b/i/u.
- **comment** — подкомментарий, обычно курсивом, на 2-3 секунды
  меньше чем message.
- **action** — `Action<Player>`, что произойдёт. Используй
  `FermixScheduler.Delay`, не голый `Timing.CallDelayed`.

### 4. Подсветка (если нужно)

Если хочешь, чтобы монетка перед броском «намекала» на исход —
не нужно ничего делать вручную. `CoinGlowController` сам ставит
цвет подсветки = цвет редкости *следующего* исхода, который сейчас
стоит в state'е игрока. Это easter egg, не описан в README.

### 5. Тесты

```bash
cd /home/ubuntu/FERMIXAPI
dotnet build FermixAPI.csproj -c Release
```

Должно быть 0 errors. После этого commit, push в `dev`, открой PR.

### 6. Финал — попроси пользователя проверить

Пользователь сам залайвстримит / зайдёт на сервер и подкинет
монетку. Если выпало — присылает скриншот, ты помогаешь подкрутить
шанс / текст.

## Чек-лист

- [ ] id уникален и понятен
- [ ] Rarity + weightMultiplier обоснованы (соразмерно эффекту)
- [ ] message и comment на русском, короткие, прикольные
- [ ] Никакого `player.ShowHint(...)` напрямую
- [ ] Все задержки через `FermixScheduler.Delay/Repeat`
- [ ] `dotnet build FermixAPI.csproj -c Release` 0 errors
- [ ] Если эффект массовый/dangerous — добавил в `KNOWN_ISSUES.md`
      запись о возможных взаимодействиях
