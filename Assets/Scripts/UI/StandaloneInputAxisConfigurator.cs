using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Complete
{
    /// <summary>
    /// Ensures the UI EventSystem uses the UI-specific axes defined in the Input Manager.
    /// </summary>
    public static class StandaloneInputAxisConfigurator
    {
        private const string HorizontalAxisName = "HorizontalUI";
        private const string VerticalAxisName = "VerticalUI";
        private const string SubmitButtonName = "Submit";
        private const string CancelButtonName = "Cancel";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ConfigureCurrentScene()
        {
            ApplyToScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToScene();
        }

        private static void ApplyToScene()
        {
            var modules = Object.FindObjectsOfType<StandaloneInputModule>();
            foreach (var module in modules)
            {
                Configure(module);
            }
        }

        private static void Configure(StandaloneInputModule module)
        {
            if (module == null)
            {
                return;
            }

            module.horizontalAxis = HorizontalAxisName;
            module.verticalAxis = VerticalAxisName;
            module.submitButton = SubmitButtonName;
            module.cancelButton = CancelButtonName;
        }
    }
}
