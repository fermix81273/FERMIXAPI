# `.agents/` — папка для другого ИИ

Эта папка — **точка входа** для любого ИИ-ассистента (или нового
разработчика-человека), который должен подхватить работу над
FermixAPI вместо текущего ассистента (Devin).

Если ты ИИ и видишь этот репозиторий впервые — прочти файлы по порядку:

1. [`OVERVIEW.md`](OVERVIEW.md) — что это за репо, какие в нём проекты,
   как они связаны, кто пользователь и что он ценит.
2. [`BUILD.md`](BUILD.md) — как собрать, проверить, прогнать линт.
   Какие версии EXILED / .NET / LabAPI / Harmony нужны.
3. [`CONVENTIONS.md`](CONVENTIONS.md) — стиль кода, языки комментариев,
   именование, как разделять ответственности по модулям.
4. [`WORKFLOW.md`](WORKFLOW.md) — git-workflow (dev → main через PR,
   тэги `vX.Y.Z`, никогда не пушим в main, fast-forward dev после
   merge), как создавать PR, как ждать CI, как релизить.
5. [`GLOSSARY.md`](GLOSSARY.md) — словарь терминов: FermixGlow, RA,
   Rarity, WeightMultiplier, RoleSpawnFlags.None, Pocket Dimension и т.д.
6. [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) — известные подводные камни и
   их обходы, чтобы не наступить на те же грабли.
7. [`SKILLS/`](SKILLS/) — пошаговые «рецепты» для частых задач:
   - [`add-fermixcoin-outcome.md`](SKILLS/add-fermixcoin-outcome.md) —
     как добавить новый Outcome в FermixCoin.
   - [`debug-hints.md`](SKILLS/debug-hints.md) — как дебажить, если
     хинты не показываются.
   - [`add-system-module.md`](SKILLS/add-system-module.md) — как
     завести новый System-модуль в FermixAPI.
   - [`update-hint-engine.md`](SKILLS/update-hint-engine.md) — как
     перетянуть свежую версию HintServiceMeow в `Hints/`.
   - [`release-checklist.md`](SKILLS/release-checklist.md) — как
     выпустить новую версию (тэг + Release).
8. [`PLUGIN_ADAPTATIONS.md`](PLUGIN_ADAPTATIONS.md) — roadmap по
   адаптации сторонних SCP:SL плагинов (G.O.C., RemoteKeycard,
   Callvote, BetterScp106, TextChatMeow и др.) в наш единый
   `FermixAPI.dll`. Перед началом работы над любым из них — прочесть.

## Главное правило

**Пользователь (Fermix) общается на русском.** Все сообщения, ответы,
комментарии в коде, логи и хинты по умолчанию — **на русском**, кроме:

- XML-doc на публичных API можно оставлять смешанные (на русском в
  кратких summary, на английском технические детали).
- Идентификаторы (имена классов, методов, полей) — английские, как
  обычно в C#.

## Что НЕ делать

- Не переименовывать публичный API `FermixHint` / `FermixHintStack` /
  `FermixCore` / `CoinManager` — на них завязаны встроенные модули.
- Не пушить в `main` напрямую (там защита, и это явное правило
  пользователя).
- Не апгрейдить EXILED / LabAPI без согласования с пользователем —
  код жёстко привязан к 9.13.3 / 1.1.6.
- Не плодить новые ветки `devin/...` или `feature/...` — работаем на
  `dev`, оттуда PR в `main`.
- Не редактировать `vendor/` (это копии исходников EXILED, LabAPI и
  HintServiceMeow «как есть», для справки).

## Что ОБЯЗАТЕЛЬНО делать

- Перед коммитом: `dotnet build FermixAPI.csproj -c Release`.
  Должно быть **0 errors**.
- GitHub Actions включён на форке — после push в `dev` / создания PR
  жди CI (`build` job) и не помечай задачу выполненной до зелёного
  чекмарка. Если работаешь без локального билда (нет SCP:SL DLL'ок) —
  CI обязателен.
- При добавлении хинтов / сообщений игрокам — писать через
  `FermixHint.Send` (или подсемейство), а не напрямую `player.ShowHint`.
- При работе с задержками — использовать `FermixScheduler.Delay`,
  а не голые корутины (он сам очищает таски при reload).
