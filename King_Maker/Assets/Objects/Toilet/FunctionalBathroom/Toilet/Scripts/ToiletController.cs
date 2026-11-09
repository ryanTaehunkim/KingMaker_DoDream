using UnityEngine;
using UnityEngine.Events;

namespace EZPZGames.FunctionalBathroom
{
   [RequireComponent(typeof(Animator))]
    public class ToiletController : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private string flushStateName = "Flush";
        [Tooltip("Playback speed of the flush. Audio pitch scales to match.")]
        [SerializeField] [Range(0.5f, 2f)] private float flushSpeed = 1f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip flushClip;
        [Tooltip("Scale audio pitch with flush speed so sound stays in sync.")]
        [SerializeField] private bool syncAudioPitch = true;

        [Header("Events")]
        public UnityEvent OnFlushStarted;
        public UnityEvent OnFlushCompleted;

        private Animator animator;
        private bool isFlushing;

        public bool IsFlushing
        {
            get
            {
                return isFlushing;
            }
        }

        public float FlushSpeed
        {
            get
            {
                return flushSpeed;
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ApplySpeed();
        }

        /// <summary>
        /// Starts the flush. Ignored if a flush is already running.
        /// </summary>
        public void Flush()
        {
            if (isFlushing)
            {
                return;
            }

            isFlushing = true;
            ApplySpeed();
            animator.Play(flushStateName, 0, 0f);

            if (audioSource != null && flushClip != null)
            {
                audioSource.clip = flushClip;
                audioSource.Play();
            }

            OnFlushStarted?.Invoke();
        }

        /// <summary>
        /// Changes how fast the flush plays. Takes effect on the next flush, or immediately if one is already running.
        /// </summary>
        public void SetFlushSpeed(float speed)
        {
            flushSpeed = Mathf.Clamp(speed, 0.5f, 2f);
            ApplySpeed();
        }

        /// <summary>
        /// Stops the flush immediately and returns to the idle state.
        /// </summary>
        public void CancelFlush()
        {
            if (!isFlushing)
            {
                return;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            isFlushing = false;
        }

        private void ApplySpeed()
        {
            if (animator != null)
            {
                animator.speed = flushSpeed;
            }

            if (audioSource != null && syncAudioPitch)
            {
                audioSource.pitch = flushSpeed;
            }
        }

        /// <summary>
        /// Called by an Animation Event on the final frame of the flush clip.
        /// </summary>
        private void OnFlushAnimationComplete()
        {
            isFlushing = false;
            OnFlushCompleted?.Invoke();
        }

        [ContextMenu("Test Flush")]
        private void TestFlush()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode to test the flush.", this);
                return;
            }

            Flush();
        }
    }
}
