using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class EnemyIdleStretch : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float stretchAmount = 0.12f;
        [SerializeField, Min(0.01f)] private float cycleDuration = 1.5f;

        private Vector3 baseScale;
        private float elapsed;
        private bool hasBaseScale;

        private void OnEnable()
        {
            baseScale = transform.localScale;
            hasBaseScale = true;
            elapsed = Random.value * Mathf.Max(0.01f, cycleDuration);
            ApplyStretch();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            ApplyStretch();
        }

        private void ApplyStretch()
        {
            float phase = elapsed * (2f * Mathf.PI / Mathf.Max(0.01f, cycleDuration));
            float stretch = 1f + stretchAmount * (1f - Mathf.Cos(phase)) * 0.5f;
            transform.localScale = new Vector3(baseScale.x, baseScale.y, baseScale.z * stretch);
        }

        private void OnDisable()
        {
            if (hasBaseScale) transform.localScale = baseScale;
        }

        private void OnValidate()
        {
            stretchAmount = Mathf.Max(0f, stretchAmount);
            cycleDuration = Mathf.Max(0.01f, cycleDuration);
        }
    }
}
