using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.Systems;
using UnityEngine;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>
    /// Категория C: телепортация. Имбовые варианты — с пониженным шансом.
    /// Логика выбора целей портирована из <c>Hazbin.Teleports</c>:
    /// валидные комнаты и игроки берутся из allow-листов
    /// <see cref="FermixTeleportRegistry"/>, который сам убирает невалидные
    /// цели на ключевых событиях раунда (декон LCZ, старт БГ, смена роли,
    /// дисконнект). Это и есть та самая «избегаемая ненужная телепортация»,
    /// о которой просил пользователь — например, исход «случайная комната»
    /// после старта БГ выкинет на Surface, а не в HCZ под обстрел.
    /// Никаких отсчётов и шансов «не твой день» — это поведение взято из
    /// <c>BetterCoins.TeleportChance</c>, и пользователь явно от него отказался.
    /// </summary>
    public static class TeleportOutcomes
    {
        private const float SafeUpOffset = 1.0f;

        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "C1",
                name: "Случайная комната с карты",
                rarity: Rarity.Common,
                message: "Хоп! Случайная комната с карты.",
                comment: "Найди дорогу обратно. Или не находи, я не настаиваю.",
                action: p =>
                {
                    var room = FermixTeleportRegistry.RandomRoom(ignorePocket: true);
                    if (room == null) return;
                    p.Teleport(room.Position + Vector3.up * SafeUpOffset);
                }));

            sink.Add(new Outcome(
                id: "C2",
                name: "К случайному игроку",
                rarity: Rarity.Rare,
                message: "Тебя засосало к случайному игроку.",
                comment: "Привет! Извини, я не сам пришёл.",
                weightMultiplier: 0.4f,
                action: p =>
                {
                    var dest = FermixTeleportRegistry.RandomPlayer(except: p);
                    if (dest == null) return;
                    p.Teleport(dest.Position + Vector3.up * SafeUpOffset);
                }));

            sink.Add(new Outcome(
                id: "C3",
                name: "Pocket Dimension на 5 секунд",
                rarity: Rarity.Epic,
                message: "Ты в Pocket Dimension... ровно 5 секунд.",
                comment: "Дыши спокойно. SCP-106 в курсе твоего визита.",
                weightMultiplier: 0.4f,
                action: p =>
                {
                    var pocket = Room.Get(RoomType.Pocket);
                    var origin = p.Position;

                    if (pocket != null)
                        p.Teleport(pocket.Position + Vector3.up * SafeUpOffset);
                    else
                        p.EnableEffect(EffectType.PocketCorroding, 1, 5f);

                    FermixScheduler.Delay(5f, () =>
                    {
                        if (p == null || !p.IsConnected || !p.IsAlive)
                            return;
                        p.Teleport(origin);
                    });
                }));

            sink.Add(new Outcome(
                id: "C4",
                name: "Поверхность",
                rarity: Rarity.Rare,
                message: "Ты на поверхности!",
                comment: "Свежий воздух, солнышко, MTF... сюрприз.",
                weightMultiplier: 0.4f,
                action: p =>
                {
                    var surface = Room.Get(RoomType.Surface);
                    if (surface != null)
                        p.Teleport(surface.Position + Vector3.up * SafeUpOffset);
                }));
        }
    }
}
