# GLOSSARY — словарь терминов

Алфавитный список понятий, которые встречаются в этом репозитории
и/или в общении с пользователем (Fermix).

## A

- **AlphaWarhead** — ядерная боеголовка SCP:SL. Запуск и отмена
  через `Warhead.Start()` / `Warhead.Stop()`. В FermixCoin это
  исход I5 — alarm на 30 сек, потом авто-отмена.

## C

- **CASSIE** — встроенный в SCP:SL автоинформатор (голосовая
  система). Зов через `Cassie.Message("...")`. В FermixCoin
  используется в G1 и I2 (но I2 удалён).
- **CommonHint** — UI-helper из заимствованного HSM. **Мы его
  сейчас не используем напрямую**: API `FermixHint` ходит через
  `FermixHintStack`, который рендерит свой `Hint` в pipeline.
  Оставлен в `Hints/UI/` ради бинарной совместимости с upstream.

## D

- **DereCoin** — оригинальный плагин-предок FermixCoin. ~150
  строк, EXILED 8.0, два типа исходов. Мы взяли только идею
  «d100 по конфигу + лимит бросков», остальное — наше.
- **Devin Review** — встроенный AI-reviewer GitHub'а. Оставляет
  inline-комментарии на PR. Часто ловит реальные баги — стоит
  читать.

## E

- **EXILED** — фреймворк плагинов для SCP:SL. У нас зафиксирована
  версия `9.13.3` в `Plugin.cs::RequiredExiledVersion` и
  `FermixCore.MinimumExiledVersion`.

## F

- **FermixCoin** — встроенный модуль FermixAPI (с v2.4.0). Лежит в
  `FermixCoin/` в корне проекта, namespace `FermixAPI.FermixCoin`.
  Реализует «монету фортуны» с ~40 исходами разных категорий.
  Управляется через `CoinManager.Initialize()` / `Shutdown()`.
- **CoinManager** — статический менеджер модуля FermixCoin. Заменил
  отдельный Plugin-класс с v2.4.0. Вызывается из `FermixCore`.
- **FermixGlow** — модуль подсветки предметов через Items API.
  Уровни интенсивности привязаны к редкости (Common 0.55,
  Uncommon 0.85, ..., Legendary 1.40). См. `Systems/FermixGlow.cs`.
- **FermixHint** — публичный API для отправки хинтов. Лежит в
  `Extensions/FermixHint.cs`. Идёт через `FermixHintStack`, не
  через нативный `player.ShowHint`.
- **FermixHintStack** — внутренняя реализация стека хинтов. На
  каждый тик (0.5 с) ремоунтит коллекцию активных хинтов в один
  HsmHint и пушит его в `Hints/Core/Utilities/PlayerDisplay`.
- **FermixScheduler** — планировщик отложенных задач.
  `Delay`/`Repeat`/`Countdown`. Сам очищает таски при reload.

## H

- **Harmony (Lib.Harmony)** — runtime patcher для .NET, позволяет
  «подменять» методы в чужих сборках. У нас используется только
  внутри `Hints/Core/Utilities/Patch/Patcher.cs` для перехвата
  `Player.ShowHint`. Сам EXILED грузит `0Harmony.dll` в рантайме.
- **HintServiceMeow (HSM)** — сторонний hint-движок, MIT, ~500 КБ.
  В FermixAPI 2.3.0+ его исходники **встроены** в каталог
  `Hints/` под нашим namespace `FermixAPI.Hints.*`. Атрибуция —
  в `vendor/HintServiceMeow/`.

## I

- **IDestructible** — внутренний интерфейс HSM, который требует от
  объекта метод `Destruct()` для освобождения ресурсов (например,
  PlayerDisplay при уходе игрока).

## L

- **LabAPI** — официальный API SCP:SL от Northwood. Используется
  параллельно с EXILED (через `LabApi.Features.Wrappers.Player`).
  Версия — 1.1.6.

## M

- **Mega-Jackpot** — самый редкий исход FermixCoin (~0.01%). При
  выпадении срабатывают **все** одобренные пользователем исходы
  разом + global broadcast.
- **MEC (More Effective Coroutines)** — корутиновая библиотека,
  которой пользуется EXILED. У нас обёрнута в `FermixScheduler`.

## P

- **PlayerDisplay** — главный класс HSM (теперь в
  `FermixAPI.Hints.Core.Utilities`). Один экземпляр на каждого
  ReferenceHub. Хранит свою `HintCollection`, отрисовщик
  `ScpslDisplayOutput`, планировщик задач.
- **Pocket Dimension** — карманное измерение SCP-106. Исход C3
  телепортирует туда на 5 сек, потом возвращает.

## R

- **RA (Remote Admin)** — встроенный в SCP:SL админ-интерфейс
  (открывается на `~`). Команды управления сервером: `bc`,
  `door`, `tp`, и т.п. В контексте FermixCoin — пользователь
  присылал скриншот «Door Management» из RA, на основе которого
  собран whitelist комнат для исхода C1.
- **Rarity** — enum в FermixCoin: `Common / Uncommon / Rare /
  Epic / Legendary` (плюс неявная `Mythic` для Mega-Jackpot).
  Влияет на цвет подсветки в `CoinGlowController`.
- **ReferenceHub** — нижнеуровневый объект игрока в SCP:SL.
  EXILED'овский `Player` оборачивает его (`player.ReferenceHub`).
  Hint-движок работает с `ReferenceHub`, а не с `Player`.
- **RoleSpawnFlags.None** — флаг для `Player.Role.Set(role,
  RoleSpawnFlags.None)`, который **не** очищает инвентарь и **не**
  телепортирует игрока. Используется в исходе D2 (туториал на
  60 сек с сохранением инвентаря).

## S

- **SCPStats / RespawnTimer / MapEditorReborn** — сторонние
  плагины, на которые у нас есть «опциональная» интеграция (через
  `FermixCore.Is*Available`). Не критичны.
- **SyncSpeed (HSM)** — частота синхронизации хинта с экраном.
  Возможные значения — `Slowest / Slow / Normal / Fast / Fastest /
  Unlimited`. Мы ставим `Fastest` для быстрых обновлений
  (countdown'ы, прогресс-бары).

## T

- **TextWidth** — ZIP-файл в `Hints/TextWidth`, embedded resource
  с таблицей ширин TextMeshPro-символов. HSM использует его для
  pixel-perfect измерения текста при рендеринге.

## W

- **WaitingForPlayers** — серверное событие EXILED, фаза «между
  раундами», когда все игроки сидят на загрузочном экране. Мы
  применяем Harmony-патчи hint-движка ровно на этом событии,
  чтобы успеть до первого `ShowHint` нового раунда.
- **WeightMultiplier** — поле `Outcome.WeightMultiplier` в
  FermixCoin. Множитель к базовой вероятности выпадения исхода
  (зависящей от его Rarity). Понижение `weightMultiplier` ниже 1.0
  делает редкий исход ещё реже. Например, `B1.weightMultiplier =
  0.15` означает «эта HE-граната выпадает в ~7 раз реже, чем
  обычный Rare-исход».
