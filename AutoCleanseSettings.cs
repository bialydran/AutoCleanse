using System.ComponentModel;
using System.IO;
using Styx;
using Styx.Helpers;
using DefaultValue = Styx.Helpers.DefaultValueAttribute;

namespace AutoCleanse
{
    public enum DruidFormMode
    {
        NeverBreakForm,
        BreakFormOutOfCombat,
        AlwaysBreakForm
    }

    // Per-character settings, saved to Settings\AutoCleanseSettings_<name>.xml.
    public class AutoCleanseSettings : Settings
    {
        private static AutoCleanseSettings _instance;

        public AutoCleanseSettings()
            : base(Path.Combine(Path.Combine(Logging.ApplicationPath, "Settings"),
                string.Format("AutoCleanseSettings_{0}.xml", SafeName)))
        {
        }

        public static AutoCleanseSettings Instance
        {
            get { return _instance ?? (_instance = new AutoCleanseSettings()); }
        }

        private static string SafeName
        {
            get
            {
                try
                {
                    return StyxWoW.Me != null && StyxWoW.Me.IsValid ? StyxWoW.Me.Name : "Default";
                }
                catch
                {
                    return "Default";
                }
            }
        }

        [Setting]
        [DefaultValue(true)]
        [Category("Debuff types")]
        [DisplayName("Remove Magic")]
        [Description("Priest Dispel Magic, Paladin Cleanse, Warlock Felhunter Devour Magic.")]
        public bool RemoveMagic { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Debuff types")]
        [DisplayName("Remove Curse")]
        [Description("Mage and Druid Remove Curse, Shaman Cleanse Spirit.")]
        public bool RemoveCurse { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Debuff types")]
        [DisplayName("Remove Poison")]
        [Description("Druid Abolish/Cure Poison, Paladin Purify/Cleanse, Shaman Cure Toxins/Cleanse Spirit, Dwarf Stoneform.")]
        public bool RemovePoison { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Debuff types")]
        [DisplayName("Remove Disease")]
        [Description("Priest Abolish/Cure Disease, Paladin Purify/Cleanse, Shaman Cure Toxins/Cleanse Spirit, Dwarf Stoneform.")]
        public bool RemoveDisease { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Extras")]
        [DisplayName("Dwarf Stoneform")]
        [Description("Use Stoneform (2 min cooldown) on poison or disease when no class spell can remove it.")]
        public bool UseStoneform { get; set; }

        [Setting]
        [DefaultValue(true)]
        [Category("Extras")]
        [DisplayName("Warlock Felhunter")]
        [Description("Have the Felhunter cast Devour Magic on you to remove a Magic debuff. The pet must be within 30 yards.")]
        public bool UseFelhunter { get; set; }

        [Setting]
        [DefaultValue(DruidFormMode.BreakFormOutOfCombat)]
        [Category("Druid")]
        [DisplayName("Cat/Bear/Travel/Aquatic form")]
        [Description("Remove Curse and Abolish/Cure Poison can't be cast in Cat, Bear, Travel or Aquatic form. Choose whether to leave the form to cure. Caster, Moonkin and Tree of Life form cure without leaving. Flight form is never cancelled.")]
        public DruidFormMode DruidForms { get; set; }

        [Setting]
        [DefaultValue(false)]
        [Category("Extras")]
        [DisplayName("Verbose log")]
        [Description("Log why a cure was skipped or failed.")]
        public bool Verbose { get; set; }
    }
}
