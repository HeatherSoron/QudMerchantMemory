using System;
using System.Collections.Generic;
using System.Linq;
using XRL.UI;


namespace XRL.World.Parts.Skill
{
	class soron_MerchantMemorySkill : BaseSkill
	{
		[NonSerialized]
        private MerchantInventory LastMerchant;

		[NonSerialized]
        private Dictionary<string, MerchantInventory> AllMerchants = new Dictionary<string, MerchantInventory>();


        private class MerchantInventory {
            public string Name;
            public List<ItemMemory> Items = new List<ItemMemory>();
            public string Location;
            public int X;
            public int Y;
            public int Z;

            public class ItemMemory {
                public string DisplayName;
                public string SearchName;
                public int Weight;
                public double Value;
                public double ValueMultiplier;

                public string FormatWeight() {
				    return "{{K|" + Weight + "#}}";
                }
            }

            public void AddItem(GameObject item) {
                Items.Add(new ItemMemory{
                    DisplayName = item.DisplayName,
                    SearchName = item.DisplayNameStripped,
                    Weight = item.WeightEach,
                    Value = TradeUI.ItemValueEach(item, true),
                    // I should probably have calculated this on its own, since TradeUI is kind of a weird pseudo-static class. but, it seems to work, for now?
                    ValueMultiplier = TradeUI.GetMultiplier(item)
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
                string message = String.Format("{0} ({1} {2}, {3})", Name, Direction(), Location, Stratum());
                foreach (ItemMemory item in Items) {
                    if (search == "" || item.SearchName.Contains(search)) {
                        count += 1;
                        message += String.Format("\n - {0} (${1} {2})", item.DisplayName, TradeUI.FormatPrice(item.Value, (float)(1/item.ValueMultiplier)), item.FormatWeight());
                    }
                }
                if (count == 0) {
                    return "";
                }
                return message;
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
            };
            LastMerchant = merch;
            AllMerchants[Trader.id] = merch;
            foreach (GameObject @object in Trader.Inventory.GetObjects())
            {
                    merch.AddItem(@object);
            }
            return base.HandleEvent(E);
        }

		public Guid RememberAbilityID = Guid.Empty;
        public Guid SearchMerchantsID = Guid.Empty;

		public override bool AddSkill(GameObject GO)
		{
			RememberAbilityID = AddMyActivatedAbility("Remember Merchants", "CommandRememberMerchants", "Skill", "You bring to mind the merchants and traders that you've seen in your travels.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false);
			SearchMerchantsID = AddMyActivatedAbility("Remember Items", "CommandSearchMerchants", "Skill", "You bring to mind specific items, sold by the merchants that you've seen in your travels.", "-", null, Toggleable: false, DefaultToggleState: false, ActiveToggle: false, IsAttack: false);
			return base.AddSkill(GO);
		}

		public override bool RemoveSkill(GameObject GO)
		{
			RemoveMyActivatedAbility(ref RememberAbilityID);
			RemoveMyActivatedAbility(ref SearchMerchantsID);
			return base.RemoveSkill(GO);
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
                    if (LastMerchant != null) {
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
                    if (LastMerchant != null) {
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
