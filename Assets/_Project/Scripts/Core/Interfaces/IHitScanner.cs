using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IHitScanner
    {
        HitScanResult Scan(HitScanRequest request);
    }
}
