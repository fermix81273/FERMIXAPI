# CONVENTIONS — стиль и правила кода

## Язык

| Что | Язык |
| --- | --- |
| Код (классы, методы, поля) | English (стандарт C#) |
| XML-doc summary | RU (краткое описание) |
| Комментарии в реализации | RU, по делу |
| Строки в логах сервера | RU, через `FermixLog.Info/Warn/Error` |
| Хинты на экране игроков | RU, через `FermixHint.*` |
| README.md, .agents/*.md | RU |
| Сообщения в PR, commit'ах | RU, кратко |
| Ответы пользователю в чате | RU, без воды |

## Когда пишешь код

- **Не используй `Any`/`getattr`/`setattr`/динамику.** Если хочется —
  значит, ты не разобрался в типах, иди читай EXILED API.
- **Не правь сгенерированные файлы.** Не трогай `bin/`, `obj/`,
  `*.csproj.user`. Зависимости менять только через `<PackageReference>`
  в csproj.
- **Минимальные правки.** Не делай рефакторинг по дороге. Держи
  изменение сфокусированным на задаче.
- **Не используй emoji в коде, логах и комментариях.** Это просьба
  Devin core-инструкций, и пользователь это уже отметил отдельно.
- **Не пиши `public static`-имена с префиксом `_`.** Приватные
  поля — `_camelCase`. Приватные методы — `PascalCase`.
- **Структура файла:** сначала `using`, потом `namespace`, потом
  `class`, внутри — регионы по теме (`#region Public API`,
  `#region Internal Types`, `#region Update Loop`, ...).

## Namespace'ы

```
FermixAPI                    ← публичные расширения и обёртки (FermixHint и т.п.)
FermixAPI.Core               ← FermixCore, FermixHintStack, FermixScheduler, ...
FermixAPI.Systems            ← FermixGlow, FermixDoors, FermixRoles, ...
FermixAPI.Commands           ← консольные / ремоут-команды
FermixAPI.Integration        ← мосты к LabAPI, MapEditorReborn, SCPStats, ...
FermixAPI.Utils              ← FermixLog, FermixData, FermixConfigUtils
FermixAPI.Hints.*            ← встроенный hint-движок (бывший HintServiceMeow)
FermixAPI.FermixCoin         ← встроенный модуль монетки (CoinManager, OutcomeRegistry, ...)
FermixAPI.FermixCoin.Outcomes ← все исходы броска монетки
```

### Как добавить новый встроенный модуль (плагин)

С v2.4.0 все плагины встроены в `FermixAPI.dll`. Шаблон:

```
FermixAPI.<ИмяМодуля>           ← папка в корне проекта
FermixAPI.<ИмяМодуля>.Core      ← внутренние классы
FermixAPI.<ИмяМодуля>.Outcomes  ← если есть исходы/эффекты
```

Менеджер модуля — `static class <ИмяМодуля>Manager` с методами
`Initialize()` / `Shutdown()`, вызываемыми из `FermixCore`.
Конфиг модуля — свойства в общем `Config.cs` FermixAPI.

## Хинты — публичный API

```csharp
using FermixAPI;

FermixHint.Send(player, "Привет!", duration: 5f);
FermixHint.SendColored(player, "Ошибка", FermixHint.Red, 3f);
FermixHint.Success(player, "+10 HP");
FermixHint.Error(player, "Нет места в инвентаре");
FermixHint.Warning(player, "Скоро взрыв!");
FermixHint.Info(player, "Ты в HCZ");
FermixHint.SendToAll("Раунд начался");
FermixHint.SendToAllColored("ВНИМАНИЕ", FermixHint.Yellow, 3f);
```

`FermixHint` под капотом пишет в `FermixHintStack`, который пакует
все активные хинты и пушит их в встроенный hint-движок (`Hints/`).
**Не** зови `player.ShowHint(...)` напрямую: даже если на сервере
других плагинов с HSM нет, наш Harmony-патч всё равно перехватит
вызов и переадресует — но это лишний overhead, и любой стиль-чек
ругнётся.

## Задержки и асинхронность

```csharp
using FermixAPI.Core;

// Через 3 секунды:
FermixScheduler.Delay(3f, () => FermixHint.Send(player, "BOOM"));

// Каждые 0.5 секунды, 10 раз:
FermixScheduler.Repeat(0.5f, 10, i => FermixHint.Send(player, $"Tick {i}"));

// Отсчёт:
FermixScheduler.Countdown(player, "Взрыв через {0} сек", seconds: 5);
```

Не используй голые `MEC.Timing.CallDelayed` напрямую — `FermixScheduler`
сам отслеживает таски и убирает их при `Shutdown()`, иначе после
reload плагина останутся «висящие» корутины.

## Конфиги

`Plugin.cs` использует стандартный EXILED-конфиг через
`Plugin<TConfig>`. Дополнительные YAML-конфиги пишутся в
`FermixPaths.ConfigDirectory` через `FermixConfigUtils`. Не плоди
своих файлов вне `FermixPaths.*` — пользователь хочет чистую
структуру каталогов EXILED/Configs/Fermix*.

## Граната, телепорт, спавн SCP

См. примеры в `FermixCoin/Outcomes/*.cs`. Главные
принципы:

- Спавн предметов — через `Pickup.CreateAndSpawn(...)` или
  `player.AddItem(...)`. Учитывать, что `RoleSpawnFlags.None`
  при `Role.Set` сохраняет инвентарь.
- Телепорт игрока — через `player.Teleport(position)`. Если позиция
  взята из `Room.Position`, добавить `+ Vector3.up * 1.0f` (комната
  в координатах пола, иначе клипуется). Если позиция уже от
  `player.Position` — НЕ добавлять offset.
- Эффекты (speed, slowness, ...) — через
  `player.EnableEffect<TEffect>(intensity, duration)`.

## Комментарии, которые пишут «что я тут поправил»

**Не пиши их.** Если изменение неочевидно, объясни в PR-описании,
не в коде. Комментарий должен описывать *что* делает код, а не
*зачем* ты его поменял.

```csharp
// Bad — комментарий «диффа»:
// Fix for когда у игрока нет инвентаря
if (player.Items.Count == 0) return;

// Good — комментарий «состояния»:
// Без предметов выдавать дубликат нечего, тихо выходим.
if (player.Items.Count == 0) return;
```

## Тесты

Их нет. Если задача предполагает живой тест на сервере —
**сначала** делай PR, **потом** проси пользователя мерджнуть и
проверить. Не блокируй PR на тестировании, потому что только
пользователь может запустить SCP:SL-сервер.
