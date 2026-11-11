using System;
using System.Collections.Generic;

namespace Chef
{
    /// <summary>
    /// 入口。只用游戏的官方接口：ModHelper.AddStartingScenario（职业）、ModHelper.AddNetworkUpgrade（王尔德的“退休大厨”）、
    /// ModHook.OnModItemDirectoryInit（新物品）、ModHook.OnCreateTooltipLate（鼠标指着时的说明）、ModHook.OnGameLoadedLate（读档）、
    /// 纸质书（mod 文件夹 Books/chef_recipes.json，游戏自己读；菜谱物品点开时 ModHelper.OpenPaper）。
    /// </summary>
    public sealed class ChefMod : IMod
    {
        internal static string ModId = "chaos5090.chef";
        private static bool hooked;
        private static UnityEngine.GameObject host;

        public void Init(ModManifest manifest)
        {
            ModId = manifest?.ID ?? ModId;
            try
            {
                if (!hooked)
                {
                    ModHook.OnModItemDirectoryInit += RegisterItems;
                    ModHook.OnCreateTooltipLate += OnTooltip;
                    ModHook.OnGameLoadedInit += Scenario.EnsureUpgrade;
                    ModHook.OnNewGameStarted += OnNewGame;
                    ModHook.OnGameLoadedLate += OnGameLoaded;
                    hooked = true;
                }
            }
            catch (Exception e) { Log.Error("订阅游戏事件失败: " + Log.Describe(e)); }

            try { Oil.Register(); }
            catch (Exception e) { Log.Error("登记食用油液体失败: " + Log.Describe(e)); }
            try { Patches.Apply(); }
            catch (Exception e) { Log.Error("油的显示 / 定价补丁失败: " + Log.Describe(e)); }

            try
            {
                var dir = UnityEngine.Object.FindObjectOfType<ModItemDirectory>();
                if (dir != null && dir.factoryDictionary != null) RegisterItems(dir);
            }
            catch (Exception e) { Log.Warn("查找物品目录失败（等游戏建好后再登记）: " + e.Message); }

            try
            {
                if (host == null)
                {
                    host = new UnityEngine.GameObject("Chef");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    host.AddComponent<ChefBehaviour>();   // 机器窗口标题栏上的按钮
                }
            }
            catch (Exception e) { Log.Error("创建按钮对象失败: " + Log.Describe(e)); }

            try { Scenario.Register(ModId); }
            catch (Exception e) { Log.Error("登记职业失败: " + Log.Describe(e)); }
            Scenario.EnsureUpgrade();   // 存档已经开着（开发者模式重新加载 mod）时直接登记

            Log.Info("已加载：职业“厨师”、王尔德人脉“退休大厨”、油锅、食品加工器、食用油、四道菜、厨师菜谱");
        }

        private static void RegisterItems(ModItemDirectory dir)
        {
            try
            {
                if (dir == null) return;
                var items = new (string id, Func<GameItem> make, string name)[]
                {
                    (Machines.FryerId, Machines.CreateFryer, "油锅"),
                    (Machines.ProcessorId, Machines.CreateProcessor, "食品加工器"),
                    (Items.BarrelId, Items.CreateBarrel, "食用油桶"),
                    (Items.StrainerId, Items.CreateStrainer, "滤油网"),
                    (Recipe.ItemId, Recipe.CreateItem, "厨师菜谱"),
                    (Food.FriesId, () => Food.CreateBase(Dish.Fries), "薯条"),
                    (Food.ChickenId, () => Food.CreateBase(Dish.Chicken), "炸鸡"),
                    (Food.BurgerId, () => Food.CreateBase(Dish.Burger), "肉汉堡"),
                    (Food.ComboId, () => Food.CreateBase(Dish.Combo), "超级大套餐"),
                };
                var done = new List<string>();
                foreach (var (id, make, name) in items)
                {
                    if (dir.factoryDictionary != null && dir.factoryDictionary.ContainsKey(id)) continue;
                    done.Add(name + (dir.Add(id, make) ? "" : "（失败）"));
                }
                if (done.Count > 0) Log.Info("登记新物品：" + string.Join("、", done));
            }
            catch (Exception e) { Log.Error("登记新物品出错: " + Log.Describe(e)); }
        }

        private static void OnNewGame(string _) => Scenario.EnsureUpgrade();

        private static int tooltipErrors;

        private static void OnTooltip(RichTextBuilder b, GameItem it)
        {
            if (b == null || it == null || tooltipErrors > 20) return;
            try
            {
                if (Food.IsDish(it))
                {
                    Food.Tooltip(b, it);
                    return;
                }
                if (it.identifier == Machines.FryerId)
                {
                    var c = Machines.FryerContainer(it);
                    if (c == null)
                    {
                        b.AddLine(" " + Lang.T("“油桶”槽里没放油", "No oil in the \"Oil\" slot"), true, RenderHandler.ColorPalette.LightRed);
                        return;
                    }
                    var m = Oil.Read(c);
                    Oil.Tooltip(b, m, isFryer: true);
                    string why = Oil.CannotFry(m);
                    b.AddLine(" " + (why ?? Lang.T("这桶油还能炸 " + Oil.Portions(m) + " 份", "Enough oil for " + Oil.Portions(m) + " more portions")),
                              true, why == null ? RenderHandler.ColorPalette.LightGray : RenderHandler.ColorPalette.LightRed);
                    return;
                }
                if (it.IsTag("LIQUID_CONTAINER_TAG")) Oil.Tooltip(b, Oil.Read(it), isFryer: false);
            }
            catch (Exception e)
            {
                if (tooltipErrors++ == 0) Log.Warn("说明文字出错: " + Log.Describe(e));
            }
        }

        /// <summary>
        /// 读档：存档会连物品的名字和说明一起存下来（SaveManager 照存档恢复 name / shortDescription），
        /// 所以改版或换语言后，已有物品还是旧文字。这里按当前版本和语言重写本 mod 物品的名字和说明。
        /// </summary>
        private static void OnGameLoaded()
        {
            try { Oil.Register(); } catch { }   // 刷新液体的显示名字（跟随当前语言）
            try
            {
                var store = Game.Store;
                if (store == null) return;
                int n = 0;
                foreach (var it in store.FindAllItem(false))
                {
                    if (it == null) continue;
                    switch (it.identifier)
                    {
                        case Machines.FryerId: it.name = Machines.FryerName(); it.shortDescription = Machines.FryerDesc(); n++; break;
                        case Machines.ProcessorId: it.name = Machines.ProcessorName(); it.shortDescription = Machines.ProcessorDesc(); n++; break;
                        case Items.BarrelId: it.name = Items.BarrelName(); it.shortDescription = Items.BarrelDesc(); n++; break;
                        case Items.StrainerId: it.name = Items.StrainerName(); it.shortDescription = Items.StrainerDesc(); n++; break;
                        case Recipe.ItemId: it.name = Recipe.Name(); it.shortDescription = Recipe.Desc(); n++; break;
                        default:
                            if (Food.IsDish(it)) { Food.Apply(it); n++; }
                            break;
                    }
                }
                if (n > 0) Log.Info("刷新了 " + n + " 件本 mod 物品的名字和说明");
            }
            catch (Exception e) { Log.Warn("刷新物品说明出错: " + e.Message); }
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            try
            {
                if (hooked)
                {
                    ModHook.OnModItemDirectoryInit -= RegisterItems;
                    ModHook.OnCreateTooltipLate -= OnTooltip;
                    ModHook.OnGameLoadedInit -= Scenario.EnsureUpgrade;
                    ModHook.OnNewGameStarted -= OnNewGame;
                    ModHook.OnGameLoadedLate -= OnGameLoaded;
                    hooked = false;
                }
            }
            catch { }
            try { ModHelper.RemoveStartingScenarios(ModId); } catch { }
            try { ModHelper.RemoveNetworkUpgrades(ModId); } catch { }
            Patches.Remove();
            try
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                host = null;
            }
            catch { }
        }
    }

    /// <summary>厨师菜谱：自己的物品，和游戏的手册一样打开（onActivateSlotItemFunc）时打开纸质书 Books/chef_recipes.json。</summary>
    internal static class Recipe
    {
        internal const string ItemId = "chaos5090_chef_recipe_book";
        internal const string BookId = "chef_recipes";

        internal static string Name() => Lang.T("厨师菜谱", "Chef's Recipe Book");

        internal static string Desc() => Lang.T("四道菜的做法、品质怎么算、食用油怎么过滤。打开就能看。", "How to make the four dishes, how quality works and how to filter cooking oil. Open it to read.");

        internal static GameItem CreateItem()
        {
            var it = ItemDirectory.CreateEmptyItem().SetSpriteAndShape("Items/items_misc", "manual1");
            it.name = Name();
            it.shortDescription = Desc();
            it.EnableTag("DOCUMENT");
            it.SetGameItemType("DOCUMENT");
            it.EnableTag("IMPORTANT_TAG");
            it.EnableTag("GUIDE_PAPER_TAG");
            it.unitValue = 30L;
            it.canActivateSlotItemFunc = (_, __, ___) => CanRead();
            it.mayActivateSlotItemFunc = (_, __, ___) => CanRead();
            it.onActivateSlotItemFunc = (_, __, ___) =>
            {
                try
                {
                    EnsurePaperSprites();
                    if (ModHelper.OpenPaper(ChefMod.ModId, BookId) == null)
                        Ui.Notify(Lang.T("菜谱打不开（书没读到，详见日志）", "Couldn't open the recipe book (book not loaded; see the log)"));
                }
                catch (Exception e) { Log.Error("打开菜谱出错: " + Log.Describe(e)); }
            };
            return it;
        }

        private static bool CanRead()
        {
            try { return HealthData.CanRead(); }
            catch { return true; }
        }

        internal static GameItem Spawn() => DirectoryMaster.Item(ItemId);

        /// <summary>
        /// 书的底纸和按钮用游戏自己的贴图（和鉴定指南一样）。但游戏发行版里没有纸质书的贴图目录（日志：
        /// “No paper asset catalog … Vanilla sprite names will not resolve”），按名字找不到，按钮就画成蓝框、底纸也没有。
        /// 这些贴图本身在游戏资源里（sharedassets1），进了游戏就已经加载：按名字从已加载的贴图里找出来，
        /// 登记到本 mod 的书用贴图表（LoadedMod.PaperSprites），PaperUI 找不到游戏贴图时会来这里找。不复制游戏的美术文件。
        /// </summary>
        private static readonly string[] PaperSpriteNames =
        {
            "notebook_manual", "notebook_manual_section", "paper_buttons_close1", "paper_buttons_screen_0", "paper_buttons_l_1", "paper_buttons1",
        };

        internal static void EnsurePaperSprites()
        {
            try
            {
                var mod = ModLoader.Mods.Find(m => m.Manifest != null && m.Manifest.ID == ChefMod.ModId);
                if (mod == null) return;
                var missing = new HashSet<string>(PaperSpriteNames);
                missing.RemoveWhere(n => mod.PaperSprites.ContainsKey(n));
                if (missing.Count == 0) return;
                foreach (var sp in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Sprite>())
                {
                    if (sp == null || !missing.Contains(sp.name)) continue;
                    mod.PaperSprites[sp.name] = sp;
                    missing.Remove(sp.name);
                    if (missing.Count == 0) break;
                }
                if (missing.Count > 0) Log.Warn("书用的游戏贴图没找到：" + string.Join("、", missing));
                else Log.Info("书用的游戏贴图已就位");
            }
            catch (Exception e) { Log.Warn("准备书用贴图出错: " + e.Message); }
        }
    }
}
