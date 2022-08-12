using System;
using SerializeField = UnityEngine.SerializeField;
using System.Collections.Generic;
using System.Linq;
using XRL.UI;
using Newtonsoft.Json;


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

            public class ItemMemory {
                public string DisplayName;
                public string SearchName;
                public int Weight;
                public double Value;
                public bool IsCurrency;

                public string FormatWeight() {
				    return "{{K|" + Weight + "#}}";
                }
            }

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

            public string Summary(string search = "") {
                int count = 0;
                search = search.ToLower();
                string message = String.Format("{0} ({1} {2}, {3}, {4})", Name, Direction(), Location, Stratum(), FormatTime());
                foreach (ItemMemory item in Items) {
                    if (search == "" || item.SearchName.Contains(search)) {
                        count += 1;
                        double mult = 1;
                        if (!item.IsCurrency) {
                            mult = 1/StandardMultiplier;
                        }
                        message += String.Format("\n - {0} (${1} {2})", item.DisplayName, TradeUI.FormatPrice(item.Value, (float)mult), item.FormatWeight());
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
            string version = "version_0.4.1";
            Writer.Write(version);

            Writer.Write(JsonConvert.SerializeObject(AllMerchants, Formatting.Indented));

            base.SaveData(Writer);
        }
        public override void LoadData(SerializationReader Reader) {
            string version = Reader.ReadString();
            if (version == "version_0.4.1") {
                string json = Reader.ReadString();

                AllMerchants = JsonConvert.DeserializeObject<Dictionary<string, MerchantInventory>>(json);
                _SeenAny = (AllMerchants.Count > 0);
            }

            base.LoadData(Reader);
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

		public Guid RememberAbilityID = Guid.Empty;
        public Guid SearchMerchantsID = Guid.Empty;

		public void InitAbilities()
		{
            if (RememberAbilityID == Guid.Empty) {
                RememberAbilityID = AddMyActivatedAbility("Remember Merchants", "CommandRememberMerchants", "Memories", "You bring to mind the merchants and traders that you've seen in your travels.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false);
            }
            if (SearchMerchantsID == Guid.Empty) {
                SearchMerchantsID = AddMyActivatedAbility("Remember Items", "CommandSearchMerchants", "Memories", "You bring to mind specific items, sold by the merchants that you've seen in your travels.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false);
            }
		}

		public override bool AllowStaticRegistration()
		{
			return true;
		}

		public override void Register(GameObject Object)
		{
			Object.RegisterPartEvent(this, "CommandRememberMerchants");
			Object.RegisterPartEvent(this, "CommandSearchMerchants");
			base.Register(Object);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "CommandRememberMerchants")
			{
				if (ParentObject.IsPlayer())
				{
                    if (_SeenAny) {
                        //LastMerchant.DebugMessage();
                        //Popup.Show(LastMerchant.Summary());
                        string message = "known merchants:\n";
                        foreach (MerchantInventory merch in AllMerchants.Values) {
                            message += "\n" + merch.Summary();
                        }
                        Popup.Show(message);
                    } else {
                        Popup.Show("You don't remember any merchants or their wares.");
                    }
				}
                return false;
			}
            if (E.ID == "CommandSearchMerchants")
            {
				if (ParentObject.IsPlayer()) {
                    if (_SeenAny) {
                        string search = Popup.AskString("Search for what item?");

                        string message = "merchants who are selling '" + search + "'";
                        foreach (MerchantInventory merch in AllMerchants.Values) {
                            string results = merch.Summary(search);
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
			return base.FireEvent(E);
		}
	}
}
