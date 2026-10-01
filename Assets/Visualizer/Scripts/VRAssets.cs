using UnityEngine;

// Odkazy na assety pro VR, které nejsou v repu (modely ovladačů z Meta XR SDK balíčku).
// Plní je editor skript BuildQuest.Setup do Assets/Visualizer/Resources/VRAssets.asset.
public class VRAssets : ScriptableObject
{
    public GameObject leftController;
    public GameObject rightController;
    public Vector3 modelOffset = new Vector3(0f, -0.03f, -0.04f);   // posun modelu vůči OpenXR grip pose (jako OVRRuntimeController v Meta SDK)
    public Vector3 modelRotation = new Vector3(-60f, 0f, 0f);
}
