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
                    ModHook.OnGameLoadedLate += OnGameLoaded;
                    hooked = true;
                }
            }
            catch (Exception e) { Log.Error("订阅游戏事件失败: " + Log.Describe(e)); }

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
            catch (Exception e) { Log.Error("登记职业 / 人脉升级失败: " + Log.Describe(e)); }

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
                if (Oil.IsHolder(it))
                {
                    var m = Oil.Read(it);
                    b.AddLine(" " + Lang.T("食用油 ", "Cooking oil ") + Oil.Ml(m.Total) + " / " + Oil.Ml(Oil.Capacity(it)) + " · " + Oil.StateName(m), true, RenderHandler.ColorPalette.LightYellow);
                    if (m.Total > 0)
                    {
                        var color = m.Purity >= 0.95 ? RenderHandler.ColorPalette.LightGreen : m.Purity >= 0.85 ? RenderHandler.ColorPalette.LightYellow : RenderHandler.ColorPalette.LightRed;
                        b.AddLine(" " + Lang.T("纯净度 ", "Purity ") + Oil.Percent(m.Purity), true, color);
                        b.AddLine(" " + Lang.T("大颗粒炸渣：", "Large crumbs: ") + Oil.Amount(m.Big) + Lang.T(" · 小颗粒油渣：", " · Fine residue: ") + Oil.Amount(m.Small), true, RenderHandler.ColorPalette.LightGray);
                        if (m.Aged > 0) b.AddLine(" " + Lang.T("老化油（滤不掉）：", "Aged oil (can't be filtered): ") + Oil.Ml(m.Aged), true, RenderHandler.ColorPalette.LightGray);
                    }
                    if (it.identifier == Machines.FryerId)
                    {
                        int n = Oil.Portions(m);
                        b.AddLine(" " + (n > 0 ? Lang.T("这锅油还能炸 " + n + " 份", "Enough oil for " + n + " more portions")
                                               : Lang.T("油不够（至少 " + Oil.Ml(Oil.MinToFry) + " 才炸）", "Not enough oil (needs at least " + Oil.Ml(Oil.MinToFry) + ")")),
                                  true, RenderHandler.ColorPalette.LightGray);
                    }
                }
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
                    ModHook.OnGameLoadedLate -= OnGameLoaded;
                    hooked = false;
                }
            }
            catch { }
            try { ModHelper.RemoveStartingScenarios(ModId); } catch { }
            try { ModHelper.RemoveNetworkUpgrades(ModId); } catch { }
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
    }
}
