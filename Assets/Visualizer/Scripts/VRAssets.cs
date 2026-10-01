using UnityEngine;

// Odkazy na assety pro VR, které nejsou v repu (modely ovladačů z Meta XR SDK balíčku).
// Plní je editor skript BuildQuest.Setup do Assets/Visualizer/Resources/VRAssets.asset.
public class VRAssets : ScriptableObject
{
    public GameObject leftController;
    public GameObject rightController;
    public Vector3 modelOffset = Vector3.zero;
    public Vector3 modelRotation = Vector3.zero;
}
