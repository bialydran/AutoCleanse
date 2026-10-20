using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Styx;
using Styx.Combat.CombatRoutine;
using Styx.Helpers;
using Styx.Logic.Combat;
using Styx.Plugins.PluginClass;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace AutoCleanse
{
    // Removes Magic, Curse, Poison and Disease debuffs from your own character with whatever
    // cure spell your class and level know (3.3.5a), plus Dwarf Stoneform and the Warlock Felhunter.
    public class AutoCleanse : HBPlugin
    {
        public override string Name { get { return "AutoCleanse"; } }
        public override string Author { get { return "bialydran"; } }
        public override Version Version { get { return new Version(1, 0); } }
        public override bool WantButton { get { return true; } }
        public override string ButtonText { get { return "Settings"; } }

        private static LocalPlayer Me { get { return StyxWoW.Me; } }
        private static AutoCleanseSettings S { get { return AutoCleanseSettings.Instance; } }

        // A self-cure spell. Ranks are listed highest first; HotAura is the buff a cure-over-time leaves on you.
        private sealed class Cure
        {
            public string Name;
            public int[] Ranks;
            public WoWDispelType[] Removes;
            public string HotAura;
            public bool DruidSpell;
            public bool Racial;
        }

        private static readonly WoWDispelType M = WoWDispelType.Magic;
        private static readonly WoWDispelType C = WoWDispelType.Curse;
        private static readonly WoWDispelType P = WoWDispelType.Poison;
        private static readonly WoWDispelType D = WoWDispelType.Disease;

        // Spell IDs and learn levels from the AzerothCore 3.3.5a trainer tables. Order is priority:
        // a player only knows their own class's entries, and the wider cure comes first.
        private static readonly Cure[] Cures =
        {
            new Cure { Name = "Cleanse", Ranks = new[] { 4987 }, Removes = new[] { M, P, D } },                       // Paladin 42
            new Cure { Name = "Purify", Ranks = new[] { 1152 }, Removes = new[] { P, D } },                           // Paladin 8
            new Cure { Name = "Dispel Magic", Ranks = new[] { 988, 527 }, Removes = new[] { M } },                    // Priest 36 / 18
            new Cure { Name = "Abolish Disease", Ranks = new[] { 552 }, Removes = new[] { D }, HotAura = "Abolish Disease" }, // Priest 32
            new Cure { Name = "Cure Disease", Ranks = new[] { 528 }, Removes = new[] { D } },                         // Priest 14
            new Cure { Name = "Remove Curse", Ranks = new[] { 2782 }, Removes = new[] { C }, DruidSpell = true },     // Druid 24
            new Cure { Name = "Abolish Poison", Ranks = new[] { 2893 }, Removes = new[] { P }, HotAura = "Abolish Poison", DruidSpell = true }, // Druid 26
            new Cure { Name = "Cure Poison", Ranks = new[] { 8946 }, Removes = new[] { P }, DruidSpell = true },      // Druid 14 (quest)
            new Cure { Name = "Cleanse Spirit", Ranks = new[] { 51886 }, Removes = new[] { P, D, C } },               // Shaman talent
            new Cure { Name = "Cure Toxins", Ranks = new[] { 526 }, Removes = new[] { P, D } },                       // Shaman 16
            new Cure { Name = "Remove Curse", Ranks = new[] { 475 }, Removes = new[] { C } },                         // Mage 18
            new Cure { Name = "Stoneform", Ranks = new[] { 20594 }, Removes = new[] { P, D }, Racial = true },        // Dwarf
        };

        // Dispelling these hurts the dispeller (Unstable Affliction: 990 damage + 5 s silence).
        private static readonly HashSet<string> NeverDispel = new HashSet<string> { "Unstable Affliction" };

        private static readonly ShapeshiftForm[] NoCastDruidForms =
        {
            ShapeshiftForm.Cat, ShapeshiftForm.Bear, ShapeshiftForm.DireBear, ShapeshiftForm.Travel, ShapeshiftForm.Aqua
        };

        private HashSet<int> _known = new HashSet<int>();
        private DateTime _nextKnownRefresh = DateTime.MinValue;
        private DateTime _nextPulse = DateTime.MinValue;
        private readonly Dictionary<string, DateTime> _backoff = new Dictionary<string, DateTime>();

        public override void Initialize()
        {
            Log("Loaded.");
        }

        public override void OnButtonPress()
        {
            using (var form = new Form
            {
                Text = "AutoCleanse",
                Size = new Size(460, 520),
                StartPosition = FormStartPosition.CenterScreen
            })
            {
                var grid = new PropertyGrid
                {
                    Dock = DockStyle.Fill,
                    SelectedObject = S,
                    PropertySort = PropertySort.Categorized,
                    HelpVisible = true
                };
                grid.PropertyValueChanged += (s, e) => S.Save();
                form.Controls.Add(grid);
                form.ShowDialog();
                S.Save();
            }
        }

        public override void Pulse()
        {
            if (DateTime.UtcNow < _nextPulse)
                return;
            _nextPulse = DateTime.UtcNow.AddMilliseconds(300);

            var me = Me;
            if (me == null || !me.IsValid || me.Dead || me.IsGhost || me.OnTaxi || me.Mounted ||
                me.Stunned || me.Silenced || me.IsCasting || me.IsChanneling || me.IsStealthed)
                return;

            RefreshKnownSpells(me);

            var wanted = WantedTypes(me);
            if (wanted.Count == 0)
                return;

            var classCovered = new HashSet<WoWDispelType>(
                Cures.Where(c => !c.Racial && KnownRank(c) != 0).SelectMany(c => c.Removes));

            foreach (var cure in Cures)
            {
                var types = cure.Removes.Where(wanted.Contains).ToList();
                if (types.Count == 0)
                    continue;
                if (cure.Racial && (!S.UseStoneform || types.All(classCovered.Contains)))
                    continue;

                int id = KnownRank(cure);
                if (id == 0 || InBackoff(cure.Name))
                    continue;

                if (TryCast(me, cure, id, types))
                    return;
            }

            if (wanted.Contains(WoWDispelType.Magic) && S.UseFelhunter && me.Class == WoWClass.Warlock)
                TryDevourMagic(me);
        }

        // Debuff types on you that are switched on and not already being cured by an Abolish over time.
        private static HashSet<WoWDispelType> WantedTypes(LocalPlayer me)
        {
            var wanted = new HashSet<WoWDispelType>();
            foreach (var aura in me.GetAllAuras())
            {
                if (!aura.IsHarmful || aura.Spell == null || NeverDispel.Contains(aura.Name))
                    continue;
                var type = aura.Spell.DispelType;
                if ((type == WoWDispelType.Magic && S.RemoveMagic) ||
                    (type == WoWDispelType.Curse && S.RemoveCurse) ||
                    (type == WoWDispelType.Poison && S.RemovePoison) ||
                    (type == WoWDispelType.Disease && S.RemoveDisease))
                    wanted.Add(type);
            }

            foreach (var cure in Cures.Where(c => c.HotAura != null && me.HasAura(c.HotAura)))
                wanted.ExceptWith(cure.Removes);

            return wanted;
        }

        private bool TryCast(LocalPlayer me, Cure cure, int id, List<WoWDispelType> types)
        {
            if (cure.DruidSpell && !DruidFormAllowsCast(me))
                return false;
            if (me.Shapeshift == ShapeshiftForm.GhostWolf)
                return false;

            var spell = WoWSpell.FromId(id);
            if (spell == null || !SpellManager.CanCast(spell, me, false))
                return false;

            SpellManager.Cast(spell, me);

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 700)
            {
                if (spell.Cooldown || SpellManager.GlobalCooldown)
                {
                    Log("{0} on me ({1}).", cure.Name, string.Join(", ", types));
                    return true;
                }
                Thread.Sleep(50);
            }

            // No GCD started: wrong form, nothing dispellable, out of mana... Don't spam it.
            _backoff[cure.Name] = DateTime.UtcNow.AddSeconds(10);
            Verbose("{0} did not go off; retrying in 10 s.", cure.Name);
            return false;
        }

        // True when the druid can cast in the current form. May cancel the form, then the cast happens next pulse.
        private static bool DruidFormAllowsCast(LocalPlayer me)
        {
            var form = me.Shapeshift;
            if (form == ShapeshiftForm.FlightForm || form == ShapeshiftForm.EpicFlightForm)
                return false;
            if (!NoCastDruidForms.Contains(form))
                return true;

            var mode = S.DruidForms;
            if (mode == DruidFormMode.NeverBreakForm || (mode == DruidFormMode.BreakFormOutOfCombat && me.Combat))
                return false;

            Log("Leaving {0} form to cure.", form);
            Lua.DoString("CancelShapeshiftForm()");
            return false;
        }

        private void TryDevourMagic(LocalPlayer me)
        {
            var pet = me.Pet;
            if (!me.GotAlivePet || pet == null || pet.Distance > 30 || InBackoff("Devour Magic"))
                return;

            int slot = Lua.GetReturnVal<int>(
                "for i=1,10 do if GetPetActionInfo(i) == \"Devour Magic\" then " +
                "if GetPetActionCooldown(i) == 0 then return i end return 0 end end return 0", 0);
            if (slot == 0)
                return;

            Lua.DoString("CastPetAction({0}, \"player\")", slot);
            Thread.Sleep(500);

            if (Lua.GetReturnVal<double>(string.Format("return GetPetActionCooldown({0})", slot), 0) > 0)
            {
                Log("Felhunter Devour Magic on me (Magic).");
                return;
            }

            _backoff["Devour Magic"] = DateTime.UtcNow.AddSeconds(10);
            Verbose("Devour Magic did not go off; retrying in 10 s.");
        }

        private void RefreshKnownSpells(LocalPlayer me)
        {
            if (DateTime.UtcNow < _nextKnownRefresh)
                return;
            _nextKnownRefresh = DateTime.UtcNow.AddSeconds(10);
            _known = new HashSet<int>(me.KnownSpells.Select(s => s.Id));
        }

        // Highest rank of the cure you have learned, or 0.
        private int KnownRank(Cure cure)
        {
            return cure.Ranks.FirstOrDefault(_known.Contains);
        }

        private bool InBackoff(string key)
        {
            DateTime until;
            return _backoff.TryGetValue(key, out until) && DateTime.UtcNow < until;
        }

        private static void Log(string format, params object[] args)
        {
            Logging.Write(Color.MediumSeaGreen, "[AutoCleanse] " + format, args);
        }

        private static void Verbose(string format, params object[] args)
        {
            if (S.Verbose)
                Log(format, args);
        }
    }
}
