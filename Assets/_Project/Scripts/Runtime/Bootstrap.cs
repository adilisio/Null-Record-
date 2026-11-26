using UnityEngine;

namespace Game
{
    /// <summary>Placeholder entry point. Drop on a GameObject in the first scene.</summary>
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log($"[{Application.productName}] booted ({Application.unityVersion})");
        }
    }
}
