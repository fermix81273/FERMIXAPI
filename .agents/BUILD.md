# BUILD — как собрать и проверить

## Версии

| Компонент | Версия | Где задаётся |
| --- | --- | --- |
| .NET SDK | 8.x (для `dotnet build`) | глобально |
| Target framework | `net48` | `FermixAPI.csproj` |
| EXILED | 9.13.3 | `refs/Exiled.*.dll`, `Plugin.cs::RequiredExiledVersion` |
| LabAPI | 1.1.6 | `refs/LabApi.dll`, `FermixCore.MinimumLabApiVersion` |
| Lib.Harmony (`0Harmony.dll`) | как у EXILED 9.13.3 | `refs/0Harmony.dll` |
| YamlDotNet | 13.7.1 (NuGet) | `FermixAPI.csproj` |
| Newtonsoft.Json | 13.0.3 (NuGet) | `FermixAPI.csproj` |

EXILED сам грузит `0Harmony.dll` рантайме, так что в Releases его
включать не нужно.

## Где лежат бинарные ссылки

`refs/` содержит DLL'ки, на которые ссылается компилятор:

- `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`,
  `CommandSystem.Core.dll`, `Mirror.dll`, `PluginAPI.dll`,
  `NorthwoodLib.dll`, `Pooling.dll` — игровые сборки SCP:SL.
- `Exiled.API.dll`, `Exiled.Events.dll`, `Exiled.Loader.dll`,
  `Exiled.CustomItems.dll`, `Exiled.CustomRoles.dll`,
  `Exiled.CreditTags.dll`, `Exiled.Permissions.dll`,
  `SemanticVersioning.dll` — EXILED 9.13.3.
- `LabApi.dll` — LabAPI 1.1.6.
- `UnityEngine*.dll` — Unity 2019 modules.
- `0Harmony.dll` — Lib.Harmony, нужен встроенному hint-движку.
- `Mono.Posix.dll`, `System.ComponentModel.DataAnnotations.dll` —
  системные зависимости net48.

В CI на GitHub Actions те же DLL'ки распакованы из последнего релиза
EXILED ровно в тот же `refs/` (см. `.github/workflows/build.yml`).

## Сборка локально

```bash
# Из корня репозитория:
dotnet build FermixAPI.csproj -c Release
```

Результат:

- `bin/Release/net48/FermixAPI.dll` (~400 КБ, включает FermixCoin) и `FermixAPI.pdb`.

**Отдельного `FermixCoin.dll` больше нет** — всё в одном `FermixAPI.dll`.

**Целевой результат — 0 errors.** Известные MSB3277 warnings
про System.IO.Compression — норма (конфликт net48 ссылок).

## Линт / форматирование

Отдельных линтеров нет — компилятор сам ловит warning'и. Если
добавляются новые warning-категории, подавлять только в крайнем
случае и с комментарием.

## CI

GitHub Actions workflow `.github/workflows/build.yml`:

1. На push в `dev` или `main` — собирает FermixAPI.csproj в Release-конфиге.
2. Публикует артефакты на job (для дебага).
3. На push тега `vX.Y.Z` — дополнительно публикует GitHub Release с:
   - `FermixAPI.dll` + `FermixAPI.pdb`
   - `FermixAPI-vX.Y.Z.zip` — всё в одном архиве.

> **Важно:** GitHub Actions на этом репо может быть не активирован
> (это форк, Actions нужно включить в Settings → Actions → General).
> Если CI не срабатывает — релизы создаются вручную через
> GitHub API с помощью токена `GITHUB_RELEASE_TOKEN`
> (сохранён как repo-scoped secret).

## Тестирование

Автоматических unit-тестов **нет**. Тестирование — **в игре**, на
сервере пользователя. Workflow:

1. Собрать локально: `dotnet build FermixAPI.csproj -c Release` → 0 errors.
2. Запушить в `dev`.
3. Открыть PR `dev → main`.
4. Пользователь мерджит на GitHub.
5. Подтянуть main в dev, тэгнуть `vX.Y.Z`.
6. Создать Release на GitHub (через CI или вручную через API).
7. Пользователь скачивает `FermixAPI.dll` из Release, кладёт в
   `EXILED/Plugins/`, рестартует сервер, проверяет глазами.

Если изменение можно проверить статически (например, регресс на
отрисовку RichText в hint-движке), напиши минимальный smoke-test в
комментарии PR — пользователь оценит, и это поможет другим ИИ.

## Чек-лист перед PR

- [ ] `dotnet build FermixAPI.csproj -c Release` → 0 errors.
- [ ] В `FermixAPI.csproj` версия `<Version>` соответствует тегу,
      который собираешься поставить.
- [ ] В `FermixCore.cs` константы `VersionMajor/Minor/Patch` тоже.
- [ ] Если затронут публичный API — обновлён `README.md` корня и/или
      `examples/`.
- [ ] Если интегрирован новый сторонний код — обновлён
      `vendor/<имя>/README.md` с атрибуцией и лицензией.
