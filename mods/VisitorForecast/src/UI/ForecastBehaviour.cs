using System;
using UnityEngine;
using VisitorForecast.Forecast;

namespace VisitorForecast.UI
{
    /// <summary>
    /// 热键 + 运行调度 + 选择显示方式。计算放在 Update 里做（OnGUI 一帧会被调用多次）。
    /// 显示优先用游戏原生的平板窗口（NativeForecastWindow）；它不可用（主菜单等）或出错时退回 IMGUI 窗口。
    /// </summary>
    internal sealed class ForecastBehaviour : MonoBehaviour
    {
        internal static ForecastBehaviour Instance;
        internal ForecastEngine Engine = new ForecastEngine();

        private bool visible;
        private int runAtFrame = -1;
        private bool busyPending;   // 待执行的这次运行要先显示“正在预测…”
        private ForecastResult result;
        private PlayerStore.StoreState? resultState;
        private float nextQueueRefresh;
        private bool dirty;

        private readonly ImguiForecastWindow imgui = new ImguiForecastWindow();
        private NativeForecastWindow native;
        private bool nativeFailed;   // 出过异常：本次读档内不再尝试原生窗口
        private bool usingNative;
        private string lastUnavailable;

        internal bool Visible => visible;
        internal bool Busy => runAtFrame >= 0 && busyPending;
        internal ForecastResult Result => result;
        internal int Runs => VisitorForecastMod.Config?.Runs ?? 300;
        internal string HotkeyLabel => (VisitorForecastMod.Config?.Hotkey ?? KeyCode.F9).ToString();

        private void Awake() => Instance = this;

        internal void OnGameLoaded()
        {
            Engine.OnGameLoaded();
            result = null;
            visible = false;
            runAtFrame = -1;
            nativeFailed = false;
            usingNative = false;
            try { native?.Hide(); } catch { }
        }

        private void Update()
        {
            try
            {
                // 原生窗口被游戏关掉了（Esc、换场景）：同步状态
                if (visible && usingNative && (native == null || !native.IsShown)) visible = false;

                var cfg = VisitorForecastMod.Config;
                if (cfg != null && Input.GetKeyDown(cfg.Hotkey) && !IsTyping())
                {
                    if (visible) Hide();
                    else
                    {
                        visible = true;
                        RequestRun(true);
                    }
                }
                if (visible && runAtFrame < 0 && result != null)
                {
                    // 开门/打烊后，旧的预测已经过时：自动换成实际队列；开门期间每秒刷新一次实际队列（不跑沙盒）
                    var ps = Game.PlayerStoreOrNull;
                    var state = ps?.storeState;
                    if (state != resultState) RequestRun(true);
                    else if (result.Kind == ForecastResult.Mode.ActualQueue && Time.unscaledTime >= nextQueueRefresh) RequestRun(false);
                }
                if (visible && dirty)
                {
                    // 先把“正在预测…”画出来，沙盒至少推迟到下一帧再跑（按钮点击可能发生在本帧 Update 之后）
                    Render();
                    if (busyPending && runAtFrame >= 0 && runAtFrame <= Time.frameCount) runAtFrame = Time.frameCount + 1;
                }
                if (visible && runAtFrame >= 0 && Time.frameCount >= runAtFrame)
                {
                    runAtFrame = -1;
                    busyPending = false;
                    resultState = Game.PlayerStoreOrNull?.storeState;
                    result = Engine.Run(cfg ?? new ForecastConfig());
                    nextQueueRefresh = Time.unscaledTime + 1f;
                    dirty = true;
                }
                if (visible && dirty) Render();
            }
            catch (Exception e)
            {
                runAtFrame = -1;
                busyPending = false;
                Log.Error("Update 出错: " + Log.Describe(e));
            }
        }

        private static bool IsTyping()
        {
            try { return StoreUIManager.IsAnyTyping(); }
            catch { return false; }
        }

        internal void RequestRun(bool showBusy)
        {
            // 下一帧再算，让“正在预测…”先画出来
            runAtFrame = Time.frameCount + 1;
            if (showBusy)
            {
                busyPending = true;
                dirty = true;
            }
        }

        internal void Hide()
        {
            visible = false;
            runAtFrame = -1;
            busyPending = false;
            try { native?.Hide(); }
            catch (Exception e) { Log.Warn("隐藏窗口出错: " + e.Message); }
        }

        private void Render()
        {
            dirty = false;
            var cfg = VisitorForecastMod.Config;
            if ((cfg == null || cfg.NativeUI) && !nativeFailed)
            {
                // 整段都包在 try 里：游戏更新后 TabletUI 等类型/成员变了时，JIT 或类型初始化也会在这里抛异常
                try
                {
                    if (native == null) native = new NativeForecastWindow(this);
                    if (native.Available(out string why))
                    {
                        native.Render();
                        usingNative = true;
                        lastUnavailable = null;
                        return;
                    }
                    if (why != lastUnavailable)
                    {
                        lastUnavailable = why;
                        Log.Info("原生窗口暂不可用（" + why + "），使用简易窗口。");
                    }
                }
                catch (Exception e)
                {
                    nativeFailed = true;
                    Log.Error("原生窗口出错，改用简易窗口（重新读档后会再试）: " + Log.Describe(e));
                    try { native?.Destroy(); } catch { }
                }
            }
            else
            {
                try { native?.Hide(); } catch { }
            }
            usingNative = false;
        }

        /// <summary>给 StoreUIManager.CheckRaycast 的补丁用：鼠标是否在本 mod 的窗口上。</summary>
        internal bool IsPointerOverWindow(Vector2 screenPos)
        {
            if (!visible) return false;
            if (usingNative) return native != null && native.ContainsScreenPoint(screenPos);
            return imgui.ContainsScreenPoint(screenPos);
        }

        private void OnGUI()
        {
            if (!visible || usingNative) return;
            imgui.OnGUI(this);
        }

        internal void OnImguiError(Exception e)
        {
            visible = false;
            Log.Error("界面绘制出错，窗口已关闭: " + Log.Describe(e));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            try { native?.Destroy(); } catch { }
            imgui.Dispose();
        }
    }
}
