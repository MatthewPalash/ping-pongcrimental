using System.Collections.Generic;
using UnityEngine;

namespace TennisPrototype
{
    [DisallowMultipleComponent]
    public sealed class EnemyVisualEffects : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly GameObject[] stateObjects = new GameObject[4];
        private readonly Vector3[] basePositions = new Vector3[4];
        private readonly Vector3[] baseScales = new Vector3[4];
        private readonly Quaternion[] baseRotations = new Quaternion[4];
        private readonly List<Renderer> flashRenderers = new List<Renderer>();
        private readonly List<Material[]> originalMaterials = new List<Material[]>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Material> flashMaterials = new List<Material>();
        private readonly List<Color> baseEmission = new List<Color>();

        private EnemyConfig config;
        private int activeIndex = -1;
        private int outgoingIndex = -1;
        private float transitionElapsed;
        private float knockbackElapsed;
        private float flashElapsed;
        private bool transitioning;
        private bool hitFeedback;
        private Vector3 localBackward;

        public void Initialize(GameObject idle, GameObject hurt, GameObject dead,
            GameObject won, EnemyConfig settings)
        {
            config = settings;
            stateObjects[0] = idle;
            stateObjects[1] = hurt;
            stateObjects[2] = dead;
            stateObjects[3] = won;
            localBackward = transform.InverseTransformVector(Vector3.forward).normalized;

            for (int i = 0; i < stateObjects.Length; i++)
            {
                if (stateObjects[i] == null) continue;
                Transform state = stateObjects[i].transform;
                basePositions[i] = state.localPosition;
                baseScales[i] = state.localScale;
                baseRotations[i] = state.localRotation;
                stateObjects[i].SetActive(false);
            }

            for (int i = 0; i < stateObjects.Length; i++)
                PrepareMaterials(stateObjects[i],
                    i == (int)EnemyState.Hurt || i == (int)EnemyState.Dead);
        }

        public void Show(EnemyState state)
        {
            int next = (int)state;
            if (next < 0 || next >= stateObjects.Length) return;

            if (transitioning) CompleteTransition();
            int previous = activeIndex == next ? -1 : activeIndex;
            outgoingIndex = previous;
            activeIndex = next;
            transitionElapsed = 0f;
            transitioning = previous >= 0;

            for (int i = 0; i < stateObjects.Length; i++)
            {
                ResetPose(i);
                if (stateObjects[i] != null)
                    stateObjects[i].SetActive(i == next || i == previous);
            }

            if (previous >= 0)
            {
                // Enter with a compact pose; the update expands it past its resting size.
                SetEntryPose(0f);
            }

            hitFeedback = state == EnemyState.Hurt || state == EnemyState.Dead;
            knockbackElapsed = 0f;
            flashElapsed = 0f;
            SetFlash(hitFeedback ? 1f : 0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (transitioning)
            {
                transitionElapsed += dt;
                float duration = config != null ? config.StateTransitionDuration : 0.28f;
                float t = Mathf.Clamp01(transitionElapsed / duration);
                UpdateTransition(t);
                if (t >= 1f) CompleteTransition();
            }

            if (!hitFeedback) return;
            knockbackElapsed += dt;
            flashElapsed += dt;
            if (activeIndex == (int)EnemyState.Hurt || activeIndex == (int)EnemyState.Dead)
                UpdateRecoilPosition();

            float flashDuration = config != null ? config.HurtFlashDuration : 0.18f;
            float flash = 1f - Mathf.Clamp01(flashElapsed / flashDuration);
            SetFlash(flash * flash);
            if (knockbackElapsed >= RecoilDuration &&
                flashElapsed >= flashDuration)
                hitFeedback = false;
        }

        private void UpdateTransition(float t)
        {
            if (outgoingIndex >= 0 && stateObjects[outgoingIndex] != null)
            {
                float exit = Smooth01(Mathf.Clamp01(t / 0.42f));
                Transform outgoing = stateObjects[outgoingIndex].transform;
                outgoing.localScale = baseScales[outgoingIndex] * Mathf.Lerp(1f, 0.7f, exit);
                outgoing.localRotation = baseRotations[outgoingIndex] *
                    Quaternion.Euler(0f, -8f * exit, 0f);
                if (t >= 0.42f)
                {
                    stateObjects[outgoingIndex].SetActive(false);
                    ResetPose(outgoingIndex);
                    outgoingIndex = -1;
                }
            }

            SetEntryPose(Mathf.Clamp01((t - 0.12f) / 0.88f));
        }

        private void SetEntryPose(float t)
        {
            if (activeIndex < 0 || stateObjects[activeIndex] == null) return;
            Transform incoming = stateObjects[activeIndex].transform;
            float scale = Mathf.LerpUnclamped(0.72f, 1f, EaseOutBack(t));
            incoming.localScale = baseScales[activeIndex] * scale;
            incoming.localRotation = baseRotations[activeIndex] *
                Quaternion.Euler(0f, 10f * (1f - Smooth01(t)), 0f);
            incoming.localPosition = basePositions[activeIndex] +
                Vector3.down * (0.045f * (1f - Smooth01(t)));
        }

        private void UpdateRecoilPosition()
        {
            bool dead = activeIndex == (int)EnemyState.Dead;
            float distance = dead
                ? (config != null ? config.DeathKnockbackDistance : 0.65f)
                : (config != null ? config.HurtKnockbackDistance : 0.18f);
            float t = Mathf.Clamp01(knockbackElapsed / RecoilDuration);
            // A normal hit springs back; death carries the visual away and leaves it there.
            float offset = dead ? 1f - Mathf.Pow(1f - t, 3f)
                : t < 0.22f
                    ? 1f - Mathf.Pow(1f - t / 0.22f, 3f)
                    : 1f - Smooth01((t - 0.22f) / 0.78f);
            Vector3 entryPosition = basePositions[activeIndex];
            if (transitioning)
            {
                float transitionDuration = config != null ? config.StateTransitionDuration : 0.28f;
                float transitionT = Mathf.Clamp01(transitionElapsed / transitionDuration);
                float entryT = Mathf.Clamp01((transitionT - 0.12f) / 0.88f);
                entryPosition += Vector3.down * (0.045f * (1f - Smooth01(entryT)));
            }
            stateObjects[activeIndex].transform.localPosition = entryPosition +
                localBackward * (distance * offset);
        }

        private float RecoilDuration => activeIndex == (int)EnemyState.Dead
            ? (config != null ? config.DeathKnockbackDuration : 0.42f)
            : (config != null ? config.HurtKnockbackDuration : 0.32f);

        private void CompleteTransition()
        {
            transitioning = false;
            if (outgoingIndex >= 0 && stateObjects[outgoingIndex] != null)
            {
                stateObjects[outgoingIndex].SetActive(false);
                ResetPose(outgoingIndex);
            }
            outgoingIndex = -1;
            ResetPose(activeIndex);
        }

        private void ResetPose(int index)
        {
            if (index < 0 || stateObjects[index] == null) return;
            Transform state = stateObjects[index].transform;
            state.localPosition = basePositions[index];
            state.localScale = baseScales[index];
            state.localRotation = baseRotations[index];
        }

        private void PrepareMaterials(GameObject stateObject, bool canFlash)
        {
            if (stateObject == null) return;
            foreach (Renderer renderer in stateObject.GetComponentsInChildren<Renderer>(true))
            {
                Material[] originals = renderer.sharedMaterials;
                Material[] copies = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] == null) continue;
                    Material copy = new Material(originals[i]);
                    copies[i] = copy;
                    ownedMaterials.Add(copy);
                    if (copy.HasProperty(BaseColor) && config != null)
                        copy.SetColor(BaseColor, copy.GetColor(BaseColor) * config.VisualTint);
                    if (!canFlash || !copy.HasProperty(EmissionColor)) continue;
                    copy.EnableKeyword("_EMISSION");
                    flashMaterials.Add(copy);
                    baseEmission.Add(copy.GetColor(EmissionColor));
                }
                flashRenderers.Add(renderer);
                originalMaterials.Add(originals);
                renderer.sharedMaterials = copies;
            }
        }

        private void SetFlash(float amount)
        {
            float intensity = config != null ? config.HurtFlashIntensity : 4f;
            for (int i = 0; i < flashMaterials.Count; i++)
                flashMaterials[i].SetColor(EmissionColor,
                    baseEmission[i] + Color.white * (intensity * amount));
        }

        private void OnDisable()
        {
            SetFlash(0f);
            if (transitioning) CompleteTransition();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < flashRenderers.Count; i++)
                if (flashRenderers[i] != null) flashRenderers[i].sharedMaterials = originalMaterials[i];
            foreach (Material material in ownedMaterials)
                if (material != null) Destroy(material);
        }

        private static float Smooth01(float t) => t * t * (3f - 2f * t);

        private static float EaseOutBack(float t)
        {
            float x = t - 1f;
            return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
        }
    }
}
