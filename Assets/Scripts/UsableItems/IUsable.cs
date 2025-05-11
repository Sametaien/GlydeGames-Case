using Fusion;

namespace UsableItems
{
    public interface IUsable
    {
        void Use(NetworkObject user, ItemHolder holder);
    }
}