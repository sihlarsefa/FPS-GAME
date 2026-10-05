using Project.Core.Domain;

namespace Project.Core.Interfaces
{
    public interface IArmorProvider
    {
        /// <summary>Vücut bölgesini koruyan zırh (kafa → kask, gövde → yelek). Yoksa null.</summary>
        ArmorPiece GetArmorFor(BodyPart part);
    }
}
