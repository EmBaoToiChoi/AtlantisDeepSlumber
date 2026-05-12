//
// Weather Maker for Unity
// (c) 2016 Digital Ruby, LLC
// Source code may be used for personal or commercial projects.
// Source code may NOT be redistributed or sold.
// 
// *** A NOTE ABOUT PIRACY ***
// 
// If you got this asset from a pirate site, please consider buying it from the Unity asset store at https://assetstore.unity.com/packages/slug/60955?aid=1011lGnL. This asset is only legally available from the Unity Asset Store.
// 
// I'm a single indie dev supporting my family by spending hundreds and thousands of hours on this and other assets. It's very offensive, rude and just plain evil to steal when I (and many others) put so much hard work into the software.
// 
// Thank you.
//
// *** END NOTE ABOUT PIRACY ***
//

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Launcher script to show how to use a single weather maker instance with player and main menu and game scene
    /// </summary>
    public class WeatherMakerLauncherScript : MonoBehaviour
    {
        private bool isLaunchScene = true;
        private bool sceneActiveWiredUp;

        [Tooltip("Set this to the scene name for your main menu")]
        public string MainMenuSceneName;

        [Tooltip("Set this to the initial scene name for your game")]
        public string GameSceneName;

        [Tooltip("The name of your button that plays the game")]
        public string PlayGameButtonName;

        [Tooltip("The name of the button that goes to the main menu")]
        public string MainMenuButtonName;

        [Tooltip("Whether to show Weather Maker on the main menu")]
        public bool ShowWeatherMakerOnMainMenu = true;

        [Tooltip("Set this to your player object in your launch scene")]
        public GameObject Player;

        private void SceneManager_activeSceneChanged(UnityEngine.SceneManagement.Scene arg0, UnityEngine.SceneManagement.Scene arg1)
        {
            GameObject playButton = GameObject.Find(PlayGameButtonName);
            if (playButton != null)
            {
                Button button = playButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => GameMenuButtonClicked());
                }
            }
            GameObject mainMenuButton = GameObject.Find(MainMenuButtonName);
            if (mainMenuButton != null)
            {
                Button button = mainMenuButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => MainMenuButtonClicked());
                }
            }
        }

        private void OnEnable()
        {
            if (instance == null)
            {
                instance = this;
            }

            if (!sceneActiveWiredUp)
            {
                sceneActiveWiredUp = true;
                UnityEngine.SceneManagement.SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
            }

            if (isLaunchScene)
            {
                isLaunchScene = false;
                GameObject.DontDestroyOnLoad(gameObject);
                if (Player != null)
                {
                    GameObject.DontDestroyOnLoad(Player);
                }
                StartCoroutine(StartMainMenu());
            }
        }

        public void MainMenuButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenuSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (Player != null)
            {
                // TODO: You can disable your player controller here if you don't want movement in the main menu
            }

            if (!ShowWeatherMakerOnMainMenu && WeatherMakerScript.HasInstance())
            {
                // turn off weather effects
                WeatherMakerScript.Instance.gameObject.SetActive(false);
            }
        }

        public void GameMenuButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (Player != null)
            {
                // TODO: You can enable your player controller here, and enable the weather maker prefab if it was disabled
            }

            StartCoroutine(TurnOnWeatherMaker());
        }

        private IEnumerator StartMainMenu()
        {
            // give onenable, etc. time to kick off
            yield return new WaitForSeconds(0.01f);

            MainMenuButtonClicked();
        }

        private IEnumerator TurnOnWeatherMaker()
        {
            // give time for weather maker to activate
            yield return new WaitForSeconds(0.01f);

            if (WeatherMakerScript.HasInstance())
            {
                // turn on weather effects
                WeatherMakerScript.Instance.gameObject.SetActive(true);
            }
        }

        private static WeatherMakerLauncherScript instance;
        /// <summary>
        /// Get the instance of the launcher script
        /// </summary>
        public static WeatherMakerLauncherScript Instance
        {
            get { return instance; }
        }
    }
}