using System;
using SerializeField = UnityEngine.SerializeField;
using System.Collections.Generic;
using System.Linq;
using XRL.UI;
using Newtonsoft.Json;
using ConsoleLib.Console;

namespace XRL.World.Parts
{
    [Serializable]
	class Soron_MerchantMemoryPart : IPart
	{
		[NonSerialized]
        private MerchantInventory LastMerchant;

        [SerializeField]
        private bool _SeenAny = false;

        // serialized with custom method
        private Dictionary<string, MerchantInventory> AllMerchants = new Dictionary<string, MerchantInventory>();

        private class Options {
            public int MinSpend = 0;
            public int MaxSpend = -1;
            public bool OnlyRestocking = false;
            public List<string> ItemCategories = new List<string>();

            public string FormatMaxSpend() {
                if (MaxSpend < 0) {
                    return "none";
                }
                return $"<= {MaxSpend}";
            }

            public string FormatItemCategories() {
                if (ItemCategories.Count == 0) {
                    return "N/A";
                }
                return String.Join(", ", ItemCategories.ToArray());
            }

            public bool Match(ItemMemory item, MerchantInventory merch) {
                if (MinSpend > item.Value / merch.StandardMultiplier) {
                    return false;
                }
                if (MaxSpend >= 0 && MaxSpend < item.Value / merch.StandardMultiplier) {
                    return false;
                }
                if (OnlyRestocking && !merch.MightRestock) {
                    return false;
                }
                if (ItemCategories.Count > 0 && !ItemCategories.Contains(item.Category)) {
                    return false;
                }
                return true;
            }
        }

        public class ItemMemory {
            public string DisplayName;
            public string SearchName;
            public int Weight;
            public double Value;
            public bool IsCurrency;
            public string Category;

            public string FormatWeight() {
                return "{{K|" + Weight + "#}}";
            }
        }

        private class MerchantInventory {
            public string Name;
            public List<ItemMemory> Items = new List<ItemMemory>();
            public string Location;
            public int X;
            public int Y;
            public int Z;
            public int wX;
            public int wY;
            public double StandardMultiplier;
            public long LastBrowsedAt; 
            public bool MightRestock = false;


            public void AddItem(GameObject item) {
                double mult = 1;
                if (!item.IsCurrency) {
                    mult = 1/StandardMultiplier;
                }
                Items.Add(new ItemMemory{
                    DisplayName = item.DisplayName,
                    SearchName = item.DisplayNameStripped.ToLower(),
                    Weight = item.WeightEach,
                    Value = TradeUI.ItemValueEach(item, true),
                    IsCurrency = item.IsCurrency,
                    Category = item.GetInventoryCategory(),
                });
            }
            
            public void DebugMessage() {
                foreach (ItemMemory item in Items) {
                    XRL.Messages.MessageQueue.AddPlayerMessage("saw: " + item.DisplayName);
                }
                XRL.Messages.MessageQueue.AddPlayerMessage(
                    String.Format("from {0}, in zone {1} ({2}, {3})",
                        Name, Location, Direction(), Stratum() 
                    )
                );
            }

            public string Summary(Options options, string search = "") {
                int count = 0;
                search = search.ToLower();
                string message = String.Format("{0} ({1} {2}, {3}, {4})", Name + (MightRestock ? " (restocks)" : ""), Direction(), Location, Stratum(), FormatTime());
                foreach (ItemMemory item in Items) {
                    if (search == "" || item.SearchName.Contains(search)) {
                        if (options.Match(item, this)) {
                            count += 1;
                            double mult = 1;
                            if (!item.IsCurrency) {
                                mult = 1/StandardMultiplier;
                            }
                            message += String.Format("\n - {0} (${1} {2})", item.DisplayName, TradeUI.FormatPrice(item.Value, (float)mult), item.FormatWeight());
                        }
                    }
                }
                if (count == 0) {
                    return "";
                }
                return message;
            }

            public string FormatTime() {
                long delta = Calendar.TotalTimeTicks - LastBrowsedAt;
                long days = (long)(delta / Calendar.turnsPerDay);

                if (days > 1) {
                    return $"{days} days ago";
                } else {
                    int hours = (int)(delta / Calendar.turnsPerHour);
                    return $"{hours} hours ago";
                }
            }

            public string Direction() {
                if (X == 0 && Y == 0) {
                    return "NW";
                }
                if (X == 1 && Y == 0) {
                    return "N";
                }
                if (X == 2 && Y == 0) {
                    return "NE";
                }
                if (X == 0 && Y == 1) {
                    return "W";
                }
                if (X == 1 && Y == 1) {
                    return "C";
                }
                if (X == 2 && Y == 1) {
                    return "E";
                }
                if (X == 0 && Y == 2) {
                    return "SW";
                }
                if (X == 1 && Y == 2) {
                    return "S";
                }
                if (X == 2 && Y == 2) {
                    return "SE";
                }
                return "???";
            }

            public string Stratum() {
                int tmp = Z-10;
                if (tmp == 0) {
                    return "surface";
                } else if (tmp > 0) {
                    return String.Format("{0} strata deep", tmp);
                } else if (tmp < 0) {
                    return String.Format("{0} strata aboveground", tmp);
                } else {
                    return "mathematically impossible stratum";
                }
            }
        }

        public override void SaveData(SerializationWriter Writer) {
            base.SaveData(Writer);

            string version = "version_0.7.0";
            Writer.Write(version);

            Writer.Write(JsonConvert.SerializeObject(AllMerchants, Formatting.Indented));
            Writer.Write(JsonConvert.SerializeObject(options));
            Writer.Write(JsonConvert.SerializeObject(altConfig));
        }
        public override void LoadData(SerializationReader Reader) {
            base.LoadData(Reader);

            string version = Reader.ReadString();
            if (version == "version_0.7.0") {
                string json = Reader.ReadString();

                AllMerchants = JsonConvert.DeserializeObject<Dictionary<string, MerchantInventory>>(json);
                _SeenAny = (AllMerchants.Count > 0);

                json = Reader.ReadString();
                options = JsonConvert.DeserializeObject<Options>(json);

                json = Reader.ReadString();
                altConfig = JsonConvert.DeserializeObject<Dictionary<string, Options>>(json);
            }
        }

        public override bool WantEvent(int ID, int cascade)
        {
            if (!base.WantEvent(ID, cascade))
            {
                return ID == GetTradePerformanceEvent.ID;
            }
            return true;
        }

        public override bool HandleEvent(GetTradePerformanceEvent E)
        {
            GameObject Trader = E.Trader;
            MerchantInventory merch = new MerchantInventory{
                Name = Trader.DisplayName,
                Location = Trader.CurrentZone.DisplayName,
                X = Trader.CurrentZone.X,
                Y = Trader.CurrentZone.Y,
                Z = Trader.CurrentZone.Z,
                wX = Trader.CurrentZone.wX,
                wY = Trader.CurrentZone.wY,
                StandardMultiplier = EventShim_GetFor(E, E.Actor, Trader),
                LastBrowsedAt = Calendar.TotalTimeTicks,
                MightRestock = Trader.HasPart("GenericInventoryRestocker") || Trader.HasPart("Restocker"),
            };
            LastMerchant = merch;
            _SeenAny = true;
            AllMerchants[Trader.id] = merch;
            foreach (GameObject @object in Trader.Inventory.GetObjects())
            {
                // this *should* get the correct set of items (aside from some possible edge cases?)
                if (TradeUI.ValidForTrade(@object, Trader, E.Actor)) {
                    merch.AddItem(@object);
                }
            }
            return base.HandleEvent(E);
        }

        // HACKY IMPLEMENTATION - minimal copy of core game code needed to get accurate numbers *without* crashing the client
        private double EventShim_GetFor(GetTradePerformanceEvent E, GameObject Actor, GameObject Trader) {
            if (Trader == null || Actor == null)
            {
                return 1.0;
            }
            if (!Actor.HasStat("Ego"))
            {
                return 0.25;
            }
            int num = Actor.StatMod("Ego");
            double num2 = 0.0;
            double num3 = 1.0;

            // hacky stuff here
            num2 = E.LinearAdjustment;
            num3 = E.FactorAdjustment;

            return Math.Min(Math.Max((0.35 + 0.07 * ((double)num + num2)) * num3, 0.05), 0.95);
        }

        public Guid SearchMerchantsID = Guid.Empty;
        public Guid DoConfigID = Guid.Empty;

		public void InitAbilities()
		{
            if (SearchMerchantsID == Guid.Empty) {
                SearchMerchantsID = AddMyActivatedAbility("Remember Items", "MerchantMemoryCommand_SearchMerchants", "Memories", "You bring to mind specific items, sold by the merchants that you've seen in your travels.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false);
            }
            if (DoConfigID == Guid.Empty) {
                DoConfigID = AddMyActivatedAbility("Configure Item Search", "MerchantMemoryCommand_ConfigureItemSearch", "Memories", "Adjust how item search works, including item filters.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false); 
            }
		}

		public override bool AllowStaticRegistration()
		{
			return true;
		}

		public override void Register(GameObject Object)
		{
			Object.RegisterPartEvent(this, "MerchantMemoryCommand_SearchMerchants");
            Object.RegisterPartEvent(this, "MerchantMemoryCommand_ConfigureItemSearch");
			base.Register(Object);
		}

		public override bool FireEvent(Event E)
		{
            if (E.ID == "MerchantMemoryCommand_ConfigureItemSearch")
            {
                ShowConfigScreen();
                return false;
            }
            if (E.ID == "MerchantMemoryCommand_SearchMerchants")
            {
                RunItemSearch();
            }
			return base.FireEvent(E);
		}

        public void ShowConfigScreen()
        {
            char hotkey = 'a';
            int OPTION_COUNT = 7;
            List<string> Options = new List<string>(OPTION_COUNT);
            List<char> keymap = new List<char>(OPTION_COUNT);
            List<string> cmds = new List<string>(OPTION_COUNT);

            void AddOption(string text, string cmd) {
                Options.Add(text);
                cmds.Add(cmd);
                keymap.Add(hotkey);
                hotkey++;
            };

            AddOption($"change minimum cost (>= {options.MinSpend})", "min_cost");
            AddOption($"change maximum cost ({options.FormatMaxSpend()})", "max_cost");
            AddOption("filter by item category" + $" ({options.FormatItemCategories()})", "update_item_cat");
            AddOption($"only search restocking merchants ({(options.OnlyRestocking ? "YES" : "no")})", "toggle_only_restock");
            AddOption("save current config", "save_config");
            AddOption("load other config", "load_config");
            AddOption("exit", "exit");

            int choice = Popup.ShowOptionList(
                "Merchant Memory Config Options",
                Options.ToArray(),
                keymap.ToArray()
            );
            //XRL.Messages.MessageQueue.AddPlayerMessage(cmds[choice]);

            if (cmds[choice] == "min_cost") {
                int? response = Popup.AskNumber("change minimum value");
                if (response is int cost) {
                    options.MinSpend = cost;
                }
            } else if (cmds[choice] == "max_cost") {
                // AskNumber doesn't seem to support negatives well, so use 0 as our sentinal value instead
                int? response = Popup.AskNumber("change maximum value (enter 0 to remove maximum)", 0, int.MinValue, int.MaxValue, "-0123456789");
                if (response is int cost) {
                    if (cost == 0) {
                        cost = -1;
                    }
                    options.MaxSpend = cost;
                }
            } else if (cmds[choice] == "update_item_cat") {
                string commaSepList = Popup.AskString("What item categories are you looking for? (comma-separated, case sensitive)");
                if (commaSepList != "") {
                    options.ItemCategories = commaSepList.Split(',').ToList();
                } else {
                    options.ItemCategories = new List<string>();
                }
            } else if (cmds[choice] == "toggle_only_restock") {
                options.OnlyRestocking = !options.OnlyRestocking;
            } else if (cmds[choice] == "save_config") {
                string name = Popup.AskString("what name do you want to save it as?");
                altConfig[name] = CloneOptions(options);
            } else if (cmds[choice] == "load_config") {
                AskLoadOptions();
            }

            if (cmds[choice] != "exit") {
                ShowConfigScreen();
            }
        }

        public void AskLoadOptions() {
            char hotkey = 'a';
            int OPTION_COUNT = altConfig.Count;
            List<string> Options = new List<string>(OPTION_COUNT);
            List<char> keymap = new List<char>(OPTION_COUNT);
            List<string> cmds = new List<string>(OPTION_COUNT);

            void AddOption(string text, string cmd) {
                Options.Add(text);
                cmds.Add(cmd);
                keymap.Add(hotkey);
                hotkey++;
            };

            foreach (string name in altConfig.Keys) {
                AddOption(name, name);
            }

            int choice = Popup.ShowOptionList(
                "Load Merchant Memory Config",
                Options.ToArray(),
                keymap.ToArray()
            );

            options = CloneOptions(altConfig[cmds[choice]]);
        }

        private Options CloneOptions(Options opt) {
            string text = JsonConvert.SerializeObject(opt);
            return JsonConvert.DeserializeObject<Options>(text);
        }



        private Options options = new Options();
        private Dictionary<string, Options> altConfig = new Dictionary<string, Options>();

        public void RunItemSearch()
        {
            if (ParentObject.IsPlayer()) {
                if (_SeenAny) {
                    string search = Popup.AskString("Search for what item?");

                    string message = "merchants who are selling '" + search + "'";
                    foreach (MerchantInventory merch in AllMerchants.Values) {
                        string results = merch.Summary(options, search);
                        if (results != "") {
                            message += "\n" + results;
                        }
                    }
                    Popup.Show(message);
                } else {
                    Popup.Show("You don't remember any merchants or their wares.");
                }
            }
        }
	}
}
