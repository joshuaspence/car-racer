using CarRacer.Core.Items;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Items
{
    /// <summary>
    /// Shows the track's item boxes (design doc §8): spinning yellow cubes that disappear while
    /// taken and come back when the host says they respawn. Which boxes are up comes from the
    /// session's <see cref="IItemView"/>.
    /// </summary>
    public sealed class ItemBoxes : MonoBehaviour
    {
        private const float SpinDegreesPerSecond = 90f;
        private const float Size = 1.1f;
        private static readonly Color Colour = new Color(0.98f, 0.78f, 0.15f);

        private IItemView _items;
        private Transform[] _boxes = new Transform[0];

        public void Initialise(IItemView items)
        {
            _items = items;
            var material = SimpleMaterials.Get(Colour);
            _boxes = new Transform[items.Boxes.Count];
            for (int i = 0; i < _boxes.Length; i++)
            {
                var box = new GameObject($"Item box {i}").transform;
                box.SetParent(transform, false);
                box.position = items.Boxes.Position(i).ToUnity();
                var cube = SimpleMaterials.Part(PrimitiveType.Cube, box, Vector3.zero, Vector3.one * Size, material);
                cube.localRotation = Quaternion.Euler(35f, 0f, 35f);
                _boxes[i] = box;
            }
        }

        private void Update()
        {
            if (_items == null)
            {
                return;
            }

            long now = NetworkSession.NowMs;
            var spin = Quaternion.Euler(0f, SpinDegreesPerSecond * Time.deltaTime, 0f);
            for (int i = 0; i < _boxes.Length; i++)
            {
                bool up = _items.Boxes.IsUp(i, now);
                if (_boxes[i].gameObject.activeSelf != up)
                {
                    _boxes[i].gameObject.SetActive(up);
                }

                _boxes[i].rotation = spin * _boxes[i].rotation;
            }
        }
    }
}
