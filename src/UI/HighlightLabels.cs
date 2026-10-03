using System;
using System.Collections.Generic;

namespace StrandedDeepModManager.UI
{
    internal static class HighlightLabels
    {
        private static readonly Dictionary<string, string> Ru =
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "split_screen", "Local split-screen" },
                { "navigation", "Навигация" },
                { "map", "Карта" },
                { "markers", "Отметки" },
                { "persistence", "Сохранение данных" },
                { "storage", "Хранилище" },
                { "furniture", "Мебель" },
                { "building", "Строительство" },
                { "raft", "Плот" },
                { "inventory", "Инвентарь" },
                { "stacking", "Стаки предметов" },
                { "crafting", "Крафт" },
                { "nearby_crafting", "Крафт рядом" },
                { "ui", "Интерфейс" },
                { "ui_scaling", "Масштаб UI" },
                { "settings", "Настройки" },
                { "ingame_settings", "Настройки в игре" },
                { "gamepad", "Геймпад" },
                { "food", "Еда" },
                { "cooking", "Готовка" },
                { "cooking_status", "Статус готовки" },
                { "fire", "Огонь" },
                { "torch", "Факел" },
                { "fuel", "Топливо" },
                { "renewable", "Возобновляемость" },
                { "regrowth", "Восстановление" },
                { "world_simulation", "Симуляция мира" },
                { "audio", "Аудио" },
                { "music", "Музыка" },
                { "radio", "Радио" },
                { "interaction", "Взаимодействие" },
                { "vehicles", "Транспорт" },
                { "quality_of_life", "Удобство" },
                { "localization", "Локализация" }
            };

        private static readonly Dictionary<string, string> En =
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                { "split_screen", "Local split-screen" },
                { "navigation", "Navigation" },
                { "map", "Map" },
                { "markers", "Markers" },
                { "persistence", "Data persistence" },
                { "storage", "Storage" },
                { "furniture", "Furniture" },
                { "building", "Building" },
                { "raft", "Raft" },
                { "inventory", "Inventory" },
                { "stacking", "Item stacking" },
                { "crafting", "Crafting" },
                { "nearby_crafting", "Nearby crafting" },
                { "ui", "Interface" },
                { "ui_scaling", "UI scaling" },
                { "settings", "Settings" },
                { "ingame_settings", "In-game settings" },
                { "gamepad", "Gamepad" },
                { "food", "Food" },
                { "cooking", "Cooking" },
                { "cooking_status", "Cooking status" },
                { "fire", "Fire" },
                { "torch", "Torch" },
                { "fuel", "Fuel" },
                { "renewable", "Renewable" },
                { "regrowth", "Regrowth" },
                { "world_simulation", "World simulation" },
                { "audio", "Audio" },
                { "music", "Music" },
                { "radio", "Radio" },
                { "interaction", "Interaction" },
                { "vehicles", "Vehicles" },
                { "quality_of_life", "Quality of life" },
                { "localization", "Localization" }
            };

        public static string Get(
            string key,
            string locale)
        {
            if (String.IsNullOrWhiteSpace(key))
                return "";

            string value;

            if (String.Equals(
                locale,
                "ru",
                StringComparison.OrdinalIgnoreCase))
            {
                if (Ru.TryGetValue(
                    key,
                    out value))
                {
                    return value;
                }
            }
            else
            {
                if (En.TryGetValue(
                    key,
                    out value))
                {
                    return value;
                }
            }

            return key.Replace(
                "_",
                " ");
        }
    }
}