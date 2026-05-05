# WORKFLOW — git, PR, релизы

## Ветки

| Ветка | Назначение |
| --- | --- |
| `main` | Только релизные коммиты. Защищена. Прямой push **запрещён**. |
| `dev` | Долгоживущая интеграционная ветка. Здесь идёт вся работа. |

Других веток быть **не должно**. Старые `devin/...` ветки удалены и
повторно создавать их нельзя.

## Жизненный цикл изменения

```
        you commit on dev
                 │
                 ▼
        push dev → GitHub
                 │
                 ▼
       open PR  dev → main      ← ИИ создаёт через git_pr action=create
                 │
                 ▼
            CI runs (4 checks)
                 │
                 ▼
   user clicks "Merge" on GitHub  ← Devin не может это сделать сам
                 │
                 ▼
        ИИ: git checkout dev
            git fetch origin
            git merge --ff-only origin/main
            git push origin dev
                 │
                 ▼
        ИИ: git tag vX.Y.Z
            git push origin vX.Y.Z
                 │
                 ▼
       CI собирает Release с DLL
                 (или релиз создаётся вручную через GitHub API)
                 │
                 ▼
       пользователь скачивает,
       ставит на сервер, тестирует
```

## Команды на каждый шаг

```bash
# Перед стартом задачи:
git checkout dev
git pull --ff-only

# Делать изменения, коммитить:
git add Core/FermixCore.cs Hints/...
git commit -m "Краткое описание на русском"

# Перед пушем — убедиться, что собирается:
dotnet build FermixAPI.csproj -c Release

# Пушим dev:
git push origin dev

# Открываем PR через ИИ-инструменты (gh CLI запрещён):
#   git_pr action=fetch_template repo=wv4kyxhrhr-code/FERMIXAPI
#   git_pr action=create repo=wv4kyxhrhr-code/FERMIXAPI head_branch=dev base_branch=main
```

## После merge'а пользователем

```bash
git checkout dev
git fetch origin
git merge --ff-only origin/main   # подтягиваем merge-коммит из main
git push origin dev

# Тэгаем релиз (только если задача релизная — не каждый PR):
git tag v2.3.0
git push origin v2.3.0
```

CI workflow `.github/workflows/build.yml` сам соберёт DLL и
опубликует Release при пуше тега `v*`.

> Если CI не активирован, создай Release вручную:
> 1. Собрать локально: `dotnet build FermixAPI.csproj -c Release`
> 2. Создать Release через GitHub API (токен `GITHUB_RELEASE_TOKEN`)
> 3. Залить `FermixAPI.dll`, `FermixAPI.pdb`, `FermixAPI-vX.Y.Z.zip`

## Правила версионирования (semver)

- **Major (`X.0.0`)** — ломающие изменения публичного API
  FermixAPI или обязательного формата конфига. Пока не было.
- **Minor (`2.X.0`)** — добавление новых публичных API, новые
  System-модули, новые плагины (например v2.2.0 — добавлен
  FermixCoin), новая зависимость (например v2.3.0 — встроен
  hint-движок).
- **Patch (`2.2.X`)** — багфиксы, мелкие подкрутки шансов /
  параметров, обновление документации, замена констант.

## Что писать в PR-теле

- **Заголовок:** что меняем одной фразой на русском.
- **Что внутри:** список фич/багфиксов, по одному на строку,
  с эмодзи маркером (галочка / молоток / лампочка) — пользователь
  любит визуально читать. **Эмодзи только в сообщениях/PR-теле,
  НЕ в коде.**
- **Что важно проверить вручную:** короткий чек-лист пунктов,
  которые пользователь может сам нажать в игре.
- **Связанные issue/тикеты:** если есть.
- Если вызов сделан Devin'ом — сессионный URL и автор уже добавятся
  автоматически, не дублируй вручную.

## Что писать в commit-сообщении

Кратко, на русском. Например:

```
Fix C3 return teleport: don't apply SafeUpOffset to saved origin

Devin Review поймал, что origin — уже валидная позиция игрока
(ноги), а не пол комнаты. Доп. offset делал «прыжок» наверх.
```

Никаких `Co-Authored-By: Devin`-плашек в commit'ах — пользователь
не любит шум в истории.

## Когда **не** делать PR

- Если задача — «прочитай код и объясни», PR не нужен.
- Если изменения чисто в `.agents/`, можно делать PR и сразу
  мерджить.
- Если в задаче явно сказано «не создавай PR», не создавай.

## Когда **обязательно** делать PR

- Любые изменения, попадающие в `.dll` (то есть всё в корне проекта:
  `Core/`, `Systems/`, `Extensions/`, `Hints/`, `FermixCoin/`, и т.д.).
- Любые изменения в `.github/workflows/`.
- Любые изменения структуры репо (новые папки, удаление).
