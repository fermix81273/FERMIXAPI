# OVERVIEW — что вообще в этом репозитории

## Кто пользователь

- **Fermix** (`grigoreve123@gmail.com`) — администратор частного
  SCP:SL-сервера. Пишет на русском, любит «прикольные» механики,
  любит цвет и эмодзи в хинтах в игре, но **не** в коде и сообщениях
  Devin (см. `CONVENTIONS.md`).
- Его сервер использует EXILED `9.13.3` и LabAPI `1.1.6`.
- Он сам **не** пишет код выпускающим образом, но активно тестирует
  плагины «вживую» на сервере и присылает скриншоты багов.
- Релизы он скачивает как готовый **один** `FermixAPI.dll` из GitHub
  Releases и кладёт в `EXILED/Plugins/`.

## Что в репозитории

```
FERMIXAPI/
├── FermixAPI.csproj          ← единственный проект (DLL = «FermixAPI.dll»)
├── Plugin.cs                  ← EXILED-плагин-обёртка над FermixCore
├── Config.cs                  ← общий конфиг (включая настройки FermixCoin)
├── Core/                      ← FermixCore, Scheduler, HintStack, Events, Paths
├── Systems/                   ← FermixGlow, FermixDoors, FermixRoles, ...
├── Extensions/                ← FermixHint (публичный API), PlayerExtensions
├── Commands/                  ← ResurrectCommand, RoundTimeCommand, TpsCommand, ...
├── Integration/               ← LabApiIntegration, LabApiCommands, LabApiEvents
├── Utils/                     ← FermixConfigUtils, FermixData, FermixLog
├── Hints/                     ← ВСТРОЕННЫЙ hint-движок (бывший HintServiceMeow)
│   ├── Core/                  ← PlayerDisplay, HintCollection, парсер RichText
│   ├── UI/                    ← CommonHint helpers (не используются в FermixHint)
│   ├── Plugin/                ← Стабы Plugin/PluginConfig (НЕ EXILED-плагин)
│   ├── TextWidth              ← embedded-resource с таблицей ширин символов
│   └── README.md              ← как обновлять движок
├── FermixCoin/                ← ВСТРОЕННЫЙ модуль «монетка фортуны» (с v2.4.0)
│   ├── CoinManager.cs         ← статический менеджер модуля (Initialize/Shutdown)
│   ├── Core/                  ← CoinHandler, CoinGlowController, OutcomeRegistry, ...
│   └── Outcomes/              ← ~40 исходов (A1-A4, B1-B6, C1-C5, D1-D6, ...)
├── examples/                  ← примеры использования API (НЕ часть DLL)
├── vendor/                    ← копии исходников EXILED / LabAPI / HintServiceMeow
├── refs/                      ← бинарные DLL-ссылки для компиляции
├── .github/workflows/build.yml ← CI: собирает FermixAPI.dll и
│                                  кладёт в Releases при пуше тега «vX.Y.Z»
└── .agents/                   ← ЭТА ПАПКА (для другого ИИ)
```

## Архитектурный принцип

**FermixAPI — это плагин-API + все игровые модули в одной DLL.**
Он сам полноценный EXILED-плагин (в `Plugin.cs` есть
`OnEnabled`/`OnDisabled`), даёт набор удобных классов (`FermixHint`,
`FermixGlow`, `FermixScheduler`, `FermixRoles`, ...) и содержит
встроенные модули (FermixCoin и будущие плагины).

**FermixCoin** — встроенный модуль (с v2.4.0). Лежит в `FermixCoin/`
в корне проекта, namespace `FermixAPI.FermixCoin`. Компилируется в
тот же `FermixAPI.dll`. Управляется через статический `CoinManager`:
`CoinManager.Initialize()` и `CoinManager.Shutdown()` вызываются из
`FermixCore`. Конфиг монетки — в общем `Config.cs` FermixAPI.

**Все будущие плагины** добавляются по тому же принципу:
папка в корне → namespace `FermixAPI.<Имя>` → статический Manager →
инициализация из FermixCore. Отдельных DLL больше нет.

## Жизненный цикл

```
EXILED → загружает FermixAPI.dll (единственная DLL)
     ↓
FermixAPI.Plugin.OnEnabled()
     ↓
FermixCore.Initialize(plugin)
   ├── FermixPaths.Initialize()
   ├── FermixConfigUtils.Initialize()
   ├── FermixData.Initialize()
   ├── Handlers.Server.WaitingForPlayers += OnWaitingForPlayers
   │     (на старте раунда: Patcher.Patch() для движка хинтов)
   ├── Handlers.Player.Left += OnPlayerLeft
   │     (на уход игрока: PlayerDisplay.Destruct(hub))
   ├── FermixEvents.Register()
   ├── FermixScheduler.Initialize()
   ├── FermixHintStack.Initialize()
   ├── Systems.FermixInput.Initialize()
   ├── Systems.FermixGlow.Initialize()
   └── CoinManager.Initialize()          ← встроенный модуль FermixCoin
         ├── OutcomeRegistry.Initialize()
         ├── CoinGlowController.Register()
         └── CoinHandler подписывается на FlippingCoin, PickingUpItem, RestartingRound
```

## Что важно понимать про hint-движок

Чтобы хинты от FermixCoin (и любых других плагинов) гарантированно
показывались на сервере, FermixAPI **встраивает в себя**
HintServiceMeow (HSM) — код лежит в [`Hints/`](../Hints/), оригинал —
в [`vendor/HintServiceMeow/`](../vendor/HintServiceMeow/). HSM
патчит `player.ShowHint` через Harmony и кооперативно объединяет
хинты от разных плагинов в один пайплайн рендеринга.

Из этого следует:

- Никогда не зови `player.ShowHint(...)` напрямую — иди через
  `FermixHint.Send(player, msg, dur)` или другой публичный API
  `FermixHint`.
- Если ты добавляешь hint-логику в `Hints/`, помни: namespace —
  `FermixAPI.Hints.*`, а не `HintServiceMeow.*`.
- Чтобы обращаться к нативному SCP:SL `Hints` API в коде внутри
  `Hints/`, используй явное `global::Hints.X` (иначе компилятор
  ловит наш `FermixAPI.Hints` и падает с CS0246).

## Что считать «готово»

Минимальный критерий «готово» для любой задачи:

- `dotnet build FermixAPI.csproj -c Release` → 0 errors.
- Если в задаче была визуальная часть — пользователь подтвердил, что
  на сервере выглядит так, как он хотел.
- PR `dev → main` открыт, пользователь замерджил.
- Если задача релизная — тег `vX.Y.Z` + Release на GitHub с `FermixAPI.dll`.
- Тэг `vX.Y.Z` создан (после merge'а пользователем) и Release
  опубликован с актуальными DLL.
