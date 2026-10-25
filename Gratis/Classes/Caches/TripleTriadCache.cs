using Gratis.Compat.Classes;
using Lumina.Excel.Sheets;

namespace Gratis.Classes.Caches;

public class TripleTriadCache : Cache<uint, bool> {
    protected override bool LoadValue(uint key) => Service.DataManager.GetExcelSheet<TripleTriad>().HasRow(key);
}