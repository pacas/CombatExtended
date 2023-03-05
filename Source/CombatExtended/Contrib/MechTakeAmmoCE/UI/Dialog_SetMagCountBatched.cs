using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Verse;
using RimWorld;
using UnityEngine;
using CombatExtended;


namespace CombatExtended
{
    public class Dialog_SetMagCountBatched : Window
    {
        private readonly List<CompMechAmmo> _mechAmmoList;
        private readonly CompMechAmmo _mechAmmoShown;
        private readonly Dictionary<AmmoDef, int> _tmpLoadouts;

        private const float BotAreaWidth = 30f;
        private const float BotAreaHeight = 30f;
        private new const float Margin = 10f;

        public Dialog_SetMagCountBatched(List<CompMechAmmo> mechAmmoList)
        {
            if (mechAmmoList == null || mechAmmoList.Count == 0)
            {
                Log.Error("null or empty CompMechAmmo list for Dialog_SetMagCount");
                return;
            }
            _mechAmmoList = mechAmmoList;
            _mechAmmoShown = mechAmmoList[0];
            // copy the loadouts from the _mechAmmoShown
            _tmpLoadouts = new Dictionary<AmmoDef, int>(_mechAmmoShown.Loadouts);

        }

        public override void PreOpen()
        {
            Vector2 initialSize = this.InitialSize;
            initialSize.y = (_mechAmmoShown.AmmoUser.Props.ammoSet.ammoTypes.Count + 4) * (BotAreaHeight);
            this.windowRect = new Rect(((float)UI.screenWidth - initialSize.x) / 2f, ((float)UI.screenHeight - initialSize.y) / 2f, initialSize.x, initialSize.y);
            this.windowRect = this.windowRect.Rounded();
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(300f, 130f);
            }
        }
        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0;
            string Maglabel = "MTA_MagazinePrefix".Translate(_mechAmmoShown.AmmoUser.Props.magazineSize);

            Vector2 headerRect = Text.CalcSize(Maglabel);
            DrawLabel(inRect, ref headerRect.y, Maglabel);
            foreach (var ammoType in _mechAmmoShown.AmmoUser.Props.ammoSet.ammoTypes)
            {
                int value = 0;
                _tmpLoadouts.TryGetValue(ammoType.ammo, out value);
                string label = ammoType.ammo.ammoClass.labelShort != null ? ammoType.ammo.ammoClass.labelShort : ammoType.ammo.ammoClass.label;
                DrawThingRow(inRect, ref curY, ref value, ammoType.ammo, label);
                _tmpLoadouts.SetOrAdd(ammoType.ammo, value);
            }


            curY += Margin;
            if (Widgets.ButtonText(new Rect(inRect.x, curY, inRect.width, BotAreaHeight), "OK".Translate(), true, true, true, null))
            {
                //set the loadouts for all the comps
                foreach (var compMechAmmo in _mechAmmoList)
                {
                    //copy the loadouts from the _tmpLoadouts
                    foreach (var ammoDef in _tmpLoadouts.Keys)
                    {
                        compMechAmmo.Loadouts.SetOrAdd(ammoDef, _tmpLoadouts[ammoDef]);
                    }
                    compMechAmmo.TakeAmmoNow();
                }
                Close(true);

            }
        }

        public void DrawThingRow(Rect rect, ref float curY, ref int count, Def defForIcon, string label)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DefIcon(new Rect(rect.x, curY, BotAreaWidth, BotAreaHeight), defForIcon);
            Widgets.Label(new Rect(rect.x + BotAreaWidth + Margin, curY + BotAreaHeight / 4, rect.width - BotAreaWidth * 4, BotAreaHeight), label);
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - BotAreaWidth * 4, curY, BotAreaWidth, BotAreaHeight), "-", true, true, true, null))
            {
                count--;
            }
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x + rect.width - BotAreaWidth * 3, curY + BotAreaHeight / 4, BotAreaWidth * 2, BotAreaHeight), count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - BotAreaWidth, curY, BotAreaWidth, BotAreaHeight), "+", true, true, true, null))
            {
                count++;
            }

            count = count < 0 ? 0 : count;

            curY += BotAreaHeight;
        }

        public void DrawLabel(Rect rect, ref float curY, string label)
        {
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x + BotAreaWidth / 2, curY, rect.width - BotAreaWidth, BotAreaHeight), label.ToString());
            curY += BotAreaHeight + Margin;
        }
    }
}


