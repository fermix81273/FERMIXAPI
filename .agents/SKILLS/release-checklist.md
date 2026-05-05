---
name: release-checklist
description: Чек-лист для выпуска новой версии (тэг + GitHub Release).
---

# SKILL — Выпустить релиз

## Пре-реквизиты

- На `dev` уже есть набор изменений, который нужно зарелизить.
- Все изменения собираются 0/0.
- PR `dev → main` открыт **либо** уже смержен пользователем.

## Шаги

### 1. Определить версию

Применить semver (см. [`WORKFLOW.md`](../WORKFLOW.md#правила-версионирования-semver)):

| Что меняется | Bump |
| --- | --- |
| Только конфигов / выкручены шансы / косметика | `2.X.Y → 2.X.(Y+1)` (patch) |
| Новый плагин / системный модуль / API | `2.X.Y → 2.(X+1).0` (minor) |
| Ломаешь публичный API | `2.X.Y → 3.0.0` (major) |
| Встраиваешь новую внешнюю зависимость | `2.X.Y → 2.(X+1).0` (minor) |

Текущая версия — посмотри `git describe --tags --abbrev=0`.

### 2. Поднять `<Version>` в csproj

```bash
# В корневом FermixAPI.csproj
# поменять <Version>2.X.Y</Version>
```

### 3. Поднять `VersionMajor/Minor/Patch` в FermixCore

```csharp
// Core/FermixCore.cs
public const int VersionMajor = 2;
public const int VersionMinor = 3;
public const int VersionPatch = 0;
```

Эта версия выводится в логе `FermixLog.Info($"Ядро FermixAPI v{Version}")`.

### 4. Собрать локально

```bash
dotnet build FermixAPI.csproj -c Release
```

0 errors. FermixCoin теперь встроен в FermixAPI.dll — отдельной сборки нет.

### 5. Закоммитить версионный bump

```bash
git add FermixAPI.csproj Core/FermixCore.cs
git commit -m "Bump version to 2.X.Y"
git push origin dev
```

### 6. Открыть PR и дождаться merge

См. [`WORKFLOW.md`](../WORKFLOW.md). PR `dev → main`. CI должен быть
зелёным (4/4). Пользователь мерджит.

### 7. Подтянуть main в dev

```bash
git checkout dev
git fetch origin
git merge --ff-only origin/main
git push origin dev
```

### 8. Тэгнуть

```bash
# Пример для v2.3.0
git tag v2.3.0
git push origin v2.3.0
```

CI workflow `.github/workflows/build.yml` сам соберёт DLL и
опубликует Release при пуше тега `v*`.

> **Если CI не активирован**, создай Release вручную:
> 1. Собери локально: `dotnet build FermixAPI.csproj -c Release`
> 2. Создай Release через GitHub API с токеном `GITHUB_RELEASE_TOKEN`
>    (сохранён как repo-scoped secret)
> 3. Залей ассеты:
>    - `FermixAPI.dll` + `FermixAPI.pdb`
>    - `FermixAPI-vX.Y.Z.zip`

### 9. Проверить релиз на GitHub

```
https://github.com/wv4kyxhrhr-code/FERMIXAPI/releases/tag/v2.X.Y
```

Должны быть ассеты: `FermixAPI.dll`, `FermixAPI.pdb`, `FermixAPI-vX.Y.Z.zip`.

### 10. Сообщить пользователю

```
Релиз v2.X.Y опубликован: <ссылка на release>

Что внутри:
- ...
- ...

Что проверить в игре:
- ...
- ...

Кладёшь FermixAPI.dll в EXILED/Plugins/ (отдельный FermixCoin.dll
больше не нужен — удали его), рестартуешь сервер.
```

## Чек-лист

- [ ] Версия определена правильно (semver)
- [ ] `<Version>` обновлён в FermixAPI.csproj
- [ ] `VersionMajor/Minor/Patch` обновлён в FermixCore
- [ ] Локальная сборка 0 errors
- [ ] Коммит-bump запушен в `dev`
- [ ] PR `dev → main` смержен
- [ ] `dev` подтянут до `main`
- [ ] Тэг `vX.Y.Z` запушен
- [ ] Release создан (CI или вручную через GitHub API)
- [ ] Ассеты: `FermixAPI.dll`, `FermixAPI.pdb`, `FermixAPI-vX.Y.Z.zip`
- [ ] Пользователю отправлено сообщение со ссылкой и чек-листом
