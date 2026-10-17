using System.Collections.Generic;

namespace CarRacer.Core.Items
{
    /// <summary>
    /// Host side: the one item each player is holding (design doc §8). A player holding an item drives
    /// through boxes without taking them, leaving them for others, and can only use the item they hold.
    /// </summary>
    public sealed class ItemSlots
    {
        private readonly Dictionary<byte, ItemKind> _held = new Dictionary<byte, ItemKind>();

        public ItemKind Held(byte playerId) => _held.TryGetValue(playerId, out var kind) ? kind : ItemKind.None;

        public bool CanPickUp(byte playerId) => Held(playerId) == ItemKind.None;

        public void Give(byte playerId, ItemKind kind)
        {
            if (kind == ItemKind.None)
            {
                _held.Remove(playerId);
            }
            else
            {
                _held[playerId] = kind;
            }
        }

        /// <summary>Uses the held item if it is <paramref name="kind"/>, emptying the slot. False if the player holds something else or nothing.</summary>
        public bool TryUse(byte playerId, ItemKind kind)
        {
            if (kind == ItemKind.None || Held(playerId) != kind)
            {
                return false;
            }

            _held.Remove(playerId);
            return true;
        }

        /// <summary>Empties a player's slot, when they leave.</summary>
        public void Remove(byte playerId) => _held.Remove(playerId);

        /// <summary>Empties every slot, for a new race.</summary>
        public void Clear() => _held.Clear();
    }
}
