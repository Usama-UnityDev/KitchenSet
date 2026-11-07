using UnityEngine;

/// <summary>Two-bone arm IK keeps both palms underneath the live serving plate.</summary>
[DefaultExecutionOrder(100)]
public class ServingChefRig : MonoBehaviour
{
    private Transform plate, head;
    private SpriteRenderer plateRenderer;
    private Quaternion headRest;
    private Arm left, right;
    private Transform leftSmile, rightSmile;
    private Vector3 leftSmileRest, rightSmileRest;
    public float WaistLocalY { get; set; }
    public float WaistWorldY => transform.TransformPoint(new Vector3(0f, WaistLocalY, 0f)).y;
    private Renderer[] visuals;
    private MaterialPropertyBlock crop;
    public float Offer { get; set; }

    private sealed class Arm
    {
        public Transform upper, forearm, hand;
        public float upperLength, lowerLength;
        public Quaternion palmBasis;
        public float side;
    }

    public void Initialize(Transform servingPlate, SpriteRenderer visual)
    {
        plate = servingPlate;
        crop = new MaterialPropertyBlock();
        plateRenderer = visual;
        visuals = GetComponentsInChildren<Renderer>();
        left = GetArm("L"); right = GetArm("R");
        head = Find("Bip01 Head");
        if (head != null) headRest = head.localRotation;
        leftSmile = Find("Bip01 LMouthCorner"); rightSmile = Find("Bip01 RMouthCorner");
        if (leftSmile != null) leftSmileRest = leftSmile.localPosition;
        if (rightSmile != null) rightSmileRest = rightSmile.localPosition;
        LateUpdate();
    }

    private Transform Find(string bone)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == bone) return child;
        return null;
    }

    private Arm GetArm(string side)
    {
        var arm = new Arm { upper = Find("Bip01 " + side + " UpperArm"), forearm = Find("Bip01 " + side + " Forearm"), hand = Find("Bip01 " + side + " Hand") };
        if (arm.upper == null || arm.forearm == null || arm.hand == null)
            throw new System.InvalidOperationException("Serving chef is missing its " + side + " arm bones.");
        arm.upperLength = Vector3.Distance(arm.upper.position, arm.forearm.position);
        arm.lowerLength = Vector3.Distance(arm.forearm.position, arm.hand.position);
        arm.side = Mathf.Sign(arm.upper.position.x - transform.position.x);
        Transform middle = Find("Bip01 " + side + " Finger2"), thumb = Find("Bip01 " + side + " Finger0"), little = Find("Bip01 " + side + " Finger4");
        Vector3 forward = arm.hand.InverseTransformDirection(middle.position - arm.hand.position).normalized;
        Vector3 normal = arm.hand.InverseTransformDirection(Vector3.Cross(thumb.position - arm.hand.position, little.position - arm.hand.position)).normalized;
        if (side == "R") normal = -normal;
        arm.palmBasis = Quaternion.LookRotation(forward, normal);
        return arm;
    }

    private void LateUpdate()
    {
        if (plate == null || left == null || crop == null) return;
        Bounds bounds = CakeToolScreenBounds.VisibleBounds(plateRenderer);
        Solve(left, bounds); Solve(right, bounds);
        if (head != null) head.localRotation = headRest * Quaternion.Euler(Offer * 5f, Mathf.Sin(Time.time * 1.7f) * 1.5f, 0f);
        float smile = (0.4f + Offer * 0.6f) * transform.TransformVector(Vector3.up).magnitude;
        if (leftSmile != null) leftSmile.localPosition = leftSmileRest + leftSmile.parent.InverseTransformVector(Vector3.up * (smile * 0.0065f));
        if (rightSmile != null) rightSmile.localPosition = rightSmileRest + rightSmile.parent.InverseTransformVector(Vector3.up * (smile * 0.0065f));
        crop.SetFloat("_ClipBelowWorldY", WaistWorldY);
        foreach (Renderer visual in visuals) visual.SetPropertyBlock(crop);
    }

    private static void Solve(Arm arm, Bounds bounds)
    {
        Vector3 target = new Vector3(bounds.center.x + arm.side * bounds.size.x * 0.39f, bounds.min.y + bounds.size.y * 0.30f, bounds.center.z + 0.06f);
        Vector3 root = arm.upper.position;
        arm.upperLength = Vector3.Distance(arm.upper.position, arm.forearm.position);
        arm.lowerLength = Vector3.Distance(arm.forearm.position, arm.hand.position);
        Vector3 delta = target - root;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(arm.upperLength - arm.lowerLength) + 0.001f, arm.upperLength + arm.lowerLength - 0.001f);
        Vector3 direction = delta.normalized;
        // Elbows stay down and outside the torso while palms turn upward.
        Vector3 bend = new Vector3(arm.side * 0.7f, -1f, 0.25f);
        bend = (bend - direction * Vector3.Dot(bend, direction)).normalized;
        float along = (arm.upperLength * arm.upperLength - arm.lowerLength * arm.lowerLength + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, arm.upperLength * arm.upperLength - along * along));
        Vector3 elbow = root + direction * along + bend * height;
        arm.upper.rotation = Quaternion.FromToRotation(arm.forearm.position - root, elbow - root) * arm.upper.rotation;
        arm.forearm.rotation = Quaternion.FromToRotation(arm.hand.position - arm.forearm.position, target - arm.forearm.position) * arm.forearm.rotation;
        arm.hand.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Inverse(arm.palmBasis);
    }
}
