using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Account dropdown — same behavior as NightRanger NRAccountConverter.</summary>
    public sealed class UltronAccountConverter : TypeConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var names = new List<string>();
            try
            {
                lock (Account.All)
                {
                    foreach (Account acct in Account.All)
                        names.Add(acct.Name);
                }
            }
            catch (Exception) { /* Account.All not accessible at design time */ }
            return new StandardValuesCollection(names);
        }
    }

    /// <summary>ATM template dropdown — same behavior as NightRanger NRAtmTemplateConverter.</summary>
    public sealed class UltronAtmTemplateConverter : TypeConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => false;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var names = new List<string>();
            try
            {
                string dir = Path.Combine(
                    NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy");
                if (Directory.Exists(dir))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.xml"))
                        names.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch (Exception) { /* ATM template dir may not exist */ }
            names.Sort();
            return new StandardValuesCollection(names);
        }
    }
}
