using Godot;

// Swings a humanoid rig's limbs from a sine wave rather than an animation clip. Kept out of Enemy
// so an enemy with a different rig — or none at all, like a flyer or a spirit — isn't carrying
// bone-posing code it can't use.
//
// Bone names are the humanoid convention this project's enemy rigs use; a rig missing any of them
// simply doesn't animate that limb (Skeleton3D.FindBone returns -1, which SetBonePoseRotation
// ignores).
public class ProceduralWalkAnimator
{
    private readonly Skeleton3D _skeleton;
    private readonly float _cycleFrequency;
    private readonly float _legSwingAmplitude;
    private readonly float _armSwingAmplitude;

    private readonly int _legLBoneIdx;
    private readonly int _legRBoneIdx;
    private readonly int _armLBoneIdx;
    private readonly int _armRBoneIdx;

    private float _walkTime;

    public ProceduralWalkAnimator(Skeleton3D skeleton, float cycleFrequency, float legSwingAmplitude, float armSwingAmplitude)
    {
        _skeleton = skeleton;
        _cycleFrequency = cycleFrequency;
        _legSwingAmplitude = legSwingAmplitude;
        _armSwingAmplitude = armSwingAmplitude;

        _legLBoneIdx = skeleton.FindBone("Leg.L");
        _legRBoneIdx = skeleton.FindBone("Leg.R");
        _armLBoneIdx = skeleton.FindBone("Arm.L");
        _armRBoneIdx = skeleton.FindBone("Arm.R");
    }

    // moveAmount scales both the cycle's advance and the swing size, so a stationary enemy winds
    // down to a neutral pose instead of marching in place.
    public void Animate(double moveAmount, double delta)
    {
        _walkTime += (float)(moveAmount * delta * _cycleFrequency);

        float legAngle = _legSwingAmplitude * Mathf.Sin(_walkTime) * (float)moveAmount;
        float armAngle = _armSwingAmplitude * Mathf.Sin(_walkTime) * (float)moveAmount;

        _skeleton.SetBonePoseRotation(_legLBoneIdx, new Quaternion(Vector3.Right, legAngle));
        _skeleton.SetBonePoseRotation(_legRBoneIdx, new Quaternion(Vector3.Right, -legAngle));
        _skeleton.SetBonePoseRotation(_armLBoneIdx, new Quaternion(Vector3.Right, -armAngle));
        _skeleton.SetBonePoseRotation(_armRBoneIdx, new Quaternion(Vector3.Right, armAngle));
    }
}
