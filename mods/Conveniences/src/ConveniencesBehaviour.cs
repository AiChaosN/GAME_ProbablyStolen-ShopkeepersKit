using System;
using UnityEngine;

namespace Conveniences
{
    /// <summary>按键：灌水键（默认 W）。正在打字、在游戏选项里改键时不触发。</summary>
    internal sealed class ConveniencesBehaviour : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                WaterFill.Tick();   // 放下瓶子后补上暂缓的水位贴图更新
                var cfg = ConveniencesMod.Config;
                if (cfg == null || !cfg.WaterFill || IsTyping()) return;
                if (Input.GetKeyDown(cfg.WaterKey)) WaterFill.OnKey();
            }
            catch (Exception e)
            {
                Log.Error("按键处理出错: " + Log.Describe(e));
            }
        }

        private static bool IsTyping()
        {
            try { return StoreUIManager.IsAnyTyping() || KeyBindHelper.SuppressHotkeys; }
            catch { return false; }
        }
    }
}
