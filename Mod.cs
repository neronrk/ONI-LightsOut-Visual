using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using KMod;

namespace NewLightsOut
{
    public static class Config
    {
        public static bool Enabled = true;
        public static float DarknessBase = 0.15f;
        public static float EquipLightBonus = 6.0f;
    }

    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            try
            {
                Debug.Log("[NewLightsOut] Загрузка...");
                LoadConfig();

                // БЕЗОПАСНОЕ НАНЕСЕНИЕ ПАТЧЕЙ
                SafePatch(harmony, typeof(Grid), "GetCellLux", typeof(LuxPatch));
                SafePatch(harmony, typeof(EquipmentDef), "GetLightRadius", typeof(EquipPatch));
                SafePatch(harmony, typeof(PropertyTextures), "UpdateFogOfWar", typeof(FogPatch));

                Debug.Log("[NewLightsOut] Инициализация завершена.");
            }
            catch (Exception e)
            {
                Debug.LogError("[NewLightsOut] КРИТИЧЕСКАЯ ОШИБКА: " + e);
            }
            base.OnLoad(harmony);
        }

        // Вспомогательный метод для безопасного патчинга
        private void SafePatch(Harmony harmony, Type targetType, string methodName, Type patchType)
        {
            try
            {
                var original = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (original == null)
                {
                    Debug.LogWarning("[NewLightsOut] Метод " + targetType.Name + "." + methodName + " не найден в этой версии игры. Патч пропущен.");
                    return;
                }

                var prefix = patchType.GetMethod("Prefix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var postfix = patchType.GetMethod("Postfix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(original,
                    prefix != null ? new HarmonyMethod(prefix) : null,
                    postfix != null ? new HarmonyMethod(postfix) : null);

                Debug.Log("[NewLightsOut] Успешно запатчен: " + targetType.Name + "." + methodName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NewLightsOut] Ошибка патча " + targetType.Name + "." + methodName + ": " + e.Message);
            }
        }

        private static void LoadConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Config.json");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{ \"Enabled\": true, \"DarknessBase\": 0.15, \"EquipLightBonus\": 6.0 }");
                return;
            }
            var text = File.ReadAllText(path);
            Config.Enabled = text.Contains("\"Enabled\": true");
            Config.DarknessBase = ParseFloat(text, "DarknessBase", 0.15f);
            Config.EquipLightBonus = ParseFloat(text, "EquipLightBonus", 6.0f);
        }

        private static float ParseFloat(string s, string key, float def)
        {
            int i = s.IndexOf(key); if (i < 0) return def;
            int c = s.IndexOf(':', i); if (c < 0) return def;
            int n = s.IndexOfAny("0123456789-.".ToCharArray(), c); if (n < 0) return def;
            int e = s.IndexOfAny(",}\r\n".ToCharArray(), n + 1); if (e < 0) e = s.Length;
            float v; return float.TryParse(s.Substring(n, e - n), out v) ? v : def;
        }
    }

    // Патч визуальной яркости
    public static class LuxPatch
    {
        public static void Postfix(int cell, ref float __result)
        {
            if (!Config.Enabled || cell < 0 || !Grid.IsValidCell(cell)) return;
            __result = Mathf.Max(__result, __result * (1f - Config.DarknessBase));
        }
    }

    // Патч света от экипировки
    public static class EquipPatch
    {
        public static void Postfix(EquipmentDef __instance, ref float __result)
        {
            if (!Config.Enabled || __instance == null) return;
            var id = __instance.Id;
            if (id != null && (id.Contains("Suit") || id.Contains("Helmet") || id.Contains("Mask")))
                __result += Config.EquipLightBonus;
        }
    }

    // Патч тумана/освещения (без аллокаций = без фризов)
    public static class FogPatch
    {
        private static readonly int[] DX = { -1, 0, 1, 1, 1, 0, -1, -1 };
        private static readonly int[] DY = { -1, -1, -1, 0, 1, 1, 1, 0 };

        public static bool Prefix(PropertyTextures __instance, TextureRegion region, int x0, int y0, int x1, int y1)
        {
            if (!Config.Enabled) return true;

            try
            {
                byte[] visible = Grid.Visible;
                int lowest = 60;
                int highest = 255;
                float threshold = 1800f;

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int cell = Grid.XYToCell(x, y);
                        if (visible[cell] == 0)
                        {
                            region.SetBytes(x, y, 0);
                            continue;
                        }

                        int lux = Grid.LightIntensity[cell];
                        if (lux == 0)
                        {
                            int sum = 0, count = 0;
                            for (int i = 0; i < 8; i++)
                            {
                                int nx = x + DX[i], ny = y + DY[i];
                                int nCell = Grid.XYToCell(nx, ny);
                                if (Grid.IsValidCell(nCell))
                                {
                                    sum += Grid.LightIntensity[nCell];
                                    count++;
                                }
                            }
                            lux = count > 0 ? sum / count : 0;
                        }

                        float factor = Mathf.Clamp01(lux / threshold);
                        byte fog = (byte)Mathf.Lerp(lowest, highest, factor);
                        region.SetBytes(x, y, fog);
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NewLightsOut] FogPatch ошибка: " + e.Message);
                return true;
            }
        }
    }
}