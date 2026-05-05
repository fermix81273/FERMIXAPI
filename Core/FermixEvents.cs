using System;
using System.Collections.Generic;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Scp914;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Scp173;
using Exiled.Events.EventArgs.Scp939;
using Exiled.Events.EventArgs.Scp079;
using Exiled.Events.EventArgs.Warhead;
using Exiled.Events.Features;
using Handlers = Exiled.Events.Handlers;

// Explicit namespaces for ambiguous types
using Scp049EventArgs = Exiled.Events.EventArgs.Scp049;
using Scp106EventArgs = Exiled.Events.EventArgs.Scp106;

namespace FermixAPI.Core
{
    /// <summary>
    /// Централизованная система событий FermixAPI.
    /// Предоставляет упрощённые и расширенные обертки над событиями EXILED.
    /// </summary>
    public static class FermixEvents
    {
        private static bool _isRegistered;

        // Все обработчики, которые мы подписали в Register(), сохраняются
        // в виде action'ов отписки. Это позволяет корректно отписаться от
        // EXILED-событий в Unregister() и избежать дублирования
        // обработчиков при Refresh() / hot-reload.
        private static readonly List<Action> _unsubscribers = new List<Action>();

        private static void Sub<T>(Event<T> ev, Action<T> invoker)
        {
            CustomEventHandler<T> handler = e => invoker?.Invoke(e);
            ev.Subscribe(handler);
            _unsubscribers.Add(() => ev.Unsubscribe(handler));
        }

        private static void Sub(Event ev, Action invoker)
        {
            CustomEventHandler handler = () => invoker?.Invoke();
            ev.Subscribe(handler);
            _unsubscribers.Add(() => ev.Unsubscribe(handler));
        }

        #region Player Events - Игрок

        /// <summary>Игрок присоединился к серверу.</summary>
        public static event Action<JoinedEventArgs> OnPlayerJoin;

        /// <summary>Игрок покинул сервер.</summary>
        public static event Action<LeftEventArgs> OnPlayerLeave;

        /// <summary>Игрок умирает (можно отменить).</summary>
        public static event Action<DyingEventArgs> OnPlayerDying;

        /// <summary>Игрок умер.</summary>
        public static event Action<DiedEventArgs> OnPlayerDied;

        /// <summary>Игрок получает урон (можно отменить/изменить).</summary>
        public static event Action<HurtingEventArgs> OnPlayerHurt;

        /// <summary>Игрок меняет роль.</summary>
        public static event Action<ChangingRoleEventArgs> OnRoleChange;

        /// <summary>Игрок появился после смены роли.</summary>
        public static event Action<SpawnedEventArgs> OnPlayerSpawned;

        /// <summary>Игрок сбегает.</summary>
        public static event Action<EscapingEventArgs> OnEscape;

        /// <summary>Игрок был закован в наручники.</summary>
        public static event Action<HandcuffingEventArgs> OnHandcuff;

        /// <summary>Игрок освободился от наручников.</summary>
        public static event Action<RemovingHandcuffsEventArgs> OnHandcuffRemove;

        /// <summary>Игрок использует интерком.</summary>
        public static event Action<IntercomSpeakingEventArgs> OnIntercomSpeak;

        /// <summary>Игрок прыгает.</summary>
        public static event Action<JumpingEventArgs> OnJump;

        /// <summary>Игрок приземлился.</summary>
        public static event Action<LandingEventArgs> OnLand;

        /// <summary>Игрок входит в комнату с карманным измерением.</summary>
        public static event Action<EnteringPocketDimensionEventArgs> OnEnterPocket;

        /// <summary>Игрок выходит из карманного измерения.</summary>
        public static event Action<EscapingPocketDimensionEventArgs> OnEscapePocket;

        /// <summary>Игрок нарушает правила побега из PD.</summary>
        public static event Action<FailingEscapePocketDimensionEventArgs> OnFailEscapePocket;

        /// <summary>Игрок был забанен.</summary>
        public static event Action<BannedEventArgs> OnBanned;

        /// <summary>Игрок был кикнут.</summary>
        public static event Action<KickedEventArgs> OnKicked;

        /// <summary>Игрок активирует генератор.</summary>
        public static event Action<ActivatingGeneratorEventArgs> OnActivateGenerator;

        /// <summary>Игрок деактивирует генератор.</summary>
        public static event Action<StoppingGeneratorEventArgs> OnStopGenerator;

        /// <summary>Игрок открывает генератор.</summary>
        public static event Action<OpeningGeneratorEventArgs> OnOpenGenerator;

        /// <summary>Игрок закрывает генератор.</summary>
        public static event Action<ClosingGeneratorEventArgs> OnCloseGenerator;

        /// <summary>Игрок разблокирует генератор (предварительная проверка карты).</summary>
        public static event Action<UnlockingGeneratorEventArgs> OnUnlockGenerator;

        #endregion

        #region Door & Locker Events - Двери и Шкафчики

        /// <summary>Игрок взаимодействует с дверью.</summary>
        public static event Action<InteractingDoorEventArgs> OnDoorInteract;

        /// <summary>Игрок взаимодействует с шкафчиком.</summary>
        public static event Action<InteractingLockerEventArgs> OnLockerInteract;

        /// <summary>Игрок открывает лифт.</summary>
        public static event Action<InteractingElevatorEventArgs> OnElevatorInteract;

        #endregion

        #region Item Events - Предметы

        /// <summary>Игрок поднимает предмет.</summary>
        public static event Action<PickingUpItemEventArgs> OnItemPickup;

        /// <summary>Игрок выбрасывает предмет.</summary>
        public static event Action<DroppingItemEventArgs> OnItemDrop;

        /// <summary>Игрок начинает использовать предмет.</summary>
        public static event Action<UsingItemEventArgs> OnItemUse;

        /// <summary>Игрок завершил использование предмета.</summary>
        public static event Action<UsedItemEventArgs> OnItemUsed;

        /// <summary>Игрок отменил использование предмета.</summary>
        public static event Action<CancellingItemUseEventArgs> OnItemUseCancel;

        /// <summary>Игрок меняет предмет в руке.</summary>
        public static event Action<ChangingItemEventArgs> OnItemChange;

        #endregion

        #region Weapon Events - Оружие

        /// <summary>Игрок стреляет.</summary>
        public static event Action<ShootingEventArgs> OnShoot;

        /// <summary>Игрок попал по цели.</summary>
        public static event Action<ShotEventArgs> OnShot;

        /// <summary>Игрок перезаряжает оружие.</summary>
        public static event Action<ReloadingWeaponEventArgs> OnReload;

        /// <summary>Игрок разряжает оружие.</summary>
        public static event Action<UnloadingWeaponEventArgs> OnUnload;

        /// <summary>Игрок переключает прицеливание.</summary>
        public static event Action<TogglingWeaponFlashlightEventArgs> OnToggleFlashlight;

        /// <summary>Игрок бросает гранату.</summary>
        public static event Action<ThrowingRequestEventArgs> OnThrowRequest;

        /// <summary>Граната брошена.</summary>
        public static event Action<ThrownProjectileEventArgs> OnThrown;

        #endregion

        #region Round & Server Events - Раунд и Сервер

        /// <summary>Раунд начался.</summary>
        public static event Action OnRoundStart;

        /// <summary>Раунд завершился.</summary>
        public static event Action<RoundEndedEventArgs> OnRoundEnd;

        /// <summary>Сервер ожидает игроков.</summary>
        public static event Action OnWaiting;

        /// <summary>Раунд перезапускается.</summary>
        public static event Action OnRestart;

        /// <summary>Игрок отправил репорт на читера.</summary>
        public static event Action<ReportingCheaterEventArgs> OnCheaterReport;

        /// <summary>Игрок отправил локальный репорт.</summary>
        public static event Action<LocalReportingEventArgs> OnLocalReport;

        /// <summary>Подкрепление появляется.</summary>
        public static event Action<RespawningTeamEventArgs> OnTeamRespawn;

        /// <summary>Команда выбрана для респавна.</summary>
        public static event Action<SelectingRespawnTeamEventArgs> OnTeamSelect;

        #endregion

        #region Map Events - Карта

        /// <summary>Деконтаминация LCZ начинается.</summary>
        public static event Action<DecontaminatingEventArgs> OnDecontamination;

        /// <summary>Генератор активирован.</summary>
        public static event Action<GeneratorActivatingEventArgs> OnGeneratorActivated;

        /// <summary>Свет выключен.</summary>
        public static event Action<TurningOffLightsEventArgs> OnLightsOff;

        /// <summary>Объявление C.A.S.S.I.E.</summary>
        public static event Action<AnnouncingNtfEntranceEventArgs> OnNtfAnnounce;

        /// <summary>Объявление о смерти SCP.</summary>
        public static event Action<AnnouncingScpTerminationEventArgs> OnScpDeathAnnounce;

        #endregion

        #region Warhead Events - Боеголовка

        /// <summary>Боеголовка запущена.</summary>
        public static event Action<StartingEventArgs> OnWarheadStart;

        /// <summary>Боеголовка остановлена.</summary>
        public static event Action<StoppingEventArgs> OnWarheadStop;

        /// <summary>Боеголовка взорвалась.</summary>
        public static event Action OnWarheadDetonate;

        /// <summary>Кнопка боеголовки нажата.</summary>
        public static event Action<ChangingLeverStatusEventArgs> OnWarheadButton;

        #endregion

        #region SCP-914 Events

        /// <summary>SCP-914 активирован.</summary>
        public static event Action<ActivatingEventArgs> On914Activate;

        /// <summary>Настройка SCP-914 изменена.</summary>
        public static event Action<ChangingKnobSettingEventArgs> On914KnobChange;

        /// <summary>Игрок улучшается в SCP-914.</summary>
        public static event Action<UpgradingPlayerEventArgs> On914UpgradePlayer;

        /// <summary>Предмет улучшается в SCP-914.</summary>
        public static event Action<UpgradingPickupEventArgs> On914UpgradePickup;

        /// <summary>Предмет в инвентаре улучшается в SCP-914.</summary>
        public static event Action<UpgradingInventoryItemEventArgs> On914UpgradeInventory;

        #endregion

        #region SCP Events - События SCP

        // SCP-049
        /// <summary>SCP-049 завершает операцию.</summary>
        public static event Action<Scp049EventArgs.FinishingRecallEventArgs> On049Recall;

        /// <summary>SCP-049 начинает операцию.</summary>
        public static event Action<Scp049EventArgs.StartingRecallEventArgs> On049StartRecall;

        /// <summary>SCP-049 атакует.</summary>
        public static event Action<Scp049EventArgs.AttackingEventArgs> On049Attack;

        // SCP-096
        /// <summary>SCP-096 добавляет цель.</summary>
        public static event Action<AddingTargetEventArgs> On096AddTarget;

        /// <summary>SCP-096 успокаивается.</summary>
        public static event Action<CalmingDownEventArgs> On096CalmDown;

        /// <summary>SCP-096 входит в ярость.</summary>
        public static event Action<EnragingEventArgs> On096Enrage;

        // SCP-106
        /// <summary>SCP-106 атакует.</summary>
        public static event Action<Scp106EventArgs.AttackingEventArgs> On106Attack;

        /// <summary>SCP-106 телепортируется.</summary>
        public static event Action<Scp106EventArgs.TeleportingEventArgs> On106Teleport;

        // SCP-173
        /// <summary>SCP-173 мигает.</summary>
        public static event Action<BlinkingEventArgs> On173Blink;

        // SCP-939
        /// <summary>SCP-939 использует облако амнезии.</summary>
        public static event Action<PlacingAmnesticCloudEventArgs> On939AmnesticCloud;

        /// <summary>SCP-939 сохраняет голос.</summary>
        public static event Action<SavingVoiceEventArgs> On939SaveVoice;

        /// <summary>SCP-939 воспроизводит голос.</summary>
        public static event Action<PlayingVoiceEventArgs> On939PlayVoice;

        // SCP-079
        /// <summary>SCP-079 взаимодействует с дверью.</summary>
        public static event Action<TriggeringDoorEventArgs> On079Door;

        /// <summary>SCP-079 использует тесла.</summary>
        public static event Action<InteractingTeslaEventArgs> On079Tesla;

        /// <summary>SCP-079 переключает камеру.</summary>
        public static event Action<ChangingCameraEventArgs> On079Camera;

        /// <summary>SCP-079 повышает уровень.</summary>
        public static event Action<GainingLevelEventArgs> On079LevelUp;

        /// <summary>SCP-079 получает опыт.</summary>
        public static event Action<GainingExperienceEventArgs> On079GainExp;

        #endregion

        #region Registration

        /// <summary>
        /// Регистрирует все обработчики событий.
        /// </summary>
        public static void Register()
        {
            if (_isRegistered)
            {
                FermixLog.Warn("События уже зарегистрированы.");
                return;
            }

            // Player Events
            Sub(Handlers.Player.Joined,                       ev => OnPlayerJoin?.Invoke(ev));
            Sub(Handlers.Player.Left,                         ev => OnPlayerLeave?.Invoke(ev));
            Sub(Handlers.Player.Dying,                        ev => OnPlayerDying?.Invoke(ev));
            Sub(Handlers.Player.Died,                         ev => OnPlayerDied?.Invoke(ev));
            Sub(Handlers.Player.Hurting,                      ev => OnPlayerHurt?.Invoke(ev));
            Sub(Handlers.Player.ChangingRole,                 ev => OnRoleChange?.Invoke(ev));
            Sub(Handlers.Player.Spawned,                      ev => OnPlayerSpawned?.Invoke(ev));
            Sub(Handlers.Player.Escaping,                     ev => OnEscape?.Invoke(ev));
            Sub(Handlers.Player.Handcuffing,                  ev => OnHandcuff?.Invoke(ev));
            Sub(Handlers.Player.RemovingHandcuffs,            ev => OnHandcuffRemove?.Invoke(ev));
            Sub(Handlers.Player.IntercomSpeaking,             ev => OnIntercomSpeak?.Invoke(ev));
            Sub(Handlers.Player.Jumping,                      ev => OnJump?.Invoke(ev));
            Sub(Handlers.Player.Landing,                      ev => OnLand?.Invoke(ev));
            Sub(Handlers.Player.EnteringPocketDimension,      ev => OnEnterPocket?.Invoke(ev));
            Sub(Handlers.Player.EscapingPocketDimension,      ev => OnEscapePocket?.Invoke(ev));
            Sub(Handlers.Player.FailingEscapePocketDimension, ev => OnFailEscapePocket?.Invoke(ev));
            Sub(Handlers.Player.Banned,                       ev => OnBanned?.Invoke(ev));
            Sub(Handlers.Player.Kicked,                       ev => OnKicked?.Invoke(ev));
            Sub(Handlers.Player.ActivatingGenerator,          ev => OnActivateGenerator?.Invoke(ev));
            Sub(Handlers.Player.StoppingGenerator,            ev => OnStopGenerator?.Invoke(ev));
            Sub(Handlers.Player.OpeningGenerator,             ev => OnOpenGenerator?.Invoke(ev));
            Sub(Handlers.Player.ClosingGenerator,             ev => OnCloseGenerator?.Invoke(ev));
            Sub(Handlers.Player.UnlockingGenerator,           ev => OnUnlockGenerator?.Invoke(ev));

            // Door & Locker Events
            Sub(Handlers.Player.InteractingDoor,     ev => OnDoorInteract?.Invoke(ev));
            Sub(Handlers.Player.InteractingLocker,   ev => OnLockerInteract?.Invoke(ev));
            Sub(Handlers.Player.InteractingElevator, ev => OnElevatorInteract?.Invoke(ev));

            // Item Events
            Sub(Handlers.Player.PickingUpItem,     ev => OnItemPickup?.Invoke(ev));
            Sub(Handlers.Player.DroppingItem,      ev => OnItemDrop?.Invoke(ev));
            Sub(Handlers.Player.UsingItem,         ev => OnItemUse?.Invoke(ev));
            Sub(Handlers.Player.UsedItem,          ev => OnItemUsed?.Invoke(ev));
            Sub(Handlers.Player.CancellingItemUse, ev => OnItemUseCancel?.Invoke(ev));
            Sub(Handlers.Player.ChangingItem,      ev => OnItemChange?.Invoke(ev));

            // Weapon Events
            Sub(Handlers.Player.Shooting,                 ev => OnShoot?.Invoke(ev));
            Sub(Handlers.Player.Shot,                     ev => OnShot?.Invoke(ev));
            Sub(Handlers.Player.ReloadingWeapon,          ev => OnReload?.Invoke(ev));
            Sub(Handlers.Player.UnloadingWeapon,          ev => OnUnload?.Invoke(ev));
            Sub(Handlers.Player.TogglingWeaponFlashlight, ev => OnToggleFlashlight?.Invoke(ev));
            Sub(Handlers.Player.ThrowingRequest,          ev => OnThrowRequest?.Invoke(ev));
            Sub(Handlers.Player.ThrownProjectile,         ev => OnThrown?.Invoke(ev));

            // Server Events
            Sub(Handlers.Server.RoundStarted,          () => OnRoundStart?.Invoke());
            Sub(Handlers.Server.RoundEnded,            ev => OnRoundEnd?.Invoke(ev));
            Sub(Handlers.Server.WaitingForPlayers,     () => OnWaiting?.Invoke());
            Sub(Handlers.Server.RestartingRound,       () => OnRestart?.Invoke());
            Sub(Handlers.Server.ReportingCheater,      ev => OnCheaterReport?.Invoke(ev));
            Sub(Handlers.Server.LocalReporting,        ev => OnLocalReport?.Invoke(ev));
            Sub(Handlers.Server.RespawningTeam,        ev => OnTeamRespawn?.Invoke(ev));
            Sub(Handlers.Server.SelectingRespawnTeam,  ev => OnTeamSelect?.Invoke(ev));

            // Map Events
            Sub(Handlers.Map.Decontaminating,           ev => OnDecontamination?.Invoke(ev));
            Sub(Handlers.Map.GeneratorActivating,       ev => OnGeneratorActivated?.Invoke(ev));
            Sub(Handlers.Map.TurningOffLights,          ev => OnLightsOff?.Invoke(ev));
            Sub(Handlers.Map.AnnouncingNtfEntrance,     ev => OnNtfAnnounce?.Invoke(ev));
            Sub(Handlers.Map.AnnouncingScpTermination,  ev => OnScpDeathAnnounce?.Invoke(ev));

            // Warhead Events
            Sub(Handlers.Warhead.Starting,            ev => OnWarheadStart?.Invoke(ev));
            Sub(Handlers.Warhead.Stopping,            ev => OnWarheadStop?.Invoke(ev));
            Sub(Handlers.Warhead.Detonated,           () => OnWarheadDetonate?.Invoke());
            Sub(Handlers.Warhead.ChangingLeverStatus, ev => OnWarheadButton?.Invoke(ev));

            // SCP-914 Events
            Sub(Handlers.Scp914.Activating,            ev => On914Activate?.Invoke(ev));
            Sub(Handlers.Scp914.ChangingKnobSetting,   ev => On914KnobChange?.Invoke(ev));
            Sub(Handlers.Scp914.UpgradingPlayer,       ev => On914UpgradePlayer?.Invoke(ev));
            Sub(Handlers.Scp914.UpgradingPickup,       ev => On914UpgradePickup?.Invoke(ev));
            Sub(Handlers.Scp914.UpgradingInventoryItem, ev => On914UpgradeInventory?.Invoke(ev));

            // SCP Events
            Sub(Handlers.Scp049.FinishingRecall, ev => On049Recall?.Invoke(ev));
            Sub(Handlers.Scp049.StartingRecall,  ev => On049StartRecall?.Invoke(ev));
            Sub(Handlers.Scp049.Attacking,       ev => On049Attack?.Invoke(ev));

            Sub(Handlers.Scp096.AddingTarget, ev => On096AddTarget?.Invoke(ev));
            Sub(Handlers.Scp096.CalmingDown,  ev => On096CalmDown?.Invoke(ev));
            Sub(Handlers.Scp096.Enraging,     ev => On096Enrage?.Invoke(ev));

            Sub(Handlers.Scp106.Attacking,    ev => On106Attack?.Invoke(ev));
            Sub(Handlers.Scp106.Teleporting,  ev => On106Teleport?.Invoke(ev));

            Sub(Handlers.Scp173.Blinking,     ev => On173Blink?.Invoke(ev));

            Sub(Handlers.Scp939.PlacingAmnesticCloud, ev => On939AmnesticCloud?.Invoke(ev));
            Sub(Handlers.Scp939.SavingVoice,          ev => On939SaveVoice?.Invoke(ev));
            Sub(Handlers.Scp939.PlayingVoice,         ev => On939PlayVoice?.Invoke(ev));

            Sub(Handlers.Scp079.TriggeringDoor,      ev => On079Door?.Invoke(ev));
            Sub(Handlers.Scp079.InteractingTesla,    ev => On079Tesla?.Invoke(ev));
            Sub(Handlers.Scp079.ChangingCamera,      ev => On079Camera?.Invoke(ev));
            Sub(Handlers.Scp079.GainingLevel,        ev => On079LevelUp?.Invoke(ev));
            Sub(Handlers.Scp079.GainingExperience,   ev => On079GainExp?.Invoke(ev));

            _isRegistered = true;
            FermixLog.Debug("Все события зарегистрированы.");
        }

        /// <summary>
        /// Отписывается от всех событий.
        /// </summary>
        public static void Unregister()
        {
            if (!_isRegistered)
                return;

            foreach (var unsub in _unsubscribers)
            {
                try
                {
                    unsub();
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"Ошибка отписки от события: {ex.Message}");
                }
            }

            _unsubscribers.Clear();
            _isRegistered = false;
            FermixLog.Debug("События отписаны.");
        }

        /// <summary>
        /// Перерегистрирует события (для hot-reload).
        /// </summary>
        public static void Refresh()
        {
            Unregister();
            Register();
        }

        #endregion
    }
}
