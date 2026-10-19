using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Smoke from a damaged car (design doc §4), so everyone can see who is hurting: grey puffs at
    /// light damage, more and darker ones at heavy damage or while wrecked. Puffs are a small pool of
    /// spheres that rise, swell and shrink away; whoever owns the car sets <see cref="Level"/>.
    /// </summary>
    public sealed class CarSmoke : MonoBehaviour
    {
        private const int PoolSize = 14;
        private const float PuffSeconds = 1f;
        private const float RiseSpeed = 1.6f;

        private static readonly Color LightColour = new Color(0.62f, 0.62f, 0.64f);
        private static readonly Color HeavyColour = new Color(0.22f, 0.22f, 0.24f);

        private readonly Transform[] _puffs = new Transform[PoolSize];
        private readonly float[] _ages = new float[PoolSize];
        private readonly Renderer[] _renderers = new Renderer[PoolSize];
        private float _untilNext;
        private int _next;

        /// <summary>0 for no smoke, 1 for light, 2 for heavy.</summary>
        public int Level { get; set; }

        public static CarSmoke Build(Transform car)
        {
            var smoke = new GameObject("Smoke").AddComponent<CarSmoke>();
            smoke.transform.SetParent(car, false);
            smoke.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            return smoke;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_puffs[i] == null || !_puffs[i].gameObject.activeSelf)
                {
                    continue;
                }

                _ages[i] += dt;
                if (_ages[i] >= PuffSeconds)
                {
                    _puffs[i].gameObject.SetActive(false);
                    continue;
                }

                float life = _ages[i] / PuffSeconds;
                _puffs[i].position += Vector3.up * (RiseSpeed * dt);
                _puffs[i].localScale = Vector3.one * (0.35f + 0.6f * Mathf.Sin(life * Mathf.PI));
            }

            if (Level <= 0)
            {
                return;
            }

            _untilNext -= dt;
            if (_untilNext > 0f)
            {
                return;
            }

            _untilNext = Level >= 2 ? 0.07f : 0.18f;
            Emit(Level >= 2 ? HeavyColour : LightColour);
        }

        private void Emit(Color colour)
        {
            int i = _next;
            _next = (_next + 1) % PoolSize;
            if (_puffs[i] == null)
            {
                _puffs[i] = SimpleMaterials.Part(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one, SimpleMaterials.Get(colour));
                _puffs[i].name = "Smoke puff";
                _renderers[i] = _puffs[i].GetComponent<Renderer>();
            }

            // Puffs are left in the world, so they trail behind a moving car.
            _renderers[i].sharedMaterial = SimpleMaterials.Get(colour);
            _puffs[i].position = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            _puffs[i].localScale = Vector3.one * 0.35f;
            _ages[i] = 0f;
            _puffs[i].gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            foreach (var puff in _puffs)
            {
                if (puff != null)
                {
                    Destroy(puff.gameObject);
                }
            }
        }
    }
}
