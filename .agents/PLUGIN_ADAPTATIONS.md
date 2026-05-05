# Адаптация сторонних плагинов в FermixAPI

Этот файл — **roadmap** для постепенной интеграции функционала из публичных
SCP:SL плагинов в `FermixAPI.dll`. Каждый пункт — отдельный PR (`dev → main`).
Список добавлен пользователем (Fermix); не удаляй пункты без явной просьбы.

> **Не копируй чужой код 1:1.** Цель — переписать поведение под архитектуру
> FermixAPI: модульный System / Manager / Outcome, наши Hint'ы, наши
> подсветки, наши Server-Specific Settings (SSS), наши конфиги.

---

## Общие правила адаптации

Применяй ко **всем** пунктам ниже:

1. **Совместимость:** EXILED 9.13.3 + LabAPI 1.1.6 + .NET Framework 4.8.
   Если плагин-источник старше — используй современные API из `vendor/`
   (или просто из `refs/`). Не вытаскивай устаревшие методы.
2. **Хинты вместо broadcast'ов.** Все `Map.Broadcast`, `player.Broadcast`,
   `Hint.Show` и т.п. **обязательно** заменяются на `FermixHint.Send` /
   `FermixHint.SendColored` / `FermixHint.SendToAll` (см. `Extensions/FermixHint.cs`).
3. **Подсветка кастомных предметов** — через `FermixGlow.AttachToPickup`
   (или аналогичный метод). Не перебарщивай: радиус ≤ 2-3 м, интенсивность
   ≤ 1.0, цвет — пастельный (не #FF0000-чистый красный).
4. **Спавн кастомных предметов:** **несколько** spawn-points с весами /
   шансами, не один на всю карту. Пример формата — список `(RoomType, weight)`,
   взвешенный random при `RoundStart`. Не клади всё в `RoomType.Surface`.
5. **Server-Specific Settings (SSS).** Если у плагина есть фичи, привязанные
   к биндам (`F`, `Alt`, цифры) — оборачивай через `FermixInput`/SSS-биндинги,
   чтобы клиент мог переназначить кнопку. Не хардкодь `KeyCode`.
6. **Локализация:** все строки (хинты, сообщения, описания, имена предметов)
   — **по-русски**. Translation-файлы из плагинов-источников переписываем,
   не копируем.
7. **Конфиг:** все настройки плагина-источника становятся полями в `Config.cs`
   с префиксом по подсистеме (`Goc...`, `Scramble...`, `RemoteKey...` и т.д.).
   Дефолты — разумные «как ванила» или с лёгким балансом для нашего сервера.
8. **Структура модуля** — следуй `.agents/SKILLS/add-system-module.md`:
   - Папка `Systems/Fermix<Name>/` или `Systems/Fermix<Name>.cs`
   - Static `Initialize()` / `Shutdown()` зарегистрировать в `FermixCore`
   - Подписки на события — через `FermixEvents`, **не** напрямую на
     `Exiled.Events.Handlers`
   - `FermixScheduler.Delay`, **не** `Timing.CallDelayed`
9. **Чёрный список зон спавна:** **никогда** не плодим предметы в
   `RoomType.Pocket`, `ZoneType.Unspecified`, и обычно не в
   `LczClassDSpawn` (стартовая для D-классов).
10. **Лицензии-источники:** GPL-3 у `MS-crew/*` и `MeowServer/*` —
    значит при копировании кода файл-приёмник тоже становится GPL.
    **Решение по умолчанию: переписываем поведение с нуля**, а не копируем
    исходники, чтобы не «заразить» лицензией всю DLL. Если без копирования
    никак — спроси Fermix'а до коммита.

---

## Список плагинов для адаптации

### 1. G.O.C.-V14.1 — `SWAG-Skay/G.O.C-V14.1`

- **URL:** https://github.com/SWAG-Skay/G.O.C-V14.1
- **Тип:** роль (Global Occult Coalition — отдельная фракция)
- **Что делает:** добавляет фракцию G.O.C., воюющую с MTF / Chaos /
  Foundation одновременно. README у автора почти пустой («Put .dll
  into Exiled --> Plugins folder») — поведение придётся восстанавливать
  из исходников релиза.
- **TODO для адаптации:**
  - [ ] Скачать последний релиз и разобрать `IL`-код / DLL Decompile
        (`ILSpy` / `dnSpy`) — у автора нет публичного исходника на default
        ветке.
  - [ ] Создать `Systems/FermixGoc/` (роль, спавн, оружие, форма).
  - [ ] Решить, использовать ли `CustomRoles` API или же подменять
        `RoleTypeId.NtfPrivate` с переименованием через `DisplayName`.
  - [ ] Спавн-волна — через `Respawn.AdvanceTimer` или собственный
        тип Wave в LabAPI 1.1.6 (если есть).
  - [ ] Хинты-уведомления о приходе волны → `FermixHint.SendToAll`,
        не `Map.Broadcast`.
  - [ ] SSS: бинд для возможных способностей (не уверен, что они есть).

### 2. Project SCRAMBLE — `MS-crew/ProjectSCRAMBLE`

- **URL:** https://github.com/MS-crew/ProjectSCRAMBLE
- **Лицензия:** GPL-3.0 → **переписываем с нуля**, не копируем код.
- **Тип:** кастомный предмет (визор), скрывающий лицо SCP-096 от триггера.
- **Что делает:** надеваемый визор (`SCP1344`-подобный), который при
  взгляде на 096 цензурит его лицо примитивом-кубом → 096 не триггерится.
  Поддерживает заряд, износ, шанс случайного «глюка» (random_error).
- **TODO для адаптации:**
  - [ ] `Systems/FermixScramble/` или `FermixCoin/Outcomes/E*` (если
        просто как outcome монетки — тогда без отдельной системы).
        Решить с Fermix'ом.
  - [ ] Кастомный предмет на базе `ItemType.SCP1344` с подменой эффекта
        и debuff-визора при взгляде на 096.
  - [ ] Спавн: 2-3 точки, например `RoomType.HczArmory` и
        `RoomType.LczArmory` с шансом 30%, `RoomType.EzIntercom` 10%.
  - [ ] Лёгкая `FermixGlow` подсветка (тёмно-синий, радиус 1.5 м, intensity 0.5).
  - [ ] Хинты на надевание/снятие/заряд через `FermixHint.SendColored`.
  - [ ] **Не** требовать `ProjectMER` — у нас своя архитектура.

### 3. RemoteKeycard — `Glesann/RemoteKeycard`

- **URL:** https://github.com/Glesann/RemoteKeycard
- **Тип:** утилитарная фича.
- **Что делает:** позволяет открывать двери / ящики, если у игрока есть
  подходящая карта **где-либо в инвентаре** — не обязательно в руках.
- **TODO для адаптации:**
  - [ ] `Systems/FermixRemoteKeycard.cs` — статический модуль, патчит
        `InteractingDoor` / `UnlockingGenerator` / `OpeningChamber` /
        `OpeningLocker` события: если deny-причина «нет карты», но в
        инвентаре есть подходящая — разрешаем, шлём `FermixHint`
        «использована карта `<тип>`».
  - [ ] Конфиг: `RemoteKeycardEnabled` (default true),
        `RemoteKeycardWorksOnLockers` (default true),
        `RemoteKeycardWorksOnGenerators` (default false — баланс).
  - [ ] **Никаких** patch'ей через Harmony, если можно через события
        EXILED — только если событий не хватает.

### 4. Callvote — `Unbistrackted/Callvote`

- **URL:** https://github.com/Unbistrackted/Callvote
- **Тип:** инфраструктура (голосования).
- **Что делает:** RA / клиентские команды на голосование за **Kick /
  RestartRound / Kill / RespawnWave / FriendlyFire / Custom**.
  Аналог cs2-style `callvote`.
- **TODO для адаптации:**
  - [ ] `Commands/CallvoteCommand.cs` (ClientCommand или RA-command —
        решить с Fermix'ом, скорее всего ClientCommand `.callvote`).
  - [ ] Подсистема `Systems/FermixVote/` со state-машиной голосования:
        текущий бюллетень, таймер, голоса, исход.
  - [ ] UI голосования через `FermixHint` (а не broadcast).
        Каждый игрок видит хинт в углу: «голосуйте F1 за / F2 против,
        осталось N сек».
  - [ ] Биндинги `F1`/`F2` для голосования через **SSS** (не хардкод).
  - [ ] Конфиг: `VoteEnabled`, `VoteTypes` (whitelist),
        `VoteCooldown`, `VoteThreshold` (% за), `VoteDuration` (сек),
        `VoteAllowedRoles` (список разрешённых ролей).
  - [ ] Логирование исходов в `FermixLog.Action`.

### 5. SCP-079 Generator List — `HyperBeastHUB/SCP-Generator-List`

- **URL:** https://github.com/HyperBeastHUB/SCP-Generator-List
- **Тип:** UI/HUD для SCP.
- **Что делает:** показывает SCP-командe список активирующихся
  генераторов и таймер до полной активации (079 в опасности).
- **TODO для адаптации:**
  - [ ] `Systems/FermixGeneratorHud.cs`.
  - [ ] Подписки: `OnActivateGenerator`, `OnStopGenerator`,
        `OnGeneratorActivated` (если есть). Уже частично есть в
        `FermixEvents` (см. строки 107-116 `Core/FermixEvents.cs`).
  - [ ] Раз в 1 сек обновлять `FermixHint` всем SCP с активными
        генераторами и оставшимся временем (`FermixScheduler.Repeat`).
  - [ ] Формат: «GEN-XX: 23 сек», «GEN-YY: остановлен», цветовая
        кодировка по близости к активации (зелёный → жёлтый → красный).
  - [ ] **Не** требовать `RueI` — используем нашу `FermixHint`.

### 6. BetterScp106 — `MS-crew/BetterScp106`

- **URL:** https://github.com/MS-crew/BetterScp106
- **Лицензия:** GPL-3.0 → **переписываем с нуля**.
- **Тип:** усиление SCP-106.
- **Что делает:**
  - `Pocket`: уйти в Pocket Dimension по команде / биндингу.
  - `Pocket-in`: затащить в PD ближайшего SCP (тот может отменить через [ALT]).
  - `Stalk`: телепорт к раненому игроку.
  - `Teleport Room`: телепорт в конкретную комнату по команде.
  - One-hit pocket (опционально, экспериментал).
- **TODO для адаптации:**
  - [ ] `Systems/FermixScp106/` со подкомандами и cooldown'ами.
  - [ ] Команды: `.GotoPocket`, `.Pocketin`, `.Stalk`, `.TeleportRoom <name>`.
        Поместить в `Commands/Scp/` (новый подфолдер).
  - [ ] Cooldown'ы и стоимость HP/Vigor — конфигурируемые
        (Scp106Pocket*, Scp106Stalk*, Scp106Teleport*).
  - [ ] SSS-бинды для способностей (PocketKey, StalkKey, etc).
  - [ ] Хинты-предупреждения жертве `Stalk` — наш `FermixHint`
        с countdown'ом.
  - [ ] **Stalk** не должен быть OP: учесть HP target'а
        (`StalkTargetMaxHealth`, default 90).
  - [ ] Whitelist комнат для `TeleportRoom` — конфиг + дефолт-список.

### 7. TextChatMeow — `MeowServer/TextChatMeow`

- **URL:** https://github.com/MeowServer/TextChatMeow
- **Лицензия:** MIT → можно копировать, но всё равно адаптируем под наш стек.
- **Тип:** глобальный текстовый чат через консоль.
- **Что делает:** игроки пишут команду в консоли (`~`), сообщение
  показывается всем как hint в углу экрана.
- **TODO для адаптации:**
  - [ ] `Commands/SayCommand.cs` (ClientCommand `.say <текст>` или
        просто `.s <текст>`).
  - [ ] `Systems/FermixChat.cs` — буфер последних N сообщений,
        отрисовка через `FermixHintStack` (наш стек хинтов уже
        поддерживает scrolling).
  - [ ] Каналы (опционально): глобальный, командный (по `Side`),
        SCP-only. Решить с Fermix'ом.
  - [ ] Анти-флуд: cooldown 3 сек, max 200 символов, фильтр
        управляющих TMP-тегов (чтобы не сломать вёрстку хинта).
  - [ ] **Не** требовать отдельный `LogWriterMeow` — пишем сообщения
        в `FermixLog`.

---

## Порядок реализации (предложенный)

Снизу вверх по сложности — для быстрой обратной связи:

1. **RemoteKeycard** (простой, 1 файл, без UI) ← начать с этого
2. **TextChatMeow** (UI через `FermixHintStack`, простая логика)
3. **SCP-079 Generator List** (HUD, подписки уже есть)
4. **Callvote** (state-машина + SSS-биндинги)
5. **Project SCRAMBLE** (кастомный предмет + glow + spawn pool)
6. **BetterScp106** (4 фичи, конфиг, биндинги)
7. **G.O.C.-V14.1** (роль/фракция — самое сложное, требует декомпиляции)

Каждый пункт = отдельный PR. После merge'а — обновляй чек-боксы в этом
файле (превращай `[ ]` в `[x]`).

---

## История адаптаций

_Обновляется при каждом релизе адаптированного плагина._

| Дата | Плагин | Версия FermixAPI | Краткие изменения |
|------|--------|-------------------|-------------------|
| —    | —      | —                 | _ещё не начато_   |
