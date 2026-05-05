# KNOWN_ISSUES — известные подводные камни

## FermixCoin.dll больше не нужен на сервере (с v2.4.0)

**Симптом:** после обновления до v2.4.0+ на сервере лежат два файла:
`FermixAPI.dll` и старый `FermixCoin.dll`.

**Проблема:** FermixCoin теперь встроен в FermixAPI.dll. Если старый
`FermixCoin.dll` останется в `EXILED/Plugins/`, EXILED попытается
загрузить его как отдельный плагин, что вызовет конфликт.

**Решение:** удалить `FermixCoin.dll` (и `FermixCoin.pdb`) из
`EXILED/Plugins/`. Оставить только `FermixAPI.dll`.

## Hint-движок и сторонние плагины

**Симптом:** хинты от FermixCoin (или FermixHint в целом) показываются
на полсекунды, потом исчезают, либо вообще не появляются.

**Причина:** на сервере параллельно работает другой плагин, который
использует HintServiceMeow (HSM) или просто часто зовёт
`player.ShowHint`. Без HSM каждый вызов `ShowHint` затирает
предыдущий — побеждает последний.

**Решение (применено в v2.3.0):** мы **встроили HSM прямо в FermixAPI**.
Источники в `Hints/`, атрибуция в `vendor/HintServiceMeow/`. На
WaitingForPlayers вызываем `Patcher.Patch()` — все вызовы
`Player.ShowHint` / `LabApi.Player.SendHint` (от любого плагина)
теперь идут через наш кооперативный pipeline.

**Если симптом вернулся в новой версии:**

1. Проверь, что `FermixCore.IsHintEnginePatched == true` через
   `.fermixstatus`-команду или просто грепни лог сервера на строку
   `Применить Harmony-патчи hint-движка`.
2. Если патч не применился — почитай stack trace в логе и проверь,
   что Harmony в принципе подгружен (EXILED грузит `0Harmony.dll`,
   но если человек руками удалил его — патч упадёт).
3. См. чек-лист в [`SKILLS/debug-hints.md`](SKILLS/debug-hints.md).

## D2 — туториал с сохранением инвентаря

**Симптом:** при срабатывании исхода D2 игрок становится
TutorialPlayer, но инвентарь чистится.

**Причина:** `Role.Set(role)` без флагов вызывает поведение «полный
respawn» — очищает инвентарь, телепортирует на спавн.

**Решение:** использовать `Role.Set(role, RoleSpawnFlags.None)`.
Этот флаг сохраняет инвентарь, позицию, хп, эффекты. Уже применено
в `FermixCoin/Outcomes/RoleOutcomes.cs::D2`.

## C3 — возврат из Pocket Dimension

**Симптом:** игрок возвращается на 1 метр выше своей исходной точки,
делает мини-падение.

**Причина (исторически):** мы добавляли `+ Vector3.up * 1.0f` ко
всем телепортам, включая возврат из Pocket. Но `origin` для возврата
— это уже сохранённая `player.Position` (валидные координаты ног),
а не `Room.Position` (координаты пола).

**Правило:** offset `+1.0f` нужен **только** при телепорте по
позиции комнаты (`Room.Position`). При телепорте по позиции игрока
(`player.Position`) offset **не нужен**.

См. `FermixCoin/Outcomes/TeleportOutcomes.cs::C3` — fixed в
v2.2.1.

## Boss-роли через Role.Set без RoleSpawnFlags

**Симптом:** при спавне SCP-079 / 049 / 173 / 939 через `Role.Set`
игроку показывается лоадинг-экран на 2-3 секунды, а у других
игроков SCP «телепортируется».

**Причина:** EXILED по умолчанию выполняет полную (server-side)
смену роли с пересинхронизацией всего стейта.

**Обходов несколько:** см. EXILED docs про SpawnReason / FlagsHandler.
Пока не реализовано в FermixCoin (исходов на спавн SCP-боссов нет).
Если будешь добавлять — учитывай этот lag и предупреждай игрока
хинтом «Превращение через 2 сек...».

## RoleSpawnFlags.None и `OnSpawning`/`OnSpawned`

**Симптом:** хочется среагировать на смену роли (например, дать
эффект), но `OnSpawning` не срабатывает при `Role.Set(...,
RoleSpawnFlags.None)`.

**Причина:** `RoleSpawnFlags.None` отключает событийную часть смены
роли, чтобы не запускать спавн-логику.

**Решение:** инлайн после `Role.Set(...)`, не через event handler.

## Hints/UI/CommonHint — не используем

**Не путать:** В `Hints/UI/Utilities/CommonHint.cs` есть готовые
helpers для item-pickup hints, role hints и т.п. Это API из
HintServiceMeow. Мы их **не используем**, потому что у нас своя
семантика хинтов через `FermixHintStack`. Но код оставлен (а не
удалён), чтобы было проще переехать на новые версии HSM, если
такое понадобится.

Если ты хочешь добавить «хинт при подборе предмета» — пиши через
`FermixHint.Send(player, ...)` на событии `Player.PickingUpItem`,
а не через `CommonHint`.

## Workbench (станция прокачки) спавн

**Симптом (исторический):** исход F4 спавнил Workbench под игроком.
Пользователь жаловался, что workbench-структуры из PrefabHelper
**не убираются между раундами** — то есть карта засирается.

**Решение:** F4 удалён в v2.2.1. Не возвращай его в этой форме.

Если вдруг захочется его вернуть — сначала найди, как HSM/EXILED
чистит prefabs между раундами (`Round.OnRestart`?), и удаляй
spawned'ы там.

## YamlDotNet версия

**Симптом:** YamlDotNet `13.7.1` (наш) ниже, чем в самом upstream
HintServiceMeow (`16.3.0`). Если кто-нибудь добавит в `Hints/` код,
использующий новые API YamlDotNet — построит, но рантайм может
упасть.

**Решение:** пока всё ОК, потому что код в `Hints/` использует
только базовые классы. Если будешь обновлять `Hints/` с upstream —
сравни вызовы YamlDotNet и при необходимости подними версию в
`FermixAPI.csproj`.

## net48 и `System.IO.Compression`

**Симптом:** при сборке `Hints/Core/Utilities/Tools/FontTool.cs`
падает с `error CS1069: ZipArchive forwarded to System.IO.Compression`.

**Причина:** на target framework `net48` ZipArchive не подхватывается
автоматически.

**Решение (применено):** в `FermixAPI.csproj` добавлено
`<Reference Include="System.IO.Compression" />` и `System.IO.Compression.FileSystem`.

## global::Hints в коде Hints/

**Симптом:** компилятор не находит `Hint`, `HintDisplay`, `HintEffect`,
`TextHint` внутри файлов в `Hints/`.

**Причина:** наш namespace `FermixAPI.Hints` shadow'ит нативный
`global::Hints` (от SCP:SL). Когда в файле, лежащем в
`FermixAPI.Hints.X.Y`, написано `Hints.HintDisplay` или просто
`using Hints;`, компилятор сначала ищет в нашем namespace, а только
потом во внешнем.

**Решение:** в файлах `Hints/Core/Utilities/Patch/Patches.cs`,
`Patcher.cs`, `UnityAdaptors/ScpslDisplayOutput.cs` обращения к
SCP:SL'овскому `Hints.X` заменены на `global::Hints.X`. Если
будешь добавлять новые файлы в `Hints/`, которые работают с native
типами — следуй той же конвенции.
