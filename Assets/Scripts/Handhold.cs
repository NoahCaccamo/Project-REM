using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public enum HandholdType
    {
        Static,      // Current behavior - restricted movement
        Momentum,    // Free swing - preserves momentum, free movement
        Drift        // Drift around corners - requires slide, stores momentum, circular motion
    }

    [RequireComponent(typeof(Collider))]
    public class Handhold : MonoBehaviour
    {
        [Header("Handhold Properties")]
        public HandholdType handholdType = HandholdType.Static;

        [Header("Momentum Handhold Settings")]
        [Tooltip("How long after grabbing can you regain momentum (seconds)")]
        public float momentumGraceWindow = 0.5f;

        [Tooltip("Percentage of momentum restored when releasing within grace period")]
        [Range(0f, 1f)]
        public float momentumRetention = 1f;

        [Tooltip("Damping applied to swing velocity for momentum handholds")]
        public float swingDamping = 0.5f;

        [Tooltip("Maximum radius for momentum handholds")]
        public float maxSwingRadius = 5f;

        [Header("Static Handhold Settings")]
        [Tooltip("Maximum radius for static handholds")]
        public float maxStaticRadius = 2.5f;

        [Header("Drift Handhold Settings")]
        [Tooltip("Radius for drifting around the pole")]
        public float driftRadius = 2f;

        [Tooltip("How long after grabbing can you regain momentum when drifting")]
        public float driftMomentumWindow = 1f;

        [Tooltip("Percentage of momentum restored when releasing drift")]
        [Range(0f, 1.5f)]
        public float driftMomentumRetention = 1.1f;

        [Tooltip("Force pulling player toward drift radius")]
        public float driftRadiusSpring = 50f;

        [Tooltip("Damping for drift motion")]
        public float driftDamping = 0.1f;

        [Tooltip("Minimum speed required to grab drift pole (must be sliding)")]
        public float minDriftSpeed = 5f;

        [Tooltip("Minimum momentum maintained while drifting")]
        public float driftMinMomentum = 8f;

        private void OnValidate()
        {
            // Ensure the collider is on the climbable layer
            if (gameObject.layer != LayerMask.NameToLayer("Climbable"))
            {
                Debug.LogWarning($"Handhold '{gameObject.name}' should be on the 'Climbable' layer");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color gizmoColor = Color.green;
            float radius = maxStaticRadius;

            switch (handholdType)
            {
                case HandholdType.Static:
                    gizmoColor = Color.green;
                    radius = maxStaticRadius;
                    break;
                case HandholdType.Momentum:
                    gizmoColor = Color.cyan;
                    radius = maxSwingRadius;
                    break;
                case HandholdType.Drift:
                    gizmoColor = Color.yellow;
                    radius = driftRadius;
                    break;
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.15f);

            // Draw max radius
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);

            // For drift poles, draw vertical line and min momentum indicator
            if (handholdType == HandholdType.Drift)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 2f);
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);

                // Draw min momentum speed indicator (arrow at drift radius)
                Vector3 momentumArrow = transform.position + Vector3.forward * driftRadius;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(momentumArrow, momentumArrow + Vector3.right * (driftMinMomentum * 0.1f));
            }
        }
    }
}